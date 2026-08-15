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
import { CandidateAvatar } from './CandidateAvatar';
import { availabilityLabel } from '../utils/statusLabels';
import type { SponsorCandidateMatchDto } from '../api/types';

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
});

/**
 * The "baseball card" detail view — bio/school/degree/GPA/skills/availability/interest.
 * Never a hidden performance score or risk flag: SponsorCandidateMatchDto structurally
 * can't carry those fields, so there's nothing to filter out here (see CLAUDE.md).
 */
export function CandidateDetailDialog({
  candidate,
  spotsRemaining,
  onClose,
  onRequest,
  isRequesting,
}: {
  candidate: SponsorCandidateMatchDto | null;
  spotsRemaining: number;
  onClose: () => void;
  onRequest: (candidate: SponsorCandidateMatchDto) => void;
  isRequesting: boolean;
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
                <Badge appearance="tint" color="brand" style={{ marginLeft: 'auto' }}>
                  {Math.round(candidate.score)}% match
                </Badge>
              </div>

              {candidate.hasPendingAssignmentElsewhere && (
                <Badge appearance="tint" color="warning" style={{ marginTop: tokens.spacingVerticalS }}>
                  Pending on another project
                </Badge>
              )}

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

              {candidate.interestRating != null && (
                <Caption1 className={styles.fieldRow}>
                  Candidate rated their interest in this project {candidate.interestRating}/5.
                </Caption1>
              )}

              <Caption1 className={styles.fieldRow}>{candidate.rationale}</Caption1>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={onClose}>
                Close
              </Button>
              <Button
                appearance="primary"
                disabled={spotsRemaining <= 0 || isRequesting}
                onClick={() => onRequest(candidate)}
              >
                {spotsRemaining <= 0 ? 'No spots remaining' : 'Request assignment'}
              </Button>
            </DialogActions>
          </DialogBody>
        )}
      </DialogSurface>
    </Dialog>
  );
}
