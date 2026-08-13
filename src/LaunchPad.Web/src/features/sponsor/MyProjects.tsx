import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Badge,
  Body1,
  Button,
  Caption1,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  tokens,
} from '@fluentui/react-components';
import { createProject, getMyProjects, submitProject } from '../../api/projects';
import { PageHeader } from '../../components/PageHeader';
import { ProjectForm, type ProjectFormValues } from '../../components/ProjectForm';

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

  const [formValues, setFormValues] = useState<ProjectFormValues>({
    name: '',
    description: '',
    availabilityNeeded: 'PartTime',
    requiredSkillNames: [],
    preferredSkillNames: [],
  });

  const createMutation = useMutation({
    mutationFn: () =>
      createProject({
        cohortId: DEMO_COHORT_ID,
        name: formValues.name,
        description: formValues.description.trim().length > 0 ? formValues.description.trim() : null,
        availabilityNeeded: formValues.availabilityNeeded,
        startDate: null,
        endDate: null,
        requiredSkillNames: formValues.requiredSkillNames,
        preferredSkillNames: formValues.preferredSkillNames,
      }),
    onSuccess: () => {
      setFormValues({ name: '', description: '', availabilityNeeded: 'PartTime', requiredSkillNames: [], preferredSkillNames: [] });
      queryClient.invalidateQueries({ queryKey: ['projects', 'mine'] });
    },
  });

  const submitMutation = useMutation({
    mutationFn: submitProject,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', 'mine'] }),
  });

  return (
    <>
      <PageHeader title="My Projects" />

      <div style={{ marginBottom: tokens.spacingVerticalXL }}>
        <ProjectForm
          heading="New project"
          values={formValues}
          onChange={setFormValues}
          onSubmit={() => createMutation.mutate()}
          isSubmitting={createMutation.isPending}
          submitLabel="Create project"
          errorMessage={createMutation.isError ? `Failed to create project: ${(createMutation.error as Error).message}` : undefined}
        />
      </div>

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
                  <Button
                    size="small"
                    onClick={() => navigate(`/projects/${project.projectId}/edit`)}
                    style={{ marginRight: tokens.spacingHorizontalXS }}
                  >
                    Edit
                  </Button>
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
