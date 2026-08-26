import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Badge,
  Body1,
  Button,
  Card,
  Caption1,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Spinner,
  Title3,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { CheckmarkRegular, DismissRegular } from '@fluentui/react-icons';
import { getEligibleCandidates, requestAssignment } from '../api/projects';
import { recommendMatch, rejectMatch } from '../api/matches';
import { CandidateAvatar } from './CandidateAvatar';
import { CandidateDetailDialog } from './CandidateDetailDialog';
import { RequestAssignmentConfirmDialog } from './RequestAssignmentConfirmDialog';
import { useSurfaceStyles } from '../theme/surfaces';
import type { SponsorCandidateMatchDto } from '../api/types';

const CONDENSED_COUNT = 6;

const useStyles = makeStyles({
  strip: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    overflowX: 'auto',
    paddingBottom: tokens.spacingVerticalXS,
  },
  miniCard: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalXS,
    padding: tokens.spacingVerticalS,
    minWidth: '150px',
    border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    cursor: 'pointer',
  },
  fullGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))',
    gap: tokens.spacingHorizontalM,
  },
  fullCard: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalXS,
    padding: tokens.spacingVerticalM,
    border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    cursor: 'pointer',
  },
  centeredText: {
    textAlign: 'center',
  },
  matchedSection: {
    marginBottom: tokens.spacingVerticalXL,
  },
  matchedCard: {
    padding: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalS,
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    cursor: 'pointer',
    border: `${tokens.strokeWidthThin} solid ${tokens.colorPaletteGreenBorder2}`,
  },
  matchedCardIdentity: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  matchedActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexShrink: 0,
  },
});

/**
 * Sponsor's eligible-candidate browsing UI for one project. Candidates Program Ops's cohort-wide
 * batch matching already proposed for this project (Assignment.Status == Proposed) surface first,
 * in their own "Matched by Program Ops" section — clicking one goes straight to Recommend/Reject
 * against that existing Assignment (see CandidateDetailDialog), never a fresh "Request" that would
 * create a second Assignment row for the same candidate+project pair. Everyone else in the cohort
 * follows below in the existing condensed strip / full-screen "baseball card" gallery. Only ever
 * queried while the project is Approved (both the server 400s otherwise and this component itself
 * doesn't fetch before then) — sponsors can't request before Ops signs off.
 */
