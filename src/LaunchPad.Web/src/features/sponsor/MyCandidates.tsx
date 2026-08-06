import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Badge, Body1, Button, Card, Caption1, Spinner, Title2, makeStyles, tokens } from '@fluentui/react-components';
import { getMyCandidates } from '../../api/sponsors';

const useStyles = makeStyles({
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalM,
  },
  card: {
    padding: tokens.spacingVerticalM,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
});

// Candidates currently or previously committed to the sponsor's own projects —
// the jumping-off point for submitting midpoint/final reviews.
export function MyCandidates() {
  const styles = useStyles();
  const navigate = useNavigate();

  const { data: candidates, isLoading, isError, error } = useQuery({
    queryKey: ['sponsors', 'me', 'candidates'],
    queryFn: getMyCandidates,
  });

  return (
    <>
      <Title2>My Candidates</Title2>
      <Body1>Candidates working on your projects.</Body1>

      {isLoading && <Spinner label="Loading your candidates..." />}
      {isError && <Body1>Failed to load your candidates: {(error as Error).message}</Body1>}
      {candidates && candidates.length === 0 && <Body1>No candidates assigned to your projects yet.</Body1>}

      <div className={styles.grid}>
        {candidates?.map((candidate) => (
          <Card key={candidate.assignmentId} className={styles.card}>
            <Body1>
              <strong>{candidate.candidateName}</strong>
            </Body1>
            <Caption1>{candidate.projectName}</Caption1>
            <Badge appearance="tint" color={candidate.status === 'Active' ? 'success' : 'informative'}>
              {candidate.status}
            </Badge>
            {candidate.status === 'Active' && (
              <Button
                appearance="primary"
                style={{ marginTop: tokens.spacingVerticalXS }}
                onClick={() => navigate(`/candidates/${candidate.assignmentId}/review`)}
              >
                Submit review
              </Button>
            )}
          </Card>
        ))}
      </div>
    </>
  );
}
