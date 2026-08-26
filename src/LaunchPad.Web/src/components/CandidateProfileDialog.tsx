import {
  Badge,
  Body1,
  Button,
  Caption1,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Title3,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { FolderRegular } from '@fluentui/react-icons';
import { CandidateAvatar } from './CandidateAvatar';
import { availabilityLabel, candidateStatusLabel, suggestedHireOutcomeLabel } from '../utils/statusLabels';
import type { CandidateDto } from '../api/types';

const useStyles = makeStyles({
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  skillRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexWrap: 'wrap',
    marginTop: tokens.spacingVerticalXS,
  },
  fieldRow: {
    display: 'block',
    marginTop: tokens.spacingVerticalS,
  },
  badgeRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexWrap: 'wrap',
    marginTop: tokens.spacingVerticalXS,
  },
});

/**
 * Read-only candidate "baseball card" for the Talent Pipeline grid — takes the full
 * CandidateDto already in hand from the grid query, no extra fetch. Distinct from
 * CandidateDetailDialog.tsx, which is shaped around a sponsor's SponsorCandidateMatchDto
 * and a "Request assignment" action that doesn't apply here. Whatever score/risk fields
 * are populated are already role-redacted server-side (see CLAUDE.md) — nothing to
 * filter client-side.
 */
export function CandidateProfileDialog({
  candidate,
  onClose,
}: {
  candidate: CandidateDto | null;
  onClose: () => void;
}) {
  const styles = useStyles();

  return (
    <Dialog open={candidate !== null} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        {candidate && (
          <DialogBody>
            <DialogTitle>Candidate details</DialogTitle>
            <DialogContent>
              <div className={styles.header}>
                <CandidateAvatar candidateId={candidate.candidateId} name={candidate.displayName} size={64} />
                <div>
                  <Title3>{candidate.displayName}</Title3>
                  {(candidate.school || candidate.degree) && (
                    <Caption1 style={{ display: 'block' }}>
                      {candidate.school}
                      {candidate.school && candidate.degree ? ' · ' : ''}
                      {candidate.degree}
                    </Caption1>
                  )}
                </div>
              </div>

              <div className={styles.badgeRow}>
                <Badge appearance="tint" color="brand">{candidateStatusLabel(candidate.status)}</Badge>
                {candidate.suggestedHireOutcome != null && (
                  <Badge appearance="tint" color="informative">
                    Suggested: {suggestedHireOutcomeLabel(candidate.suggestedHireOutcome)}
                  </Badge>
                )}
                {(candidate.hasPerformanceRisk || candidate.hasEngagementRisk) && (
                  <Badge appearance="tint" color="warning">Flagged</Badge>
                )}
              </div>

              {candidate.bio && <Body1 className={styles.fieldRow}>{candidate.bio}</Body1>}

              <Caption1 className={styles.fieldRow}>
                {candidate.location ?? 'Location unknown'} · {availabilityLabel(candidate.availability)}
                {candidate.gpa != null ? ` · GPA ${candidate.gpa}` : ''}
                {candidate.graduationDate ? ` · Graduates ${candidate.graduationDate}` : ''}
              </Caption1>

              <div className={styles.skillRow}>
                {candidate.skills.map((skill) => (
                  <Badge key={skill} appearance="outline">
                    {skill}
                  </Badge>
                ))}
              </div>

              <Caption1 className={styles.fieldRow}>Outcome: {candidate.outcome}</Caption1>

              {candidate.averageScore != null && (
                <Caption1 className={styles.fieldRow}>Average score: {candidate.averageScore}</Caption1>
              )}

              {candidate.sharePointFolderWebUrl && (
                <Button
                  appearance="secondary"
                  icon={<FolderRegular />}
                  className={styles.fieldRow}
                  onClick={() => window.open(candidate.sharePointFolderWebUrl!, '_blank', 'noopener')}
                >
                  View in SharePoint
                </Button>
              )}
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={onClose}>
                Close
              </Button>
            </DialogActions>
          </DialogBody>
        )}
      </DialogSurface>
    </Dialog>
  );
}
