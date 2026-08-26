import { useQuery } from '@tanstack/react-query';
import { Badge, Body1, Card, Caption1, Spinner, makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { WarningRegular } from '@fluentui/react-icons';
import { getAtRiskCandidates } from '../../api/ops';
import { PageHeader } from '../../components/PageHeader';
import { useSurfaceStyles } from '../../theme/surfaces';
import type { RiskCandidateDto } from '../../api/types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalM,
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'flex-start',
  },
  iconWrap: {
    fontSize: '24px',
    color: tokens.colorPaletteRedForeground2,
  },
  content: {
    flexGrow: 1,
  },
  topRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
  },
  flagRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    marginTop: tokens.spacingVerticalXS,
  },
});

function severityOf(risk: RiskCandidateDto): 'High' | 'Medium' {
  return risk.hasPerformanceRisk && risk.hasEngagementRisk ? 'High' : 'Medium';
}

// Built from the real computed signal (CandidateRisk.HasPerformanceRisk/
// HasEngagementRisk) rather than a stored, narrative risk register — CLAUDE.md
// requires risk to stay a computed view, never a stored status. Severity here is
// a display-only derivation of those two real flags, not a new stored field.
export function Risks() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();

  const { data: risks, isLoading, isError, error } = useQuery({
    queryKey: ['ops', 'risks'],
    queryFn: getAtRiskCandidates,
  });

  if (isLoading) return <Spinner label="Loading the risk register..." />;
  if (isError) return <Body1>Failed to load risks: {(error as Error).message}</Body1>;

  return (
    <>
      <PageHeader
        title="Risk register"
        subtitle="Every at-risk candidate we're tracking across the program, ordered by severity."
      />

      {risks && risks.length === 0 && <Body1 block>No flagged candidates right now.</Body1>}

      {risks?.map((risk) => {
        const severity = severityOf(risk);
        return (
          <Card key={risk.candidateId} className={mergeClasses(styles.card, surfaces.card)} style={{ marginTop: tokens.spacingVerticalM }}>
            <WarningRegular className={styles.iconWrap} />
            <div className={styles.content}>
              <div className={styles.topRow}>
                <div>
                  <Body1><strong>{risk.displayName}</strong></Body1>
                  <Caption1 style={{ display: 'block' }}>
                    {risk.cohortName}
                    {risk.avgScore != null ? ` · Avg. score ${risk.avgScore}` : ''}
                  </Caption1>
                </div>
                <Badge appearance="tint" color={severity === 'High' ? 'danger' : 'warning'}>{severity}</Badge>
              </div>
              <div className={styles.flagRow}>
                {risk.hasPerformanceRisk && <Badge appearance="outline">Performance risk</Badge>}
                {risk.hasEngagementRisk && <Badge appearance="outline">Engagement risk</Badge>}
                {risk.staleTodoCount > 0 && <Badge appearance="outline">{risk.staleTodoCount} stale task(s)</Badge>}
              </div>
            </div>
          </Card>
        );
      })}
    </>
  );
}
