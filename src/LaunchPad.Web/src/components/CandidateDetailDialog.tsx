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
import { CheckmarkRegular, DismissRegular } from '@fluentui/react-icons';
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
 *
 * A candidate with a non-null proposedAssignmentId already has a Program-Ops-batch-matched
 * Assignment (Status = Proposed) on this project, so the footer offers Recommend/Reject
 * against that existing Assignment instead of "Request assignment" — issuing a fresh request
 * would create a second, duplicate Assignment row for the same candidate+project pair.
 */
export function CandidateDetailDialog({
  candidate,
  spotsRemaining,
  onClose,
  onRequest,
  isRequesting,
  onRecommend,
  onReject,
  isRecommending,
  isRejecting,
}: {
  candidate: SponsorCandidateMatchDto | null;
  spotsRemaining: number;
  onClose: () => void;
  onRequest: (candidate: SponsorCandidateMatchDto) => void;
  isRequesting: boolean;
  onRecommend: (candidate: SponsorCandidateMatchDto) => void;
  onReject: (candidate: SponsorCandidateMatchDto) => void;
  isRecommending: boolean;
  isRejecting: boolean;
}) {
  const styles = useStyles();
  const isMatched = candidate?.proposedAssignmentId != null;

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

              {isMatched && (
                <Badge appearance="tint" color="success" style={{ marginTop: tokens.spacingVerticalS }}>
                  Matched by Program Ops
                </Badge>
              )}
              {candidate.hasPendingAssignmentElsewhere && (
                <Badge
                  appearance="tint"
                  color="warning"
                  style={{ marginTop: tokens.spacingVerticalS, marginLeft: tokens.spacingHorizontalXS }}
                >
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
              {isMatched ? (
                <>
                  <Button
                    icon={<DismissRegular />}
                    disabled={isRecommending || isRejecting}
                    onClick={() => onReject(candidate)}
                  >
                    Reject
                  </Button>
                  <Button
                    appearance="primary"
                    icon={<CheckmarkRegular />}
                    disabled={isRecommending || isRejecting}
                    onClick={() => onRecommend(candidate)}
                  >
                    Recommend
                  </Button>
                </>
              ) : (
                <Button
                  appearance="primary"
                  disabled={spotsRemaining <= 0 || isRequesting}
                  onClick={() => onRequest(candidate)}
                >
                  {spotsRemaining <= 0 ? 'No spots remaining' : 'Request assignment'}
                </Button>
              )}
            </DialogActions>
          </DialogBody>
        )}
      </DialogSurface>
    </Dialog>
  );
}
