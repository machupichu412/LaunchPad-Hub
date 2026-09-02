import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Body1,
  Card,
  Caption1,
  Select,
  Spinner,
  Title3,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { Bar, BarChart, CartesianGrid, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { getCohorts } from '../../api/cohorts';
import { getExecutiveDashboard } from '../../api/ops';
import { PageHeader } from '../../components/PageHeader';
import { useSurfaceStyles } from '../../theme/surfaces';

const useStyles = makeStyles({
  toolbarRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalL,
  },
  cohortSelect: {
    minWidth: '220px',
  },
  tileGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(2, 1fr)',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalXL,
  },
  kpiGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(3, 1fr)',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalL,
  },
  tile: {
    padding: tokens.spacingVerticalL,
  },
  tileValue: {
    fontSize: tokens.fontSizeHero800,
    fontWeight: tokens.fontWeightSemibold,
  },
  panel: {
    padding: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalXL,
  },
  sectionTitle: {
    marginBottom: tokens.spacingVerticalM,
    display: 'block',
  },
  universityRow: {
    display: 'flex',
    justifyContent: 'space-between',
    padding: `${tokens.spacingVerticalXS} 0`,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
  },
});

// denominator can legitimately be 0 (no projects/decided candidates yet) — show an
// em dash rather than a misleading 0% or a NaN.
function formatPercent(numerator: number, denominator: number): string {
  if (denominator === 0) return '—';
  return `${Math.round((numerator / denominator) * 100)}%`;
}

function StatTile({ label, value, caption }: { label: string; value: string; caption?: string }) {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  return (
    <Card className={mergeClasses(styles.tile, surfaces.card)}>
      <Caption1>{label}</Caption1>
      <div className={styles.tileValue}>{value}</div>
      {caption && <Caption1>{caption}</Caption1>}
    </Card>
  );
}

const funnelColors = [
  tokens.colorPaletteBlueForeground2,
  tokens.colorPaletteGreenForeground2,
  tokens.colorPaletteMarigoldForeground2,
];

// Funnel (recommended -> approved -> hired) + risk counts for one cohort, backed by
// IReportingRepository.GetExecutiveDashboardAsync on the API. Scoped to a single
// cohort (unlike OpsDashboard's cross-cohort view) because the underlying query has
// no meaningful cross-cohort aggregate — see OpsController.GetExecutiveDashboard.
export function ExecutiveDashboard() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();

  const { data: cohorts, isLoading: cohortsLoading } = useQuery({ queryKey: ['cohorts'], queryFn: getCohorts });
  const [cohortId, setCohortId] = useState<number | null>(null);

  useEffect(() => {
    if (cohortId !== null || !cohorts || cohorts.length === 0) return;
    const active = cohorts.find((c) => c.status === 'Active');
    setCohortId((active ?? cohorts[0]).cohortId);
  }, [cohorts, cohortId]);

  const { data: dashboard, isLoading: dashboardLoading, isError, error } = useQuery({
    queryKey: ['ops', 'executive-dashboard', cohortId],
    queryFn: () => getExecutiveDashboard(cohortId!),
    enabled: cohortId != null,
  });

  const funnelData = dashboard
    ? [
        { name: 'Recommended', value: dashboard.recommendedCount },
        { name: 'Approved', value: dashboard.approvedCount },
        { name: 'Hired', value: dashboard.hiredCount },
      ]
    : [];

  return (
    <>
      <PageHeader
        title="Executive Dashboard"
        subtitle="Program funnel and risk signals for one cohort at a time."
      />

      <div className={styles.toolbarRow}>
        {cohortsLoading && <Spinner size="tiny" label="Loading cohorts..." />}
        {cohorts && cohorts.length > 0 && (
          <Select
            className={styles.cohortSelect}
            value={cohortId ?? ''}
            onChange={(_, data) => setCohortId(Number(data.value))}
          >
            {cohorts.map((cohort) => (
              <option key={cohort.cohortId} value={cohort.cohortId}>
                {cohort.name}
              </option>
            ))}
          </Select>
        )}
      </div>

      {cohorts && cohorts.length === 0 && <Body1>No cohorts exist yet.</Body1>}
      {dashboardLoading && <Spinner label="Loading program funnel..." />}
      {isError && <Body1>Failed to load the executive dashboard: {(error as Error).message}</Body1>}

      {dashboard && (
        <>
          <Title3 className={styles.sectionTitle}>Core Objectives &amp; Exec KPIs</Title3>
          <div className={styles.kpiGrid}>
            <StatTile
              label="AI Solutions Delivered"
              value={String(dashboard.solutionsDeliveredCount)}
              caption="Target: 20+ per cohort"
            />
            <StatTile
              label="Advance Beyond Showcase"
              value={formatPercent(dashboard.pilotReadyCount, dashboard.projectCount)}
              caption={`Target: ≥50% · MVP complete ${formatPercent(dashboard.mvpCompleteCount, dashboard.projectCount)}`}
            />
            <StatTile
              label="Hire-Ready Talent"
              value={formatPercent(dashboard.hireReadyCandidateCount, dashboard.decidedCandidateCount)}
              caption="Target: ≥80% deemed hire-ready"
            />
            <StatTile
              label="Sponsor Rating"
              value={dashboard.averageSponsorRating != null ? dashboard.averageSponsorRating.toFixed(1) : '—'}
              caption={`Target: ≥4.0/5 · n=${dashboard.sponsorRatingCount}`}
            />
            <StatTile
              label="Business Value Generated"
              value={formatPercent(dashboard.businessValueDocumentedCount, dashboard.projectCount)}
              caption="Target: 100% documented & signed off"
            />
            <StatTile
              label="University Breakdown"
              value={String(dashboard.universityBreakdown.length)}
              caption="Universities represented"
            />
          </div>

          {dashboard.universityBreakdown.length > 0 && (
            <Card className={mergeClasses(styles.panel, surfaces.card)}>
              <Title3>Future Workforce Diversity — by university</Title3>
              <div style={{ marginTop: tokens.spacingVerticalM }}>
                {dashboard.universityBreakdown.map((u) => (
                  <div key={u.school} className={styles.universityRow}>
                    <Body1>{u.school}</Body1>
                    <Body1>{u.candidateCount}</Body1>
                  </div>
                ))}
              </div>
            </Card>
          )}

          <div className={styles.tileGrid}>
            <StatTile label="Performance Risk" value={String(dashboard.performanceRiskCount)} caption="Candidates flagged" />
            <StatTile label="Engagement Risk" value={String(dashboard.engagementRiskCount)} caption="Candidates flagged" />
          </div>

          <Card className={mergeClasses(styles.panel, surfaces.card)}>
            <Title3>Recommended → Approved → Hired</Title3>
            <div style={{ height: 280, marginTop: tokens.spacingVerticalM }}>
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={funnelData}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} />
                  <XAxis dataKey="name" />
                  <YAxis allowDecimals={false} />
                  <Tooltip />
                  <Bar dataKey="value" radius={[4, 4, 0, 0]} isAnimationActive={false}>
                    {funnelData.map((entry, index) => (
                      <Cell key={entry.name} fill={funnelColors[index]} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          </Card>
        </>
      )}
    </>
  );
}
