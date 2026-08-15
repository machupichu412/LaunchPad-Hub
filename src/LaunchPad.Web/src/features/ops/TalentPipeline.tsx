import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Badge, Body1, Card, Caption1, Field, Input, Select, Spinner, Title3, makeStyles, tokens } from '@fluentui/react-components';
import { SearchRegular } from '@fluentui/react-icons';
import { getCandidatesByCohort, updateCandidateStatus } from '../../api/candidates';
import { CandidateAvatar } from '../../components/CandidateAvatar';
import { PageHeader } from '../../components/PageHeader';
import { candidateStatusLabel, suggestedHireOutcomeLabel } from '../../utils/statusLabels';
import type { CandidateStatus } from '../../api/types';

// Falls back to cohort 1 (the local seed data's only cohort) when reached via the
// plain "/pipeline" nav link rather than a specific cohort's "View candidates" — see
// Cohorts.tsx, which links here with a real :cohortId.
const DEMO_COHORT_ID = 1;

const useStyles = makeStyles({
  search: {
    width: '280px',
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
  cardHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
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
  const { cohortId: cohortIdParam } = useParams<{ cohortId?: string }>();
  const cohortId = cohortIdParam ? Number(cohortIdParam) : DEMO_COHORT_ID;
  const queryClient = useQueryClient();

  const { data: candidates, isLoading, isError, error } = useQuery({
    queryKey: ['candidates', 'cohort', cohortId],
    queryFn: () => getCandidatesByCohort(cohortId),
    enabled: !Number.isNaN(cohortId),
  });

  const statusMutation = useMutation({
    mutationFn: ({ candidateId, status }: { candidateId: number; status: CandidateStatus }) =>
      updateCandidateStatus(candidateId, { status, reason: null }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['candidates', 'cohort', cohortId] }),
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
      <PageHeader
        title="Candidates"
        subtitle="Browse the current cohort of participating candidates."
        actions={
          <Input
            className={styles.search}
            contentBefore={<SearchRegular />}
            placeholder="Search candidates..."
            value={search}
            onChange={(_, data) => setSearch(data.value)}
          />
        }
      />

      {filtered.length === 0 && <Body1>No candidates match.</Body1>}

      <div className={styles.grid}>
        {filtered.map((candidate) => (
          <Card key={candidate.candidateId} className={styles.card}>
            <div className={styles.cardHeader}>
              <CandidateAvatar candidateId={candidate.candidateId} name={candidate.displayName} />
              <Title3>{candidate.displayName}</Title3>
            </div>
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
            {candidate.suggestedHireOutcome != null && (
              <Badge appearance="tint" color="brand" style={{ marginTop: tokens.spacingVerticalXXS, alignSelf: 'flex-start' }}>
                Suggested: {suggestedHireOutcomeLabel(candidate.suggestedHireOutcome)}
              </Badge>
            )}
            {candidate.hasPerformanceRisk !== undefined && (
              <Field label="Outcome" style={{ marginTop: tokens.spacingVerticalXS }}>
                <Select
                  value={candidate.status}
                  disabled={statusMutation.isPending}
                  onChange={(_, data) =>
                    statusMutation.mutate({ candidateId: candidate.candidateId, status: data.value as CandidateStatus })
                  }
                >
                  <option value="InProgress">{candidateStatusLabel('InProgress')}</option>
                  <option value="Hire">{candidateStatusLabel('Hire')}</option>
                  <option value="TalentPlus">{candidateStatusLabel('TalentPlus')}</option>
                  <option value="NoHire">{candidateStatusLabel('NoHire')}</option>
                </Select>
              </Field>
            )}
          </Card>
        ))}
      </div>
      {statusMutation.isError && <Body1>Failed to update outcome: {(statusMutation.error as Error).message}</Body1>}
    </>
  );
}