export function CandidateGallery({
  projectId,
  projectName,
  isApproved,
  spotsRemaining,
}: {
  projectId: number;
  projectName: string;
  isApproved: boolean;
  spotsRemaining: number;
}) {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const queryClient = useQueryClient();

  const { data: candidates, isLoading, isError } = useQuery({
    queryKey: ['projects', projectId, 'eligible-candidates'],
    queryFn: () => getEligibleCandidates(projectId),
    enabled: isApproved,
  });

  const [galleryOpen, setGalleryOpen] = useState(false);
  const [selectedCandidate, setSelectedCandidate] = useState<SponsorCandidateMatchDto | null>(null);
  const [pendingRequest, setPendingRequest] = useState<SponsorCandidateMatchDto | null>(null);

  const invalidateAfterAssignmentChange = () => {
    queryClient.invalidateQueries({ queryKey: ['projects', projectId] });
    queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'eligible-candidates'] });
    queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'assigned-candidates'] });
    queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'matches'] });
  };

  const requestMutation = useMutation({
    mutationFn: (candidateId: number) => requestAssignment(projectId, candidateId),
    onSuccess: () => {
      setPendingRequest(null);
      setSelectedCandidate(null);
      invalidateAfterAssignmentChange();
    },
  });

  const recommendMutation = useMutation({
    mutationFn: (assignmentId: number) => recommendMatch(projectId, assignmentId),
    onSuccess: () => {
      setSelectedCandidate(null);
      invalidateAfterAssignmentChange();
    },
  });

  const rejectMutation = useMutation({
    mutationFn: (assignmentId: number) => rejectMatch(projectId, assignmentId),
    onSuccess: () => {
      setSelectedCandidate(null);
      invalidateAfterAssignmentChange();
    },
  });

  const openRequestConfirm = (candidate: SponsorCandidateMatchDto) => {
    setSelectedCandidate(null);
    setPendingRequest(candidate);
  };

  const handleRecommend = (candidate: SponsorCandidateMatchDto) => {
    if (candidate.proposedAssignmentId != null) recommendMutation.mutate(candidate.proposedAssignmentId);
  };
  const handleReject = (candidate: SponsorCandidateMatchDto) => {
    if (candidate.proposedAssignmentId != null) rejectMutation.mutate(candidate.proposedAssignmentId);
  };

  const { matchedCandidates, otherCandidates } = useMemo(() => {
    const matched: SponsorCandidateMatchDto[] = [];
    const other: SponsorCandidateMatchDto[] = [];
    for (const c of candidates ?? []) {
      (c.proposedAssignmentId != null ? matched : other).push(c);
    }
    return { matchedCandidates: matched, otherCandidates: other };
  }, [candidates]);

  if (!isApproved) {
    return (
      <Body1 style={{ display: 'block', marginTop: tokens.spacingVerticalM }}>
        Candidates can be browsed once this project is approved.
      </Body1>
    );
  }

  if (isLoading) return <Spinner size="tiny" label="Loading eligible candidates..." />;
  if (isError) return <Body1>Failed to load eligible candidates.</Body1>;
  if (!candidates || candidates.length === 0) {
    return <Body1 style={{ display: 'block', marginTop: tokens.spacingVerticalM }}>No eligible candidates in this cohort right now.</Body1>;
  }

  const condensed = otherCandidates.slice(0, CONDENSED_COUNT);
  const isActingOnMatch = recommendMutation.isPending || rejectMutation.isPending;

  return (
    <div style={{ marginTop: tokens.spacingVerticalL }}>
      {matchedCandidates.length > 0 && (
        <div className={styles.matchedSection}>
          <Title3>Matched by Program Ops</Title3>
          <Caption1 style={{ display: 'block', marginBottom: tokens.spacingVerticalS }}>
            The matching engine already shortlisted these candidates for this project — recommend one to move it toward approval, or reject to send it back to the pool.
          </Caption1>
          {matchedCandidates.map((c) => (
            <Card
              key={c.candidateId}
              className={mergeClasses(styles.matchedCard, surfaces.card)}
              onClick={() => setSelectedCandidate(c)}
            >
              <div className={styles.matchedCardIdentity}>
                <CandidateAvatar candidateId={c.candidateId} name={c.displayName} size={40} />
                <div>
                  <Body1>
                    <strong>{c.displayName}</strong>
                  </Body1>
                  <Badge appearance="tint" color="brand" style={{ marginLeft: tokens.spacingHorizontalXS }}>
                    {Math.round(c.score)}% match
                  </Badge>
                  <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalXXS }}>{c.rationale}</Caption1>
                </div>
              </div>
              <div className={styles.matchedActions}>
                <Button
                  icon={<DismissRegular />}
                  disabled={isActingOnMatch}
                  onClick={(e) => {
                    e.stopPropagation();
                    handleReject(c);
                  }}
                >
                  Reject
                </Button>
                <Button
                  appearance="primary"
                  icon={<CheckmarkRegular />}
                  disabled={isActingOnMatch}
                  onClick={(e) => {
                    e.stopPropagation();
                    handleRecommend(c);
                  }}
                >
                  Recommend
                </Button>
              </div>
            </Card>
          ))}
          {recommendMutation.isError && <Body1>Failed to recommend: {(recommendMutation.error as Error).message}</Body1>}
          {rejectMutation.isError && <Body1>Failed to reject: {(rejectMutation.error as Error).message}</Body1>}
        </div>
      )}

      <Title3>{matchedCandidates.length > 0 ? 'Other eligible candidates' : 'Eligible candidates'}</Title3>
      <Caption1 style={{ display: 'block', marginBottom: tokens.spacingVerticalS }}>
        {spotsRemaining > 0 ? `${spotsRemaining} spot(s) open` : 'All spots filled'}
      </Caption1>

      {otherCandidates.length === 0 ? (
        <Body1>No other eligible candidates in this cohort right now.</Body1>
      ) : (
        <>
          <div className={styles.strip}>
            {condensed.map((c) => (
              <div key={c.candidateId} className={styles.miniCard} onClick={() => setSelectedCandidate(c)}>
                <CandidateAvatar candidateId={c.candidateId} name={c.displayName} size={40} />
                <Body1 className={styles.centeredText}>{c.displayName}</Body1>
                <Badge appearance="tint" color="brand">
                  {Math.round(c.score)}% match
                </Badge>
                <Button
                  size="small"
                  appearance="primary"
                  disabled={spotsRemaining <= 0}
                  onClick={(e) => {
                    e.stopPropagation();
                    openRequestConfirm(c);
                  }}
                >
                  Request
                </Button>
              </div>
            ))}
          </div>

          <Button appearance="secondary" style={{ marginTop: tokens.spacingVerticalS }} onClick={() => setGalleryOpen(true)}>
            View all {otherCandidates.length} candidates
          </Button>
        </>
      )}

      <Dialog open={galleryOpen} onOpenChange={(_, data) => setGalleryOpen(data.open)}>
        <DialogSurface style={{ maxWidth: '95vw', width: '1100px' }}>
          <DialogBody>
            <DialogTitle>Eligible candidates for {projectName}</DialogTitle>
            <DialogContent>
              <div className={styles.fullGrid}>
                {otherCandidates.map((c) => (
                  <div key={c.candidateId} className={styles.fullCard} onClick={() => setSelectedCandidate(c)}>
                    <CandidateAvatar candidateId={c.candidateId} name={c.displayName} size={64} />
                    <Body1>{c.displayName}</Body1>
                    {c.school && <Caption1>{c.school}</Caption1>}
                    <Badge appearance="tint" color="brand">
                      {Math.round(c.score)}% match
                    </Badge>
                    {c.hasPendingAssignmentElsewhere && (
                      <Badge appearance="tint" color="warning" size="small">
                        Pending elsewhere
                      </Badge>
                    )}
                    <Button
                      size="small"
                      appearance="primary"
                      disabled={spotsRemaining <= 0}
                      onClick={(e) => {
                        e.stopPropagation();
                        openRequestConfirm(c);
                      }}
                    >
                      Request
                    </Button>
                  </div>
                ))}
              </div>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setGalleryOpen(false)}>
                Close
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <CandidateDetailDialog
        candidate={selectedCandidate}
        spotsRemaining={spotsRemaining}
        onClose={() => setSelectedCandidate(null)}
        onRequest={openRequestConfirm}
        isRequesting={requestMutation.isPending}
        onRecommend={handleRecommend}
        onReject={handleReject}
        isRecommending={recommendMutation.isPending}
        isRejecting={rejectMutation.isPending}
      />

      <RequestAssignmentConfirmDialog
        candidateName={pendingRequest?.displayName ?? null}
        projectName={projectName}
        open={pendingRequest !== null}
        isSubmitting={requestMutation.isPending}
        errorMessage={requestMutation.isError ? (requestMutation.error as Error).message : undefined}
        onConfirm={() => pendingRequest && requestMutation.mutate(pendingRequest.candidateId)}
        onCancel={() => setPendingRequest(null)}
      />
    </div>
  );
}
