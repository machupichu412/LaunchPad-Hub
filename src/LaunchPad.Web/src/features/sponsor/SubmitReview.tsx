import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import {
  Body1,
  Button,
  Checkbox,
  Field,
  Select,
  Textarea,
  Title2,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { submitReview } from '../../api/reviews';
import type { Checkpoint } from '../../api/types';

const useStyles = makeStyles({
  form: {
    maxWidth: '560px',
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    marginTop: tokens.spacingVerticalM,
  },
  ratingRow: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: tokens.spacingHorizontalM,
  },
});

const ratingOptions = [1, 2, 3, 4, 5];

function RatingField({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <Field label={label}>
      <Select value={value} onChange={(_, data) => onChange(data.value)}>
        <option value="">Not rated</option>
        {ratingOptions.map((n) => (
          <option key={n} value={n}>
            {n}
          </option>
        ))}
      </Select>
    </Field>
  );
}

// Midpoint/final review submission. The response never carries a score back — see
// SponsorReviewDto — so there's nothing here that could accidentally render one.
export function SubmitReview() {
  const styles = useStyles();
  const { assignmentId } = useParams<{ assignmentId: string }>();
  const parsedAssignmentId = Number(assignmentId);

  const [checkpoint, setCheckpoint] = useState<Checkpoint>('Midpoint');
  const [commitment, setCommitment] = useState('');
  const [availability, setAvailability] = useState('');
  const [guidance, setGuidance] = useState('');
  const [outputQuality, setOutputQuality] = useState('');
  const [comments, setComments] = useState('');
  const [strengths, setStrengths] = useState('');
  const [growthAreas, setGrowthAreas] = useState('');
  const [recommendConversion, setRecommendConversion] = useState(false);

  const hasAnyRating = [commitment, availability, guidance, outputQuality].some((v) => v.length > 0);

  const submitMutation = useMutation({
    mutationFn: () =>
      submitReview({
        assignmentId: parsedAssignmentId,
        reviewType: 'SponsorOnCandidate',
        checkpoint,
        commitment: commitment ? Number(commitment) : null,
        availability: availability ? Number(availability) : null,
        guidance: guidance ? Number(guidance) : null,
        outputQuality: outputQuality ? Number(outputQuality) : null,
        comments: comments.trim().length > 0 ? comments.trim() : null,
        strengths: strengths.trim().length > 0 ? strengths.trim() : null,
        growthAreas: growthAreas.trim().length > 0 ? growthAreas.trim() : null,
        recommendConversion,
      }),
  });

  if (submitMutation.isSuccess) {
    return (
      <>
        <Title2>Review submitted</Title2>
        <Body1>Thanks — your {checkpoint.toLowerCase()} review has been recorded.</Body1>
      </>
    );
  }

  return (
    <>
      <Title2>Submit review</Title2>
      <Body1>Rate the candidate's engagement and share qualitative feedback.</Body1>

      <div className={styles.form}>
        <Field label="Checkpoint">
          <Select value={checkpoint} onChange={(_, data) => setCheckpoint(data.value as Checkpoint)}>
            <option value="Midpoint">Midpoint</option>
            <option value="Final">Final</option>
          </Select>
        </Field>

        <div className={styles.ratingRow}>
          <RatingField label="Commitment" value={commitment} onChange={setCommitment} />
          <RatingField label="Availability" value={availability} onChange={setAvailability} />
          <RatingField label="Guidance received" value={guidance} onChange={setGuidance} />
          <RatingField label="Output quality" value={outputQuality} onChange={setOutputQuality} />
        </div>

        <Field label="Comments">
          <Textarea value={comments} onChange={(_, data) => setComments(data.value)} resize="vertical" />
        </Field>
        <Field label="Strengths">
          <Textarea value={strengths} onChange={(_, data) => setStrengths(data.value)} resize="vertical" />
        </Field>
        <Field label="Growth areas">
          <Textarea value={growthAreas} onChange={(_, data) => setGrowthAreas(data.value)} resize="vertical" />
        </Field>
        <Checkbox
          label="On track for conversion"
          checked={recommendConversion}
          onChange={(_, data) => setRecommendConversion(data.checked === true)}
        />

        <Button
          appearance="primary"
          disabled={!hasAnyRating || submitMutation.isPending}
          onClick={() => submitMutation.mutate()}
        >
          {submitMutation.isPending ? 'Submitting...' : 'Submit review'}
        </Button>
        {!hasAnyRating && <Body1>Rate at least one dimension before submitting.</Body1>}
        {submitMutation.isError && <Body1>Failed to submit: {(submitMutation.error as Error).message}</Body1>}
      </div>
    </>
  );
}
