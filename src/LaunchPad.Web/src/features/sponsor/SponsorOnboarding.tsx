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
  Spinner,
  Title3,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { createMySponsorProfile, getMySponsorProfile } from '../../api/sponsors';
import { AvatarEditorDialog } from '../../components/AvatarEditorDialog';
import { PageHeader } from '../../components/PageHeader';
import { useMyAvatarUrl } from '../../auth/useMyAvatarUrl';

const useStyles = makeStyles({
  section: {
    padding: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalL,
    maxWidth: '640px',
  },
  photoRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalM,
  },
});

// Self-service onboarding for a Sponsor-role user with no Sponsor row yet (see
// RequireSponsorProfile — this page is where it redirects). Mirrors
// candidate/Onboarding.tsx; simpler since Sponsor has no cohort/skills to set.
export function SponsorOnboarding() {
  const styles = useStyles();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: profile, isLoading } = useQuery({
    queryKey: ['sponsors', 'me'],
    queryFn: getMySponsorProfile,
  });
  const { url: avatarUrl } = useMyAvatarUrl();

  const [organization, setOrganization] = useState('');
  const [title, setTitle] = useState('');

  const createMutation = useMutation({
    mutationFn: () =>
      createMySponsorProfile({
        organization: organization.trim().length > 0 ? organization.trim() : null,
        title: title.trim().length > 0 ? title.trim() : null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sponsors', 'me'] });
      navigate('/projects');
    },
  });

  if (isLoading) return <Spinner label="Checking your profile..." />;
  // Already onboarded — nothing to do here, this page is create-only.
  if (profile) return <Navigate to="/projects" replace />;

  return (
    <>
      <PageHeader
        title="Welcome to LaunchPad"
        subtitle="Let's set up your sponsor profile so you can post projects and review candidates."
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
          <Caption1>Add a photo so candidates and Program Ops recognize you.</Caption1>
        </div>
        <div style={{ marginTop: tokens.spacingVerticalS }}>
          <Field label="Organization">
            <Input value={organization} onChange={(_, data) => setOrganization(data.value)} placeholder="Company or team name" />
          </Field>
          <Field label="Title" style={{ marginTop: tokens.spacingVerticalS }}>
            <Input value={title} onChange={(_, data) => setTitle(data.value)} placeholder="Your role" />
          </Field>
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
