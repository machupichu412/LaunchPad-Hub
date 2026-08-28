import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Badge,
  Body1,
  Button,
  Card,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Select,
  Spinner,
  Subtitle2,
  Textarea,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { cancelProject, getProject, updateDeliveryStage, updateProject } from '../api/projects';
import { AssignedCandidatesSection } from './AssignedCandidatesSection';
import { CandidateGallery } from './CandidateGallery';
import { PageHeader } from './PageHeader';
import { ProjectForm, type ProjectFormValues } from './ProjectForm';
import { useSurfaceStyles } from '../theme/surfaces';
import {
  deliveryStageLabel,
  deliveryStageOrder,
  projectApprovalStatusLabel,
  projectStatusLabel,
} from '../utils/statusLabels';
import type { ProjectDeliveryStage } from '../api/types';

const useStyles = makeStyles({
  stageCard: {
    padding: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalXL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  stageRow: {
    display: 'flex',
    alignItems: 'flex-end',
    gap: tokens.spacingHorizontalM,
    flexWrap: 'wrap',
  },
});

const emptyValues: ProjectFormValues = {
  name: '',
  description: '',
  availabilityNeeded: 'PartTime',
  maxCandidates: 1,
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
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
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
      maxCandidates: project.maxCandidates,
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
        maxCandidates: values.maxCandidates,
        requiredSkillNames: values.requiredSkillNames,
        preferredSkillNames: values.preferredSkillNames,
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', projectId] }),
  });

  const [stageValue, setStageValue] = useState<ProjectDeliveryStage>('NotStarted');
  const [stageReason, setStageReason] = useState('');

  useEffect(() => {
    if (project) setStageValue(project.deliveryStage);
  }, [project]);

  const stageMutation = useMutation({
    mutationFn: () => updateDeliveryStage(projectId, { stage: stageValue, reason: stageReason.trim() || null }),
    onSuccess: () => {
      setStageReason('');
      queryClient.invalidateQueries({ queryKey: ['projects', projectId] });
    },
  });

  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const cancelMutation = useMutation({
    mutationFn: () => cancelProject(projectId, { reason: cancelReason }),
    onSuccess: () => {
      setCancelOpen(false);
      setCancelReason('');
      queryClient.invalidateQueries({ queryKey: ['projects', projectId] });
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'assigned-candidates'] });
    },
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
            {project.sponsorName} <Badge appearance="tint" style={{ marginLeft: tokens.spacingHorizontalXS }}>{projectApprovalStatusLabel(project.approvalStatus)}</Badge>{' '}
            <Badge appearance="tint" color={project.status === 'Open' ? 'success' : 'informative'}>{projectStatusLabel(project.status)}</Badge>
          </>
        }
        actions={
          project.status !== 'Cancelled' && (
            <Button appearance="secondary" onClick={() => setCancelOpen(true)}>
              Cancel project
            </Button>
          )
        }
      />

      <Card className={mergeClasses(styles.stageCard, surfaces.card)}>
        <Subtitle2>Delivery stage</Subtitle2>
        <Body1>
          Feeds the Executive dashboard's delivery KPIs. Current stage:{' '}
          <Badge appearance="tint">{deliveryStageLabel(project.deliveryStage)}</Badge>
        </Body1>
        <div className={styles.stageRow}>
          <Field label="Set stage to">
            <Select value={stageValue} onChange={(_, data) => setStageValue(data.value as ProjectDeliveryStage)}>
              {deliveryStageOrder.map((stage) => (
                <option key={stage} value={stage}>
                  {deliveryStageLabel(stage)}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Reason (optional)">
            <Textarea
              value={stageReason}
              onChange={(_, data) => setStageReason(data.value)}
              resize="vertical"
              placeholder="What changed?"
            />
          </Field>
          <Button
            appearance="primary"
            disabled={stageValue === project.deliveryStage || stageMutation.isPending}
            onClick={() => stageMutation.mutate()}
          >
            {stageMutation.isPending ? 'Updating...' : 'Update stage'}
          </Button>
        </div>
        {stageMutation.isError && <Body1>Failed to update: {(stageMutation.error as Error).message}</Body1>}
        {stageMutation.isSuccess && <Body1>Stage updated.</Body1>}
      </Card>

      <ProjectForm
        values={values}
        onChange={setValues}
        onSubmit={() => updateMutation.mutate()}
        isSubmitting={updateMutation.isPending}
        submitLabel="Save changes"
        errorMessage={updateMutation.isError ? `Failed to save: ${(updateMutation.error as Error).message}` : undefined}
        successMessage={updateMutation.isSuccess ? 'Saved.' : undefined}
      />

      <AssignedCandidatesSection projectId={projectId} />

      <CandidateGallery
        projectId={projectId}
        projectName={project.name}
        isApproved={project.approvalStatus === 'Approved'}
        spotsRemaining={project.spotsRemaining}
      />

      <Dialog open={cancelOpen} onOpenChange={(_, data) => setCancelOpen(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Cancel this project?</DialogTitle>
            <DialogContent>
              <Body1 style={{ display: 'block', marginBottom: tokens.spacingVerticalS }}>
                This withdraws every candidate currently assigned or pending on this project, freeing them back into
                the open pool. This can't be undone.
              </Body1>
              <Field label="Reason">
                <Textarea
                  value={cancelReason}
                  onChange={(_, data) => setCancelReason(data.value)}
                  resize="vertical"
                  placeholder="Why is this project being cancelled?"
                />
              </Field>
              {cancelMutation.isError && (
                <Body1 style={{ display: 'block', marginTop: tokens.spacingVerticalS }}>
                  Failed to cancel: {(cancelMutation.error as Error).message}
                </Body1>
              )}
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setCancelOpen(false)} disabled={cancelMutation.isPending}>
                Never mind
              </Button>
              <Button
                appearance="primary"
                disabled={cancelReason.trim().length === 0 || cancelMutation.isPending}
                onClick={() => cancelMutation.mutate()}
              >
                {cancelMutation.isPending ? 'Cancelling...' : 'Cancel project'}
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </>
  );
}
