import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Badge, Body1, Spinner, tokens } from '@fluentui/react-components';
import { getProject, updateProject } from '../api/projects';
import { PageHeader } from './PageHeader';
import { ProjectForm, type ProjectFormValues } from './ProjectForm';

const emptyValues: ProjectFormValues = {
  name: '',
  description: '',
  availabilityNeeded: 'PartTime',
  requiredSkillNames: [],
  preferredSkillNames: [],
};

/**
 * The one edit-a-project screen, mounted at two routes: the Sponsor's own
 * /projects/:id/edit and Ops's /ops/projects/:id. Deliberately role-agnostic —
 * GET/PUT /api/projects/{id} already authorize this via ManageOwnProject (a
 * Sponsor editing another sponsor's project gets a 403; Ops bypasses ownership
 * entirely, see OwnsProjectHandler), so this component doesn't need to know or
 * care which role rendered it.
 */
export function ProjectEditor() {
  const { id } = useParams<{ id: string }>();
  const projectId = Number(id);
  const queryClient = useQueryClient();

  const { data: project, isLoading, isError, error } = useQuery({
    queryKey: ['projects', projectId],
    queryFn: () => getProject(projectId),
    enabled: !Number.isNaN(projectId),
  });

  const [values, setValues] = useState<ProjectFormValues>(emptyValues);

  useEffect(() => {
    if (!project) return;
    setValues({
      name: project.name,
      description: project.description ?? '',
      availabilityNeeded: project.availabilityNeeded,
      requiredSkillNames: project.requiredSkills.filter((s) => s.isRequired).map((s) => s.skillName),
      preferredSkillNames: project.requiredSkills.filter((s) => !s.isRequired).map((s) => s.skillName),
    });
  }, [project]);

  const updateMutation = useMutation({
    mutationFn: () =>
      updateProject(projectId, {
        name: values.name,
        description: values.description.trim().length > 0 ? values.description.trim() : null,
        availabilityNeeded: values.availabilityNeeded,
        startDate: project?.startDate ?? null,
        endDate: project?.endDate ?? null,
        requiredSkillNames: values.requiredSkillNames,
        preferredSkillNames: values.preferredSkillNames,
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', projectId] }),
  });

  if (isLoading) return <Spinner label="Loading project..." />;
  if (isError) return <Body1>Failed to load project: {(error as Error).message}</Body1>;
  if (!project) return null;

  return (
    <>
      <PageHeader
        title={project.name}
        subtitle={
          <>
            {project.sponsorName} <Badge appearance="tint" style={{ marginLeft: tokens.spacingHorizontalXS }}>{project.approvalStatus}</Badge>{' '}
            <Badge appearance="tint" color={project.status === 'Open' ? 'success' : 'informative'}>{project.status}</Badge>
          </>
        }
      />

      <ProjectForm
        values={values}
        onChange={setValues}
        onSubmit={() => updateMutation.mutate()}
        isSubmitting={updateMutation.isPending}
        submitLabel="Save changes"
        errorMessage={updateMutation.isError ? `Failed to save: ${(updateMutation.error as Error).message}` : undefined}
        successMessage={updateMutation.isSuccess ? 'Saved.' : undefined}
      />
    </>
  );
}
