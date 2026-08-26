import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Avatar,
  Body1,
  Button,
  Card,
  Field,
  Input,
  Select,
  Spinner,
  Textarea,
  Title3,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { getMyCandidateProfile, updateMyCandidateProfile } from '../../api/candidates';
import { PageHeader } from '../../components/PageHeader';
import { SkillTagPicker } from '../../components/SkillTagPicker';
import { useSurfaceStyles } from '../../theme/surfaces';
import type { Availability } from '../../api/types';

const useStyles = makeStyles({
  layout: {
    display: 'grid',
    gridTemplateColumns: '280px 1fr',
    gap: tokens.spacingHorizontalXL,
    alignItems: 'start',
  },
  summaryCard: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalS,
    textAlign: 'center',
  },
  section: {
    padding: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalL,
  },
  fieldGrid: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: tokens.spacingHorizontalM,
  },
});

// Candidate's own profile: resume upload (SAS-issued — not yet built) remains
// out of scope, same as before this rewrite.
export function MyProfile() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const queryClient = useQueryClient();
  const { data: profile, isLoading, isError, error } = useQuery({
    queryKey: ['candidates', 'me'],
    queryFn: getMyCandidateProfile,
  });

  const [location, setLocation] = useState('');
  const [availability, setAvailability] = useState<Availability>('PartTime');
  const [skillNames, setSkillNames] = useState<string[]>([]);
  const [linkedInUrl, setLinkedInUrl] = useState('');
  const [portfolioUrl, setPortfolioUrl] = useState('');
  const [bio, setBio] = useState('');
  const [school, setSchool] = useState('');
  const [degree, setDegree] = useState('');
  const [gpa, setGpa] = useState('');

  useEffect(() => {
    if (!profile) return;
    setLocation(profile.location ?? '');
    setAvailability(profile.availability);
    setSkillNames(profile.skills);
    setLinkedInUrl(profile.linkedInUrl ?? '');
    setPortfolioUrl(profile.portfolioUrl ?? '');
    setBio(profile.bio ?? '');
    setSchool(profile.school ?? '');
    setDegree(profile.degree ?? '');
    setGpa(profile.gpa != null ? String(profile.gpa) : '');
  }, [profile]);

  const updateMutation = useMutation({
    mutationFn: () =>
      updateMyCandidateProfile({
        location: location.trim().length > 0 ? location.trim() : null,
        availability,
        graduationDate: profile?.graduationDate ?? null,
        linkedInUrl: linkedInUrl.trim().length > 0 ? linkedInUrl.trim() : null,
        portfolioUrl: portfolioUrl.trim().length > 0 ? portfolioUrl.trim() : null,
        bio: bio.trim().length > 0 ? bio.trim() : null,
        school: school.trim().length > 0 ? school.trim() : null,
        degree: degree.trim().length > 0 ? degree.trim() : null,
        gpa: gpa.trim().length > 0 ? Number(gpa) : null,
        skillNames,
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['candidates', 'me'] }),
  });

  if (isLoading) return <Spinner label="Loading your profile..." />;
  if (isError) return <Body1>Failed to load your profile: {(error as Error).message}</Body1>;
  if (!profile) return <Body1>No candidate profile found for your account yet — contact Program Ops.</Body1>;

  return (
    <>
      <PageHeader title="My Profile" />

      <div className={styles.layout}>
        <Card className={mergeClasses(styles.summaryCard, surfaces.card)}>
          <Avatar name={profile.displayName} size={72} />
          <Title3>{profile.displayName}</Title3>
          {profile.email && <Body1>{profile.email}</Body1>}
          <Body1>Outcome: {profile.outcome}</Body1>
        </Card>

        <div>
          <Card className={mergeClasses(styles.section, surfaces.card)}>
            <Title3>Basic Info</Title3>
            <Field label="Bio" style={{ marginTop: tokens.spacingVerticalS }}>
              <Textarea value={bio} onChange={(_, data) => setBio(data.value)} resize="vertical" />
            </Field>
            <div className={styles.fieldGrid} style={{ marginTop: tokens.spacingVerticalS }}>
              <Field label="Location">
                <Input value={location} onChange={(_, data) => setLocation(data.value)} />
              </Field>
              <Field label="Availability">
                <Select value={availability} onChange={(_, data) => setAvailability(data.value as Availability)}>
                  <option value="PartTime">Part-time</option>
                  <option value="FullTime">Full-time</option>
                </Select>
              </Field>
              <Field label="LinkedIn URL">
                <Input value={linkedInUrl} onChange={(_, data) => setLinkedInUrl(data.value)} />
              </Field>
              <Field label="Portfolio URL">
                <Input value={portfolioUrl} onChange={(_, data) => setPortfolioUrl(data.value)} />
              </Field>
            </div>
          </Card>

          <Card className={mergeClasses(styles.section, surfaces.card)}>
            <Title3>Education</Title3>
            <div className={styles.fieldGrid} style={{ marginTop: tokens.spacingVerticalS }}>
              <Field label="School">
                <Input value={school} onChange={(_, data) => setSchool(data.value)} />
              </Field>
              <Field label="Degree">
                <Input value={degree} onChange={(_, data) => setDegree(data.value)} />
              </Field>
              <Field label="GPA">
                <Input type="number" min={0} max={4} step={0.01} value={gpa} onChange={(_, data) => setGpa(data.value)} />
              </Field>
            </div>
          </Card>

          <Card className={mergeClasses(styles.section, surfaces.card)}>
            <Title3>Skills</Title3>
            <Field label="Skills" style={{ marginTop: tokens.spacingVerticalS }}>
              <SkillTagPicker selectedNames={skillNames} onChange={setSkillNames} allowCreate />
            </Field>
          </Card>

          <Button
            appearance="primary"
            disabled={updateMutation.isPending}
            onClick={() => updateMutation.mutate()}
          >
            {updateMutation.isPending ? 'Saving...' : 'Save profile'}
          </Button>
          {updateMutation.isError && (
            <Body1>Failed to save: {(updateMutation.error as Error).message}</Body1>
          )}
          {updateMutation.isSuccess && <Body1>Saved.</Body1>}
        </div>
      </div>
    </>
  );
}
