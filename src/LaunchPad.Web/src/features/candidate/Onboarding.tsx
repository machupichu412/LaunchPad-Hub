import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Avatar,
  Body1,
  Button,
  Caption1,
  Card,
  Field,
  Input,
  Select,
  Spinner,
  Textarea,
  Title3,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { createMyCandidateProfile, getMyCandidateProfile } from '../../api/candidates';
import { AvatarEditorDialog } from '../../components/AvatarEditorDialog';
import { PageHeader } from '../../components/PageHeader';
import { useMyAvatarUrl } from '../../auth/useMyAvatarUrl';
import { SkillPicker } from './SkillPicker';
import type { Availability } from '../../api/types';

const useStyles = makeStyles({
  section: {
    padding: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalL,
    maxWidth: '640px',
  },
  fieldGrid: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: tokens.spacingHorizontalM,
  },
  photoRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalM,
  },
});

// Self-service onboarding for a Candidate-role user with no Candidate row yet (see
// RequireCandidateProfile — this page is where it redirects). One page, two
// sections rather than a multi-step wizard, per the goal of keeping this simple.
export function Onboarding() {
  const styles = useStyles();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: profile, isLoading } = useQuery({
    queryKey: ['candidates', 'me'],
    queryFn: getMyCandidateProfile,
  });
  const { url: avatarUrl } = useMyAvatarUrl();

  const [location, setLocation] = useState('');
  const [availability, setAvailability] = useState<Availability>('PartTime');
  const [graduationDate, setGraduationDate] = useState('');
  const [bio, setBio] = useState('');
  const [school, setSchool] = useState('');
  const [degree, setDegree] = useState('');
  const [gpa, setGpa] = useState('');
  const [linkedInUrl, setLinkedInUrl] = useState('');
  const [portfolioUrl, setPortfolioUrl] = useState('');
  const [skillIds, setSkillIds] = useState<number[]>([]);

  const createMutation = useMutation({
    mutationFn: () =>
      createMyCandidateProfile({
        location: location.trim().length > 0 ? location.trim() : null,
        availability,
        graduationDate: graduationDate.trim().length > 0 ? graduationDate : null,
        linkedInUrl: linkedInUrl.trim().length > 0 ? linkedInUrl.trim() : null,
        portfolioUrl: portfolioUrl.trim().length > 0 ? portfolioUrl.trim() : null,
        bio: bio.trim().length > 0 ? bio.trim() : null,
        school: school.trim().length > 0 ? school.trim() : null,
        degree: degree.trim().length > 0 ? degree.trim() : null,
        gpa: gpa.trim().length > 0 ? Number(gpa) : null,
        skillIds,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['candidates', 'me'] });
      navigate('/dashboard');
    },
  });

  if (isLoading) return <Spinner label="Checking your profile..." />;
  // Already onboarded — nothing to do here, this page is create-only.
  if (profile) return <Navigate to="/dashboard" replace />;

  return (
    <>
      <PageHeader
        title="Welcome to LaunchPad"
        subtitle="Let's set up your profile so sponsors and Program Ops can start matching you to projects."
      />

      <Card className={styles.section}>
        <Title3>About you</Title3>
        <div className={styles.photoRow} style={{ marginTop: tokens.spacingVerticalS }}>
          <AvatarEditorDialog
            trigger={
              <Avatar
                size={64}
                name="Add a photo"
                image={avatarUrl ? { src: avatarUrl } : undefined}
                style={{ cursor: 'pointer' }}
                aria-label="Add a photo"
              />
            }
          />
          <Caption1>Add a photo so sponsors and Program Ops recognize you.</Caption1>
        </div>
        <Field label="Bio" style={{ marginTop: tokens.spacingVerticalS }}>
          <Textarea value={bio} onChange={(_, data) => setBio(data.value)} resize="vertical" placeholder="A couple sentences about you" />
        </Field>
        <div className={styles.fieldGrid} style={{ marginTop: tokens.spacingVerticalS }}>
          <Field label="Location">
            <Input value={location} onChange={(_, data) => setLocation(data.value)} placeholder="City, State" />
          </Field>
          <Field label="Availability">
            <Select value={availability} onChange={(_, data) => setAvailability(data.value as Availability)}>
              <option value="PartTime">Part-time</option>
              <option value="FullTime">Full-time</option>
            </Select>
          </Field>
          <Field label="Graduation date">
            <Input type="date" value={graduationDate} onChange={(_, data) => setGraduationDate(data.value)} />
          </Field>
          <Field label="School">
            <Input value={school} onChange={(_, data) => setSchool(data.value)} />
          </Field>
          <Field label="Degree">
            <Input value={degree} onChange={(_, data) => setDegree(data.value)} />
          </Field>
          <Field label="GPA">
            <Input type="number" min={0} max={4} step={0.01} value={gpa} onChange={(_, data) => setGpa(data.value)} />
          </Field>
          <Field label="LinkedIn URL">
            <Input value={linkedInUrl} onChange={(_, data) => setLinkedInUrl(data.value)} />
          </Field>
          <Field label="Portfolio URL">
            <Input value={portfolioUrl} onChange={(_, data) => setPortfolioUrl(data.value)} />
          </Field>
        </div>
      </Card>

      <Card className={styles.section}>
        <Title3>Your skills</Title3>
        <Body1>Pick the skills you have — sponsors and the matching engine use these to find you good-fit projects.</Body1>
        <div style={{ marginTop: tokens.spacingVerticalM }}>
          <SkillPicker selectedSkillIds={skillIds} onChange={setSkillIds} />
        </div>
      </Card>

      <Button appearance="primary" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
        {createMutation.isPending ? 'Creating your profile...' : 'Create my profile'}
      </Button>
      {createMutation.isError && (
        <Body1 style={{ display: 'block', marginTop: tokens.spacingVerticalS }}>
          {(createMutation.error as Error).message}
        </Body1>
      )}
    </>
  );
}
