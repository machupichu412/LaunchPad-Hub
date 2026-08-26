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
import { availabilityLabel, projectApprovalStatusLabel, projectStatusLabel } from '../../utils/statusLabels';

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

  const emptyFormValues: ProjectFormValues = {
    name: '',
    description: '',
    availabilityNeeded: 'PartTime',
    maxCandidates: 1,
    requiredSkillNames: [],
    preferredSkillNames: [],
  };
  const [formValues, setFormValues] = useState<ProjectFormValues>(emptyFormValues);

  const createMutation = useMutation({
    mutationFn: () =>
      createProject({
        cohortId: DEMO_COHORT_ID,
        name: formValues.name,
        description: formValues.description.trim().length > 0 ? formValues.description.trim() : null,
        availabilityNeeded: formValues.availabilityNeeded,
        startDate: null,
        endDate: null,
        maxCandidates: formValues.maxCandidates,
        requiredSkillNames: formValues.requiredSkillNames,
        preferredSkillNames: formValues.preferredSkillNames,
      }),
    onSuccess: () => {
      setFormValues(emptyFormValues);
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
                <TableCell>{availabilityLabel(project.availabilityNeeded)}</TableCell>
                <TableCell>
                  <Badge appearance="tint" color={project.approvalStatus === 'Rejected' ? 'danger' : 'informative'}>
                    {projectApprovalStatusLabel(project.approvalStatus)}
                  </Badge>
                  {project.approvalStatus === 'Rejected' && project.rejectionReason && (
                    <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalXXS }}>
                      {project.rejectionReason}
                    </Caption1>
                  )}
                </TableCell>
                <TableCell>{projectStatusLabel(project.status)}</TableCell>
                <TableCell>
                  <div
                    style={{
                      display: 'flex',
                      flexWrap: 'wrap',
                      gap: tokens.spacingHorizontalXS,
                    }}
                  >
                    {(project.approvalStatus === 'Draft' || project.approvalStatus === 'Rejected') && (
                      <Button
                        size="small"
                        appearance="primary"
                        disabled={submitMutation.isPending}
                        onClick={() => submitMutation.mutate(project.projectId)}
                      >
                        Submit for approval
                      </Button>
                    )}
                    <Button size="small" onClick={() => navigate(`/projects/${project.projectId}/edit`)}>
                      Edit
                    </Button>
                    <Button size="small" onClick={() => navigate(`/projects/${project.projectId}/matches`)}>
                      Review matches
                    </Button>
                  </div>
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
