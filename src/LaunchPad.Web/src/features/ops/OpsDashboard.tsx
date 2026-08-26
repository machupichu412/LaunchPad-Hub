import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Body1,
  Button,
  Card,
  Caption1,
  Spinner,
  Title1,
  Title3,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { CheckmarkCircleRegular } from '@fluentui/react-icons';
import { Bar, BarChart, CartesianGrid, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { getOpsDashboard } from '../../api/ops';
import { useSurfaceStyles } from '../../theme/surfaces';

const useStyles = makeStyles({
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: tokens.spacingVerticalXL,
  },
  eyebrow: {
    color: tokens.colorBrandForeground1,
    fontWeight: tokens.fontWeightSemibold,
  },
  tileGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(4, 1fr)',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalXL,
  },
  tile: {
    padding: tokens.spacingVerticalL,
  },
  tileValue: {
    fontSize: tokens.fontSizeHero800,
    fontWeight: tokens.fontWeightSemibold,
  },
  bottomRow: {
    display: 'grid',
    gridTemplateColumns: '2fr 1fr',
    gap: tokens.spacingHorizontalL,
    alignItems: 'start',
  },
  panel: {
    padding: tokens.spacingVerticalL,
  },
  riskCard: {
    padding: tokens.spacingVerticalM,
    marginTop: tokens.spacingVerticalS,
  },
});

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
  tokens.colorPaletteRedForeground2,
  tokens.colorPaletteMarigoldForeground2,
];

export function OpsDashboard() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const navigate = useNavigate();

  const { data: dashboard, isLoading, isError, error } = useQuery({
    queryKey: ['ops', 'dashboard'],
    queryFn: getOpsDashboard,
  });

  if (isLoading) return <Spinner label="Loading program health..." />;
  if (isError) return <Body1>Failed to load the dashboard: {(error as Error).message}</Body1>;
  if (!dashboard) return null;

  const funnelData = [
    { name: 'Proposed', value: dashboard.matchFunnel.proposed },
    { name: 'Approved', value: dashboard.matchFunnel.approved },
    { name: 'Denied', value: dashboard.matchFunnel.denied },
    { name: 'Active', value: dashboard.matchFunnel.active },
  ];

  return (
    <>
      <div className={styles.header}>
        <div>
          <Caption1 block className={styles.eyebrow}>PROGRAM OPERATIONS</Caption1>
          <Title1 block style={{ marginTop: tokens.spacingVerticalXXS }}>Program health at a glance</Title1>
          <Body1 block style={{ marginTop: tokens.spacingVerticalXS }}>
            Approve proposed matches, monitor cohort health, and stay ahead of the risks that matter most.
          </Body1>
        </div>
        <Button appearance="primary" icon={<CheckmarkCircleRegular />} onClick={() => navigate('/ops/approvals')}>
          Review approvals
        </Button>
      </div>

      <div className={styles.tileGrid}>
        <StatTile label="Active Candidates" value={String(dashboard.activeCandidateCount)} caption="Across all cohorts" />
        <StatTile
          label="Active Projects"
          value={String(dashboard.activeProjectCount)}
          caption={`${dashboard.activeProjectCohortCount} cohort${dashboard.activeProjectCohortCount === 1 ? '' : 's'}`}
        />
        <StatTile
          label="Pending Approvals"
          value={String(dashboard.pendingApprovalCount)}
          caption={`${dashboard.approvedTotalCount} approved`}
        />
        <StatTile label="High Risks" value={String(dashboard.highRiskCount)} />
      </div>

      <div className={styles.bottomRow}>
        <Card className={mergeClasses(styles.panel, surfaces.card)}>
          <Title3>Match funnel</Title3>
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

        <Card className={mergeClasses(styles.panel, surfaces.card)}>
          <Title3>Top risks</Title3>
          {dashboard.topRisks.length === 0 && <Body1>No flagged candidates right now.</Body1>}
          {dashboard.topRisks.map((risk) => {
            const severity = risk.hasPerformanceRisk && risk.hasEngagementRisk ? 'High' : 'Medium';
            return (
              <Card key={risk.candidateId} className={mergeClasses(styles.riskCard, surfaces.card)}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                  <div>
                    <Body1><strong>{risk.displayName}</strong></Body1>
                    <Caption1 style={{ display: 'block' }}>{risk.cohortName}</Caption1>
                  </div>
                  <Badge appearance="tint" color={severity === 'High' ? 'danger' : 'warning'}>{severity}</Badge>
                </div>
              </Card>
            );
          })}
        </Card>
      </div>
    </>
  );
}
