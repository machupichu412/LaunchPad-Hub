import { useQuery } from '@tanstack/react-query';
import { Badge, Body1, Spinner, Title3, makeStyles, tokens } from '@fluentui/react-components';
import { getAssignedCandidates } from '../api/projects';
import { CandidateAvatar } from './CandidateAvatar';
import { assignmentStatusLabel } from '../utils/statusLabels';
import type { AssignmentStatus } from '../api/types';

const useStyles = makeStyles({
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalS,
  },
  card: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    padding: tokens.spacingVerticalS,
    border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
  },
  name: {
    display: 'block',
  },
});

// SponsorApproved/OpsApproved/Active only distinguish "pending Ops review" from "committed" —
// Completed shows up too once a project wraps, Withdrawn/Proposed never reach this endpoint.
const statusColor: Record<AssignmentStatus, 'informative' | 'success' | 'warning'> = {
  Proposed: 'informative',
  SponsorApproved: 'warning',
  OpsApproved: 'success',
  Active: 'success',
  Completed: 'success',
  Withdrawn: 'informative',
};

/** The "Assigned candidates" section on a project's page — every SponsorApproved/
 * OpsApproved/Active/Completed assignment, so Ops can see the whole roster at a glance. */
export function AssignedCandidatesSection({ projectId }: { projectId: number }) {
  const styles = useStyles();
  const { data: candidates, isLoading } = useQuery({
    queryKey: ['projects', projectId, 'assigned-candidates'],
    queryFn: () => getAssignedCandidates(projectId),
  });

  if (isLoading) return <Spinner size="tiny" label="Loading assigned candidates..." />;
  if (!candidates || candidates.length === 0) return null;

  return (
    <div style={{ marginTop: tokens.spacingVerticalL }}>
      <Title3>Assigned candidates</Title3>
      <div className={styles.grid}>
        {candidates.map((c) => (
          <div key={c.assignmentId} className={styles.card}>
            <CandidateAvatar candidateId={c.candidateId} name={c.candidateName} size={32} />
            <div>
              <Body1 className={styles.name}>{c.candidateName}</Body1>
              <Badge appearance="tint" color={statusColor[c.status]} size="small">
                {assignmentStatusLabel(c.status)}
              </Badge>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
