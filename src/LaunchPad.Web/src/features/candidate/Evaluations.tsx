import { useQuery } from '@tanstack/react-query';
import { Badge, Body1, Card, CardHeader, Caption1, Spinner, Title3, makeStyles, tokens } from '@fluentui/react-components';
import { getAssignmentEvaluations, getMyAssignment } from '../../api/assignments';
import { PageHeader } from '../../components/PageHeader';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalM,
  },
  section: {
    marginTop: tokens.spacingVerticalS,
  },
});

/**
 * Candidate-facing evaluations. CLAUDE.md's single most important control: a
 * hidden numeric/star rating must never reach a Candidate, not in the UI and not
 * in the payload — CandidateEvaluationDto (see api/types.ts) only ever carries
 * Strengths/GrowthAreas/RecommendConversion, so there is nothing to render here
 * even by accident. Do not add a score or star display to this component.
 */
export function Evaluations() {
  const styles = useStyles();

  const { data: assignment, isLoading: assignmentLoading } = useQuery({
    queryKey: ['assignments', 'mine'],
    queryFn: getMyAssignment,
  });

  const { data: evaluations, isLoading: evaluationsLoading } = useQuery({
    queryKey: ['assignments', assignment?.assignmentId, 'evaluations'],
    queryFn: () => getAssignmentEvaluations(assignment!.assignmentId),
    enabled: assignment != null,
  });

  if (assignmentLoading) return <Spinner label="Loading..." />;
  if (!assignment) return <Body1>You don't have an active assignment yet, so there are no evaluations yet.</Body1>;

  return (
    <>
      <PageHeader title="Evaluations" />
      {evaluationsLoading && <Spinner label="Loading your evaluations..." />}
      {evaluations && evaluations.length === 0 && <Body1>No evaluations submitted yet.</Body1>}
      {evaluations?.map((evaluation) => (
        <Card key={evaluation.reviewId} className={styles.card}>
          <CardHeader
            header={<Title3>{evaluation.checkpoint} review</Title3>}
            description={<Caption1>{new Date(evaluation.submittedUtc).toLocaleDateString()}</Caption1>}
          />

          {evaluation.strengths && (
            <div className={styles.section}>
              <Body1><strong>Strengths</strong></Body1>
              <Body1>{evaluation.strengths}</Body1>
            </div>
          )}

          {evaluation.growthAreas && (
            <div className={styles.section}>
              <Body1><strong>Growth areas</strong></Body1>
              <Body1>{evaluation.growthAreas}</Body1>
            </div>
          )}

          {evaluation.recommendConversion != null && (
            <div className={styles.section}>
              <Badge appearance="tint" color={evaluation.recommendConversion ? 'success' : 'informative'}>
                {evaluation.recommendConversion ? 'On track for conversion' : 'Conversion not yet recommended'}
              </Badge>
            </div>
          )}
        </Card>
      ))}
    </>
  );
}
