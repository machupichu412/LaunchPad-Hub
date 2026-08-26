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
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { submitReview } from '../../api/reviews';
import { PageHeader } from '../../components/PageHeader';
import type { Checkpoint, ReviewType } from '../../api/types';

const useStyles = makeStyles({
  form: {
    maxWidth: '560px',
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  ratingRow: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: tokens.spacingHorizontalM,
  },
});

const ratingOptions = [1, 2, 3, 4, 5];

const pageCopy: Record<ReviewType, { title: string; subtitle: string }> = {
  SponsorOnCandidate: { title: 'Submit review', subtitle: "Rate the candidate's engagement and share qualitative feedback." },
  CandidateOnSponsor: { title: 'Review your sponsor', subtitle: "Rate your sponsor's support and share qualitative feedback." },
  ProjectEval: { title: 'Review your project', subtitle: 'Rate your project experience and share qualitative feedback.' },
};

const dimensionLabels: Record<ReviewType, { commitment: string; availability: string; guidance: string; outputQuality: string }> = {
  SponsorOnCandidate: {
    commitment: 'Commitment', availability: 'Availability', guidance: 'Guidance received', outputQuality: 'Output quality',
  },
  CandidateOnSponsor: {
    commitment: 'Engagement', availability: 'Availability', guidance: 'Guidance provided', outputQuality: 'Support quality',
  },
  ProjectEval: {
    commitment: 'Clarity of scope', availability: 'Resources provided', guidance: 'Guidance provided', outputQuality: 'Overall experience',
  },
};

const feedbackLabels: Record<ReviewType, { strengths: string; growthAreas: string }> = {
  SponsorOnCandidate: { strengths: 'Strengths', growthAreas: 'Growth areas' },
  CandidateOnSponsor: { strengths: "What's working well", growthAreas: 'What could be better' },
  ProjectEval: { strengths: "What's working well", growthAreas: 'What could be better' },
};

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

// Midpoint/final review submission for all three ReviewTypes — reached either via the
// sponsor's original self-serve route (/candidates/:assignmentId/review, no reviewType/
// checkpoint params — defaults below preserve that flow exactly) or the generalized
// deep link from an Ops-scheduled review to-do (/reviews/submit/:assignmentId/:reviewType/
// :checkpoint — see Tasks.tsx/ManageTodos.tsx). The response never carries a score back —
// see SponsorReviewDto — so there's nothing here that could accidentally render one.
export function SubmitReview() {
  const styles = useStyles();
  const { assignmentId, reviewType: reviewTypeParam, checkpoint: checkpointParam } = useParams<{
    assignmentId: string;
    reviewType?: string;
    checkpoint?: string;
  }>();
  const parsedAssignmentId = Number(assignmentId);
  const reviewType = (reviewTypeParam as ReviewType | undefined) ?? 'SponsorOnCandidate';
  const checkpointLocked = checkpointParam != null;

  const [checkpoint, setCheckpoint] = useState<Checkpoint>((checkpointParam as Checkpoint | undefined) ?? 'Midpoint');
  const [commitment, setCommitment] = useState('');
  const [availability, setAvailability] = useState('');
  const [guidance, setGuidance] = useState('');
  const [outputQuality, setOutputQuality] = useState('');
  const [comments, setComments] = useState('');
  const [strengths, setStrengths] = useState('');
  const [growthAreas, setGrowthAreas] = useState('');
  const [recommendConversion, setRecommendConversion] = useState(false);

  const hasAnyRating = [commitment, availability, guidance, outputQuality].some((v) => v.length > 0);
  const dimensions = dimensionLabels[reviewType];
  const feedback = feedbackLabels[reviewType];

  const submitMutation = useMutation({
    mutationFn: () =>
      submitReview({
        assignmentId: parsedAssignmentId,
        reviewType,
        checkpoint,
        commitment: commitment ? Number(commitment) : null,
        availability: availability ? Number(availability) : null,
        guidance: guidance ? Number(guidance) : null,
        outputQuality: outputQuality ? Number(outputQuality) : null,
        comments: comments.trim().length > 0 ? comments.trim() : null,
        strengths: strengths.trim().length > 0 ? strengths.trim() : null,
        growthAreas: growthAreas.trim().length > 0 ? growthAreas.trim() : null,
        recommendConversion: reviewType === 'SponsorOnCandidate' ? recommendConversion : null,
      }),
  });

  if (submitMutation.isSuccess) {
    return (
      <PageHeader
        title="Review submitted"
        subtitle={`Thanks — your ${checkpoint.toLowerCase()} review has been recorded.`}
      />
    );
  }

  return (
    <>
      <PageHeader title={pageCopy[reviewType].title} subtitle={pageCopy[reviewType].subtitle} />

      <div className={styles.form}>
        <Field label="Checkpoint">
          <Select
            value={checkpoint}
            disabled={checkpointLocked}
            onChange={(_, data) => setCheckpoint(data.value as Checkpoint)}
          >
            <option value="Midpoint">Midpoint</option>
            <option value="Final">Final</option>
          </Select>
        </Field>

        <div className={styles.ratingRow}>
          <RatingField label={dimensions.commitment} value={commitment} onChange={setCommitment} />
          <RatingField label={dimensions.availability} value={availability} onChange={setAvailability} />
          <RatingField label={dimensions.guidance} value={guidance} onChange={setGuidance} />
          <RatingField label={dimensions.outputQuality} value={outputQuality} onChange={setOutputQuality} />
        </div>

        <Field label="Comments">
          <Textarea value={comments} onChange={(_, data) => setComments(data.value)} resize="vertical" />
        </Field>
        <Field label={feedback.strengths}>
          <Textarea value={strengths} onChange={(_, data) => setStrengths(data.value)} resize="vertical" />
        </Field>
        <Field label={feedback.growthAreas}>
          <Textarea value={growthAreas} onChange={(_, data) => setGrowthAreas(data.value)} resize="vertical" />
        </Field>
        {reviewType === 'SponsorOnCandidate' && (
          <Checkbox
            label="On track for conversion"
            checked={recommendConversion}
            onChange={(_, data) => setRecommendConversion(data.checked === true)}
          />
        )}

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
