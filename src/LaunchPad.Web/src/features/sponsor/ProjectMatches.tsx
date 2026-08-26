import { useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Badge, Body1, Button, Card, Caption1, Spinner, makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { CheckmarkRegular, DismissRegular } from '@fluentui/react-icons';
import { getProjectMatches, recommendMatch, rejectMatch } from '../../api/matches';
import { PageHeader } from '../../components/PageHeader';
import { useSurfaceStyles } from '../../theme/surfaces';

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

// The sponsor's half of the two-stage approval: pick one of up to 3 candidates the
// matching engine proposed for this project. Recommending one auto-withdraws the
// other proposed candidates on this same project (see ProjectsController); Ops then
// approves from there.
export function ProjectMatches() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const { id } = useParams<{ id: string }>();
  const projectId = Number(id);
  const queryClient = useQueryClient();

  const { data: matches, isLoading, isError, error } = useQuery({
    queryKey: ['projects', projectId, 'matches'],
    queryFn: () => getProjectMatches(projectId),
    enabled: !Number.isNaN(projectId),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'matches'] });
  const recommendMutation = useMutation({
    mutationFn: (assignmentId: number) => recommendMatch(projectId, assignmentId),
    onSuccess: invalidate,
  });
  const rejectMutation = useMutation({
    mutationFn: (assignmentId: number) => rejectMatch(projectId, assignmentId),
    onSuccess: invalidate,
  });

  return (
    <>
      <PageHeader
        title="Review matches"
        subtitle="Pick one candidate to recommend for this project — Program Ops approves from there."
      />

      {isLoading && <Spinner label="Loading matches..." />}
      {isError && <Body1>Failed to load matches: {(error as Error).message}</Body1>}
      {matches && matches.length === 0 && (
        <Body1>No matches proposed yet — check back after Program Ops runs matching for this cohort.</Body1>
      )}

      <div style={{ marginTop: tokens.spacingVerticalM }}>
        {matches?.map((match) => (
          <Card key={match.assignmentId} className={mergeClasses(styles.card, surfaces.card)}>
            <div>
              <Body1>
                <strong>{match.candidateName}</strong>
              </Body1>
              {match.matchScore != null && (
                <Badge appearance="tint" color="brand" style={{ marginLeft: tokens.spacingHorizontalXS }}>
                  {match.matchScore}% match
                </Badge>
              )}
              {match.matchRationale && (
                <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalXS }}>{match.matchRationale}</Caption1>
              )}
            </div>
            <div className={styles.actions}>
              <Button
                icon={<DismissRegular />}
                disabled={recommendMutation.isPending || rejectMutation.isPending}
                onClick={() => rejectMutation.mutate(match.assignmentId)}
              >
                Reject
              </Button>
              <Button
                appearance="primary"
                icon={<CheckmarkRegular />}
                disabled={recommendMutation.isPending || rejectMutation.isPending}
                onClick={() => recommendMutation.mutate(match.assignmentId)}
              >
                Recommend
              </Button>
            </div>
          </Card>
        ))}
      </div>
      {recommendMutation.isError && <Body1>Failed to recommend: {(recommendMutation.error as Error).message}</Body1>}
      {rejectMutation.isError && <Body1>Failed to reject: {(rejectMutation.error as Error).message}</Body1>}
    </>
  );
}
