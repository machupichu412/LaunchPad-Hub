import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Badge, Body1, Button, Card, Caption1, Input, Spinner, Textarea, Title3, makeStyles, tokens } from '@fluentui/react-components';
import { CheckmarkRegular, DismissRegular, SearchRegular } from '@fluentui/react-icons';
import { approveProject, getPendingApprovalProjects, getProjectsByCohort, rejectProject } from '../../api/projects';
import { PageHeader } from '../../components/PageHeader';
import { projectApprovalStatusLabel } from '../../utils/statusLabels';
import type { ProjectDto } from '../../api/types';

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
    alignItems: 'stretch',
    gap: tokens.spacingHorizontalXS,
    flexShrink: 0,
    width: '340px',
  },
  reasonInput: {
    width: '100%',
  },
  buttonRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    justifyContent: 'flex-end',
  },
  historySection: {
    marginTop: tokens.spacingVerticalXXL,
  },
  search: {
    width: '320px',
    marginBottom: tokens.spacingVerticalM,
  },
  historyCard: {
    padding: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalS,
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: tokens.spacingHorizontalM,
  },
});

const pendingQueryKey = ['projects', 'pending-approval', DEMO_COHORT_ID];
const cohortQueryKey = ['projects', 'cohort', DEMO_COHORT_ID];

function matchesSearch(project: ProjectDto, term: string): boolean {
  if (term.length === 0) return true;
  const haystack = `${project.name} ${project.description ?? ''} ${project.sponsorName}`.toLowerCase();
  return haystack.includes(term);
}

// Shared by both the pending queue and the decision-history section — approve/reject
// now work from any non-Draft status (see ProjectsController.Approve/Reject), so Ops
// can revisit a past call the same way they made the first one.
function ApprovalCard({ project, onDone }: { project: ProjectDto; onDone: () => void }) {
  const styles = useStyles();
  const [reason, setReason] = useState(project.rejectionReason ?? '');

  const approveMutation = useMutation({ mutationFn: approveProject, onSuccess: onDone });
  const rejectMutation = useMutation({
    mutationFn: (r: string) => rejectProject(project.projectId, { reason: r }),
    onSuccess: onDone,
  });

  const busy = approveMutation.isPending || rejectMutation.isPending;

  return (
    <Card className={styles.card}>
      <div>
        <Title3>{project.name}</Title3>
        <Caption1 style={{ display: 'block' }}>
          {project.sponsorName} &middot; {project.availabilityNeeded === 'FullTime' ? 'Full-time' : 'Part-time'}
        </Caption1>
        {project.approvalStatus !== 'PendingOps' && (
          <Badge
            appearance="tint"
            color={project.approvalStatus === 'Rejected' ? 'danger' : 'success'}
            style={{ marginTop: tokens.spacingVerticalXXS }}
          >
            {projectApprovalStatusLabel(project.approvalStatus)}
          </Badge>
        )}
        {project.description && <Body1 style={{ display: 'block', marginTop: tokens.spacingVerticalXS }}>{project.description}</Body1>}
      </div>
      <div className={styles.actions}>
        <Textarea
          className={styles.reasonInput}
          placeholder="Reason (required to reject)"
          resize="vertical"
          rows={3}
          value={reason}
          onChange={(_, data) => setReason(data.value)}
        />
        <div className={styles.buttonRow}>
          <Button
            icon={<DismissRegular />}
            disabled={busy || !reason.trim()}
            onClick={() => rejectMutation.mutate(reason)}
          >
            Reject
          </Button>
          <Button
            appearance="primary"
            icon={<CheckmarkRegular />}
            disabled={busy}
            onClick={() => approveMutation.mutate(project.projectId)}
          >
            Approve
          </Button>
        </div>
        {approveMutation.isError && <Body1>Failed to approve: {(approveMutation.error as Error).message}</Body1>}
        {rejectMutation.isError && <Body1>Failed to reject: {(rejectMutation.error as Error).message}</Body1>}
      </div>
    </Card>
  );
}

// Ops's half of the project approval workflow: PendingOps -> Approved/Rejected, plus
// a searchable history of already-decided projects so a past call can be revisited.
// Mirrors the Assignment two-stage flow's ApprovalQueue.tsx in shape.
export function ProjectApprovals() {
  const styles = useStyles();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');

  const { data: pending, isLoading, isError, error } = useQuery({
    queryKey: pendingQueryKey,
    queryFn: () => getPendingApprovalProjects(DEMO_COHORT_ID),
  });

  const { data: allProjects, isLoading: historyLoading, isError: historyIsError, error: historyError } = useQuery({
    queryKey: cohortQueryKey,
    queryFn: () => getProjectsByCohort(DEMO_COHORT_ID),
  });

  const onDecided = () => {
    queryClient.invalidateQueries({ queryKey: pendingQueryKey });
    queryClient.invalidateQueries({ queryKey: cohortQueryKey });
  };

  const decided = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (allProjects ?? [])
      .filter((p) => p.approvalStatus === 'Approved' || p.approvalStatus === 'Rejected')
      .filter((p) => matchesSearch(p, term));
  }, [allProjects, search]);

  return (
    <>
      <PageHeader
        title="Project Approvals"
        subtitle="Review projects sponsors have submitted. Approving makes a project visible to candidates on the marketplace."
      />

      {isLoading && <Spinner label="Loading the queue..." />}
      {isError && <Body1>Failed to load the queue: {(error as Error).message}</Body1>}
      {pending && pending.length === 0 && <Body1>Nothing pending approval right now.</Body1>}

      <div>
        {pending?.map((project) => (
          <ApprovalCard key={project.projectId} project={project} onDone={onDecided} />
        ))}
      </div>

      <div className={styles.historySection}>
        <Title3>Decision history</Title3>
        <Body1 style={{ display: 'block', marginTop: tokens.spacingVerticalXXS, marginBottom: tokens.spacingVerticalM }}>
          Already-approved or rejected projects. Change a decision the same way you made it.
        </Body1>
        <Input
          className={styles.search}
          contentBefore={<SearchRegular />}
          placeholder="Search by title, description, or sponsor..."
          value={search}
          onChange={(_, data) => setSearch(data.value)}
        />

        {historyLoading && <Spinner label="Loading history..." />}
        {historyIsError && <Body1>Failed to load history: {(historyError as Error).message}</Body1>}
        {allProjects && decided.length === 0 && <Body1>No decided projects match.</Body1>}

        {decided.map((project) => (
          <ApprovalCard key={project.projectId} project={project} onDone={onDecided} />
        ))}
      </div>
    </>
  );
}
