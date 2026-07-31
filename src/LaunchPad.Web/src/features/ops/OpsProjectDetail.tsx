import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Badge,
  Body1,
  Button,
  Field,
  Input,
  Select,
  Spinner,
  Textarea,
  Title2,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { getProject, updateProject } from '../../api/projects';
import type { Availability } from '../../api/types';

const useStyles = makeStyles({
  form: {
    maxWidth: '560px',
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    marginTop: tokens.spacingVerticalM,
  },
});

// Ops "editing any project" is already fully wired at the API layer — Update
// already lets Ops bypass Sponsor ownership (see OwnsProjectHandler). This page
// is the only missing piece: a frontend surface for that existing capability.
export function OpsProjectDetail() {
  const styles = useStyles();
  const { id } = useParams<{ id: string }>();
  const projectId = Number(id);
  const queryClient = useQueryClient();

  const { data: project, isLoading, isError, error } = useQuery({
    queryKey: ['projects', projectId],
    queryFn: () => getProject(projectId),
    enabled: !Number.isNaN(projectId),
  });

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [availability, setAvailability] = useState<Availability>('PartTime');
  const [requiredSkills, setRequiredSkills] = useState('');
  const [preferredSkills, setPreferredSkills] = useState('');

  useEffect(() => {
    if (!project) return;
    setName(project.name);
    setDescription(project.description ?? '');
    setAvailability(project.availabilityNeeded);
    setRequiredSkills(project.requiredSkills.filter((s) => s.isRequired).map((s) => s.skillName).join(', '));
    setPreferredSkills(project.requiredSkills.filter((s) => !s.isRequired).map((s) => s.skillName).join(', '));
  }, [project]);

  const updateMutation = useMutation({
    mutationFn: () =>
      updateProject(projectId, {
        name,
        description: description.trim().length > 0 ? description.trim() : null,
        availabilityNeeded: availability,
        startDate: project?.startDate ?? null,
        endDate: project?.endDate ?? null,
        requiredSkillNames: requiredSkills.split(',').map((s) => s.trim()).filter((s) => s.length > 0),
        preferredSkillNames: preferredSkills.split(',').map((s) => s.trim()).filter((s) => s.length > 0),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', projectId] }),
  });

  if (isLoading) return <Spinner label="Loading project..." />;
  if (isError) return <Body1>Failed to load project: {(error as Error).message}</Body1>;
  if (!project) return null;

  return (
    <>
      <Title2>{project.name}</Title2>
      <Body1>
        {project.sponsorName} <Badge appearance="tint" style={{ marginLeft: tokens.spacingHorizontalXS }}>{project.approvalStatus}</Badge>{' '}
        <Badge appearance="tint" color={project.status === 'Open' ? 'success' : 'informative'}>{project.status}</Badge>
      </Body1>

      <div className={styles.form}>
        <Field label="Name">
          <Input value={name} onChange={(_, data) => setName(data.value)} />
        </Field>
        <Field label="Description">
          <Textarea value={description} onChange={(_, data) => setDescription(data.value)} resize="vertical" />
        </Field>
        <Field label="Availability needed">
          <Select value={availability} onChange={(_, data) => setAvailability(data.value as Availability)}>
            <option value="PartTime">Part-time</option>
            <option value="FullTime">Full-time</option>
          </Select>
        </Field>
        <Field label="Required skills (comma-separated)">
          <Input value={requiredSkills} onChange={(_, data) => setRequiredSkills(data.value)} />
        </Field>
        <Field label="Preferred skills (comma-separated)">
          <Input value={preferredSkills} onChange={(_, data) => setPreferredSkills(data.value)} />
        </Field>

        <Button
          appearance="primary"
          disabled={name.trim().length === 0 || updateMutation.isPending}
          onClick={() => updateMutation.mutate()}
        >
          {updateMutation.isPending ? 'Saving...' : 'Save changes'}
        </Button>
        {updateMutation.isError && <Body1>Failed to save: {(updateMutation.error as Error).message}</Body1>}
        {updateMutation.isSuccess && <Body1>Saved.</Body1>}
      </div>
    </>
  );
}
