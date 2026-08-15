import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Badge, Body1, Button, Card, Caption1, Spinner, makeStyles, tokens } from '@fluentui/react-components';
import { ArrowSyncRegular, CheckmarkRegular, DismissRegular } from '@fluentui/react-icons';
import { approveMatch, denyMatch, getMatchingQueue, runMatching } from '../../api/matching';
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
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexShrink: 0,
  },
});

// Reviews SponsorApproved assignments — the item Ops actually acts on, whether it
// arrived via the matching engine + a sponsor's recommend, or a sponsor's direct
// request from a project's candidate gallery. "Run matching" queues an async job
// (Service Bus + CohortMatchingFunction in Azure; an inline local-dev fallback here)
// rather than running inline — see MatchingController.Run.
export function ApprovalQueue() {
  const styles = useStyles();
  const queryClient = useQueryClient();

  const { data: queue, isLoading, isError, error } = useQuery({
    queryKey: ['matching', 'queue', DEMO_COHORT_ID],
    queryFn: () => getMatchingQueue(DEMO_COHORT_ID),
  });

  const invalidateQueue = () => queryClient.invalidateQueries({ queryKey: ['matching', 'queue', DEMO_COHORT_ID] });

  const runMutation = useMutation({
    mutationFn: () => runMatching(DEMO_COHORT_ID),
    onSuccess: invalidateQueue,
  });
  const approveMutation = useMutation({ mutationFn: approveMatch, onSuccess: invalidateQueue });
  const denyMutation = useMutation({ mutationFn: denyMatch, onSuccess: invalidateQueue });

  return (
    <>
      <PageHeader
        title="Approval queue"
        subtitle="Review matches proposed by the matching engine. Approving staffs the candidate; denying returns them to the pool."
        actions={
          <Button
            appearance="primary"
            icon={<ArrowSyncRegular />}
            disabled={runMutation.isPending}
            onClick={() => runMutation.mutate()}
          >
            {runMutation.isPending ? 'Running...' : 'Run matching'}
          </Button>
        }
      />
      {runMutation.isSuccess && <Body1>Matching job queued — new proposals will appear here shortly.</Body1>}
      {runMutation.isError && <Body1>Failed to run matching: {(runMutation.error as Error).message}</Body1>}

      {isLoading && <Spinner label="Loading the queue..." />}
      {isError && <Body1>Failed to load the queue: {(error as Error).message}</Body1>}
      {queue && queue.length === 0 && <Body1>Nothing pending — try "Run matching" to generate new proposals.</Body1>}

      {queue?.map((pending) => (
        <Card key={pending.assignmentId} className={styles.card}>
          <div>
            <Body1>
              <strong>{pending.candidateName}</strong> → {pending.projectName}
            </Body1>
            <Caption1 style={{ display: 'block' }}>
              Proposed for {pending.sponsorName}
              {pending.sponsorOrganization ? ` · ${pending.sponsorOrganization}` : ''}
            </Caption1>
            {pending.matchScore != null && (
              <Badge appearance="tint" color="brand" style={{ marginTop: tokens.spacingVerticalXS }}>
                {pending.matchScore}% match
              </Badge>
            )}
            {pending.matchRationale && (
              <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalXS }}>{pending.matchRationale}</Caption1>
            )}
          </div>
          <div className={styles.actions}>
            <Button
              icon={<DismissRegular />}
              disabled={denyMutation.isPending || approveMutation.isPending}
              onClick={() => denyMutation.mutate(pending.assignmentId)}
            >
              Deny
            </Button>
            <Button
              appearance="primary"
              icon={<CheckmarkRegular />}
              disabled={denyMutation.isPending || approveMutation.isPending}
              onClick={() => approveMutation.mutate(pending.assignmentId)}
            >
              Approve
            </Button>
          </div>
        </Card>
      ))}
      {approveMutation.isError && <Body1>Failed to approve: {(approveMutation.error as Error).message}</Body1>}
    </>
  );
}
