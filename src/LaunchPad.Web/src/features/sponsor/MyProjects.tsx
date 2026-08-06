import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Badge,
  Body1,
  Button,
  Card,
  Caption1,
  Field,
  Input,
  Select,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Textarea,
  Title2,
  Title3,
  tokens,
} from '@fluentui/react-components';
import { createProject, getMyProjects, submitProject } from '../../api/projects';
import type { Availability } from '../../api/types';

// Demo-only: the local seed data creates exactly one cohort, which is always
// CohortId 1. Once cohort selection exists (Phase 2), this becomes a dropdown.
const DEMO_COHORT_ID = 1;

// Sponsor's own projects (ManageOwnProject policy on the API) — create/edit
// projects, review top-3 matches, recommend a candidate.
export function MyProjects() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { data: projects, isLoading, isError, error } = useQuery({
    queryKey: ['projects', 'mine'],
    queryFn: getMyProjects,
  });

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [availability, setAvailability] = useState<Availability>('PartTime');
  const [requiredSkillsInput, setRequiredSkillsInput] = useState('');
  const [preferredSkillsInput, setPreferredSkillsInput] = useState('');

  const parseSkillNames = (input: string) =>
    input
      .split(',')
      .map((s) => s.trim())
      .filter((s) => s.length > 0);

  const createMutation = useMutation({
    mutationFn: () =>
      createProject({
        cohortId: DEMO_COHORT_ID,
        name,
        description: description.trim().length > 0 ? description.trim() : null,
        availabilityNeeded: availability,
        startDate: null,
        endDate: null,
        requiredSkillNames: parseSkillNames(requiredSkillsInput),
        preferredSkillNames: parseSkillNames(preferredSkillsInput),
      }),
    onSuccess: () => {
      setName('');
      setDescription('');
      setRequiredSkillsInput('');
      setPreferredSkillsInput('');
      queryClient.invalidateQueries({ queryKey: ['projects', 'mine'] });
    },
  });

  const submitMutation = useMutation({
    mutationFn: submitProject,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', 'mine'] }),
  });

  return (
    <>
      <Title2>My Projects</Title2>

      <Card
        style={{
          marginTop: tokens.spacingVerticalM,
          marginBottom: tokens.spacingVerticalXL,
          padding: tokens.spacingVerticalM,
          maxWidth: '520px',
        }}
      >
        <Title3>New project</Title3>
        <Field label="Name">
          <Input value={name} onChange={(_, data) => setName(data.value)} />
        </Field>
        <Field label="Description" style={{ marginTop: tokens.spacingVerticalS }}>
          <Textarea
            value={description}
            onChange={(_, data) => setDescription(data.value)}
            resize="vertical"
            placeholder="What will the candidate be working on?"
          />
        </Field>
        <Field label="Availability needed" style={{ marginTop: tokens.spacingVerticalS }}>
          <Select value={availability} onChange={(_, data) => setAvailability(data.value as Availability)}>
            <option value="PartTime">Part-time</option>
            <option value="FullTime">Full-time</option>
          </Select>
        </Field>
        <Field
          label="Required skills"
          hint="Comma-separated — candidates must have all of these to be matched."
          style={{ marginTop: tokens.spacingVerticalS }}
        >
          <Input
            value={requiredSkillsInput}
            onChange={(_, data) => setRequiredSkillsInput(data.value)}
            placeholder="e.g. React, TypeScript"
          />
        </Field>
        <Field
          label="Preferred skills"
          hint="Comma-separated — nice to have, not required to match."
          style={{ marginTop: tokens.spacingVerticalS }}
        >
          <Input
            value={preferredSkillsInput}
            onChange={(_, data) => setPreferredSkillsInput(data.value)}
            placeholder="e.g. Figma, Power BI"
          />
        </Field>
        <Button
          appearance="primary"
          style={{ marginTop: tokens.spacingVerticalM }}
          disabled={name.trim().length === 0 || createMutation.isPending}
          onClick={() => createMutation.mutate()}
        >
          {createMutation.isPending ? 'Creating...' : 'Create project'}
        </Button>
        {createMutation.isError && <Body1>Failed to create project: {(createMutation.error as Error).message}</Body1>}
      </Card>

      {isLoading && <Spinner label="Loading your projects..." />}
      {isError && <Body1>Failed to load your projects: {(error as Error).message}</Body1>}
      {projects && projects.length === 0 && <Body1>You haven't created any projects yet.</Body1>}
      {projects && projects.length > 0 && (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Name</TableHeaderCell>
              <TableHeaderCell>Availability</TableHeaderCell>
              <TableHeaderCell>Approval</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell />
            </TableRow>
          </TableHeader>
          <TableBody>
            {projects.map((project) => (
              <TableRow key={project.projectId}>
                <TableCell>{project.name}</TableCell>
                <TableCell>{project.availabilityNeeded}</TableCell>
                <TableCell>
                  <Badge appearance="tint" color={project.approvalStatus === 'Rejected' ? 'danger' : 'informative'}>
                    {project.approvalStatus}
                  </Badge>
                  {project.approvalStatus === 'Rejected' && project.rejectionReason && (
                    <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalXXS }}>
                      {project.rejectionReason}
                    </Caption1>
                  )}
                </TableCell>
                <TableCell>{project.status}</TableCell>
                <TableCell>
                  {(project.approvalStatus === 'Draft' || project.approvalStatus === 'Rejected') && (
                    <Button
                      size="small"
                      appearance="primary"
                      disabled={submitMutation.isPending}
                      onClick={() => submitMutation.mutate(project.projectId)}
                      style={{ marginRight: tokens.spacingHorizontalXS }}
                    >
                      Submit for approval
                    </Button>
                  )}
                  <Button size="small" onClick={() => navigate(`/projects/${project.projectId}/matches`)}>
                    Review matches
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
      {submitMutation.isError && <Body1>Failed to submit for approval: {(submitMutation.error as Error).message}</Body1>}
    </>
  );
}
