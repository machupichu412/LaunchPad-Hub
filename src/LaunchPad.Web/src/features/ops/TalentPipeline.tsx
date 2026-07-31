import { useQuery } from '@tanstack/react-query';
import {
  Body1,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Title2,
} from '@fluentui/react-components';
import { getCandidatesByCohort } from '../../api/candidates';

// Demo-only: the local seed data creates exactly one cohort, which is always
// CohortId 1. Once cohort selection exists (Phase 2), this becomes a route param.
const DEMO_COHORT_ID = 1;

// Sponsor/Ops/Exec/HiringManager talent pipeline view. CandidateDto already omits
// hidden scores server-side for unauthorized roles — no client-side filtering needed.
export function TalentPipeline() {
  const { data: candidates, isLoading, isError, error } = useQuery({
    queryKey: ['candidates', 'cohort', DEMO_COHORT_ID],
    queryFn: () => getCandidatesByCohort(DEMO_COHORT_ID),
  });

  if (isLoading) return <Spinner label="Loading talent pipeline..." />;
  if (isError) return <Body1>Failed to load candidates: {(error as Error).message}</Body1>;
  if (!candidates || candidates.length === 0) return <Body1>No candidates in this cohort yet.</Body1>;

  const showScores = candidates.some((c) => c.averageScore != null || c.hasPerformanceRisk != null);

  return (
    <>
      <Title2>Talent Pipeline</Title2>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Name</TableHeaderCell>
            <TableHeaderCell>Location</TableHeaderCell>
            <TableHeaderCell>Availability</TableHeaderCell>
            <TableHeaderCell>Skills</TableHeaderCell>
            <TableHeaderCell>Outcome</TableHeaderCell>
            {showScores && <TableHeaderCell>Avg. score</TableHeaderCell>}
            {showScores && <TableHeaderCell>Risk</TableHeaderCell>}
          </TableRow>
        </TableHeader>
        <TableBody>
          {candidates.map((candidate) => (
            <TableRow key={candidate.candidateId}>
              <TableCell>{candidate.displayName}</TableCell>
              <TableCell>{candidate.location ?? '—'}</TableCell>
              <TableCell>{candidate.availability}</TableCell>
              <TableCell>{candidate.skills.join(', ') || '—'}</TableCell>
              <TableCell>{candidate.outcome}</TableCell>
              {showScores && <TableCell>{candidate.averageScore ?? '—'}</TableCell>}
              {showScores && (
                <TableCell>
                  {candidate.hasPerformanceRisk || candidate.hasEngagementRisk ? 'Flagged' : '—'}
                </TableCell>
              )}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </>
  );
}
