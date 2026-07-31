import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Badge, Body1, Card, Caption1, Input, Spinner, Title2, Title3, makeStyles, tokens } from '@fluentui/react-components';
import { SearchRegular } from '@fluentui/react-icons';
import { getCandidatesByCohort } from '../../api/candidates';

// Demo-only: the local seed data creates exactly one cohort, which is always
// CohortId 1. Once cohort selection exists (Phase 2), this becomes a route param.
const DEMO_COHORT_ID = 1;

const useStyles = makeStyles({
  search: {
    maxWidth: '420px',
    marginTop: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalL,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
    gap: tokens.spacingHorizontalM,
  },
  card: {
    padding: tokens.spacingVerticalM,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  skillRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexWrap: 'wrap',
    marginTop: tokens.spacingVerticalXS,
  },
});

// Sponsor/Ops/Exec/HiringManager talent directory (labeled "Candidates" in the
// Program Ops nav — see NavMenu). CandidateDto already omits hidden scores
// server-side for unauthorized roles — no client-side filtering needed.
export function TalentPipeline() {
  const styles = useStyles();
  const [search, setSearch] = useState('');

  const { data: candidates, isLoading, isError, error } = useQuery({
    queryKey: ['candidates', 'cohort', DEMO_COHORT_ID],
    queryFn: () => getCandidatesByCohort(DEMO_COHORT_ID),
  });

  if (isLoading) return <Spinner label="Loading candidates..." />;
  if (isError) return <Body1>Failed to load candidates: {(error as Error).message}</Body1>;
  if (!candidates || candidates.length === 0) return <Body1>No candidates in this cohort yet.</Body1>;

  const showScores = candidates.some((c) => c.averageScore != null || c.hasPerformanceRisk != null);

  const filtered = candidates.filter((c) => {
    const term = search.trim().toLowerCase();
    if (term.length === 0) return true;
    return (
      c.displayName.toLowerCase().includes(term) ||
      c.school?.toLowerCase().includes(term) ||
      c.skills.some((s) => s.toLowerCase().includes(term))
    );
  });

  return (
    <>
      <Title2>Candidates</Title2>
      <Body1>Browse the current cohort of participating candidates.</Body1>

      <Input
        className={styles.search}
        contentBefore={<SearchRegular />}
        placeholder="Search candidates..."
        value={search}
        onChange={(_, data) => setSearch(data.value)}
      />

      {filtered.length === 0 && <Body1>No candidates match.</Body1>}

      <div className={styles.grid}>
        {filtered.map((candidate) => (
          <Card key={candidate.candidateId} className={styles.card}>
            <Title3>{candidate.displayName}</Title3>
            {(candidate.school || candidate.degree) && (
              <Caption1>
                {candidate.school}
                {candidate.school && candidate.degree ? ' · ' : ''}
                {candidate.degree}
              </Caption1>
            )}
            <Caption1>
              {candidate.location ?? 'Location unknown'}
              {candidate.gpa != null ? ` · GPA ${candidate.gpa}` : ''}
            </Caption1>
            <div className={styles.skillRow}>
              {candidate.skills.map((skill) => (
                <Badge key={skill} appearance="outline">{skill}</Badge>
              ))}
            </div>
            <Caption1 style={{ marginTop: tokens.spacingVerticalXS }}>Outcome: {candidate.outcome}</Caption1>
            {showScores && (candidate.averageScore != null || candidate.hasPerformanceRisk || candidate.hasEngagementRisk) && (
              <Caption1>
                {candidate.averageScore != null ? `Avg. score ${candidate.averageScore}` : ''}
                {(candidate.hasPerformanceRisk || candidate.hasEngagementRisk) ? ' · Flagged' : ''}
              </Caption1>
            )}
          </Card>
        ))}
      </div>
    </>
  );
}
