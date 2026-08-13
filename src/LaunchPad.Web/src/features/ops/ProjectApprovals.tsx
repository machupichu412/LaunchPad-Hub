import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Body1, Button, Card, Caption1, Input, Spinner, Title3, makeStyles, tokens } from '@fluentui/react-components';
import { CheckmarkRegular, DismissRegular } from '@fluentui/react-icons';
import { approveProject, getPendingApprovalProjects, rejectProject } from '../../api/projects';
import { PageHeader } from '../../components/PageHeader';

// Demo-only: same single-cohort simplification used elsewhere until real cohort
// selection exists.
const DEMO_COHORT_ID = 1;

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalS,
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: tokens.spacingHorizontalM,
  },
  actions: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'flex-end',
    gap: tokens.spacingHorizontalXS,
    flexShrink: 0,
    width: '260px',
  },
  buttonRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
  },
});

// Ops's half of the project approval workflow: PendingOps -> Approved/Rejected.
// Mirrors the Assignment two-stage flow's ApprovalQueue.tsx in shape.
export function ProjectApprovals() {
  const styles = useStyles();
  const queryClient = useQueryClient();
  const [reasons, setReasons] = useState<Record<number, string>>({});

  const { data: projects, isLoading, isError, error } = useQuery({
    queryKey: ['projects', 'pending-approval', DEMO_COHORT_ID],
    queryFn: () => getPendingApprovalProjects(DEMO_COHORT_ID),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['projects', 'pending-approval', DEMO_COHORT_ID] });
  const approveMutation = useMutation({ mutationFn: approveProject, onSuccess: invalidate });
  const rejectMutation = useMutation({
    mutationFn: ({ projectId, reason }: { projectId: number; reason: string }) => rejectProject(projectId, { reason }),
    onSuccess: invalidate,
  });

  return (
    <>
      <PageHeader
        title="Project Approvals"
        subtitle="Review projects sponsors have submitted. Approving makes a project visible to candidates on the marketplace."
      />

      {isLoading && <Spinner label="Loading the queue..." />}
      {isError && <Body1>Failed to load the queue: {(error as Error).message}</Body1>}
      {projects && projects.length === 0 && <Body1>Nothing pending approval right now.</Body1>}

      <div>
        {projects?.map((project) => (
          <Card key={project.projectId} className={styles.card}>
            <div>
              <Title3>{project.name}</Title3>
              <Caption1 style={{ display: 'block' }}>
                {project.sponsorName} &middot; {project.availabilityNeeded === 'FullTime' ? 'Full-time' : 'Part-time'}
              </Caption1>
              {project.description && <Body1>{project.description}</Body1>}
            </div>
            <div className={styles.actions}>
              <Input
                placeholder="Reason (required to reject)"
                value={reasons[project.projectId] ?? ''}
                onChange={(_, data) => setReasons((prev) => ({ ...prev, [project.projectId]: data.value }))}
              />
              <div className={styles.buttonRow}>
                <Button
                  icon={<DismissRegular />}
                  disabled={rejectMutation.isPending || approveMutation.isPending || !(reasons[project.projectId] ?? '').trim()}
                  onClick={() => rejectMutation.mutate({ projectId: project.projectId, reason: reasons[project.projectId] ?? '' })}
                >
                  Reject
                </Button>
                <Button
                  appearance="primary"
                  icon={<CheckmarkRegular />}
                  disabled={rejectMutation.isPending || approveMutation.isPending}
                  onClick={() => approveMutation.mutate(project.projectId)}
                >
                  Approve
                </Button>
              </div>
            </div>
          </Card>
        ))}
      </div>
      {approveMutation.isError && <Body1>Failed to approve: {(approveMutation.error as Error).message}</Body1>}
      {rejectMutation.isError && <Body1>Failed to reject: {(rejectMutation.error as Error).message}</Body1>}
    </>
  );
}
