import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Body1,
  Button,
  Card,
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
  Title2,
  Title3,
} from '@fluentui/react-components';
import { createProject, getMyProjects } from '../../api/projects';
import type { Availability } from '../../api/types';

// Demo-only: the local seed data creates exactly one cohort, which is always
// CohortId 1. Once cohort selection exists (Phase 2), this becomes a dropdown.
const DEMO_COHORT_ID = 1;

// Sponsor's own projects (ManageOwnProject policy on the API) — create/edit
// projects, review top-3 matches, recommend a candidate.
export function MyProjects() {
  const queryClient = useQueryClient();
  const { data: projects, isLoading, isError, error } = useQuery({
    queryKey: ['projects', 'mine'],
    queryFn: getMyProjects,
  });

  const [name, setName] = useState('');
  const [availability, setAvailability] = useState<Availability>('PartTime');

  const createMutation = useMutation({
    mutationFn: () =>
      createProject({
        cohortId: DEMO_COHORT_ID,
        name,
        description: null,
        availabilityNeeded: availability,
        startDate: null,
        endDate: null,
        requiredSkillNames: [],
        preferredSkillNames: [],
      }),
    onSuccess: () => {
      setName('');
      queryClient.invalidateQueries({ queryKey: ['projects', 'mine'] });
    },
  });

  return (
    <>
      <Title2>My Projects</Title2>

      <Card style={{ marginTop: '1rem', marginBottom: '1.5rem', padding: '1rem', maxWidth: 420 }}>
        <Title3>New project</Title3>
        <Field label="Name">
          <Input value={name} onChange={(_, data) => setName(data.value)} />
        </Field>
        <Field label="Availability needed">
          <Select value={availability} onChange={(_, data) => setAvailability(data.value as Availability)}>
            <option value="PartTime">Part-time</option>
            <option value="FullTime">Full-time</option>
          </Select>
        </Field>
        <Button
          appearance="primary"
          style={{ marginTop: '0.75rem' }}
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
            </TableRow>
          </TableHeader>
          <TableBody>
            {projects.map((project) => (
              <TableRow key={project.projectId}>
                <TableCell>{project.name}</TableCell>
                <TableCell>{project.availabilityNeeded}</TableCell>
                <TableCell>{project.approvalStatus}</TableCell>
                <TableCell>{project.status}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </>
  );
}
