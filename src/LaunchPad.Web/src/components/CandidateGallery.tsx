import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
  Spinner,
  Title3,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { getEligibleCandidates, requestAssignment } from '../api/projects';
import { CandidateAvatar } from './CandidateAvatar';
import { CandidateDetailDialog } from './CandidateDetailDialog';
import { RequestAssignmentConfirmDialog } from './RequestAssignmentConfirmDialog';
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
});

/**
 * Sponsor's eligible-candidate browsing UI for one project: a condensed strip, expandable to
 * a full-screen "baseball card" gallery, each card clickable to CandidateDetailDialog. Only
 * ever queried while the project is Approved (both the server 400s otherwise and this
 * component itself doesn't fetch before then) — sponsors can't request before Ops signs off.
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
  const queryClient = useQueryClient();

  const { data: candidates, isLoading, isError } = useQuery({
    queryKey: ['projects', projectId, 'eligible-candidates'],
    queryFn: () => getEligibleCandidates(projectId),
    enabled: isApproved,
  });

  const [galleryOpen, setGalleryOpen] = useState(false);
  const [selectedCandidate, setSelectedCandidate] = useState<SponsorCandidateMatchDto | null>(null);
  const [pendingRequest, setPendingRequest] = useState<SponsorCandidateMatchDto | null>(null);

  const requestMutation = useMutation({
    mutationFn: (candidateId: number) => requestAssignment(projectId, candidateId),
    onSuccess: () => {
      setPendingRequest(null);
      setSelectedCandidate(null);
      queryClient.invalidateQueries({ queryKey: ['projects', projectId] });
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'eligible-candidates'] });
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'assigned-candidates'] });
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'matches'] });
    },
  });

  const openRequestConfirm = (candidate: SponsorCandidateMatchDto) => {
    setSelectedCandidate(null);
    setPendingRequest(candidate);
  };

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

  const condensed = candidates.slice(0, CONDENSED_COUNT);

  return (
    <div style={{ marginTop: tokens.spacingVerticalL }}>
      <Title3>Eligible candidates</Title3>
      <Caption1 style={{ display: 'block', marginBottom: tokens.spacingVerticalS }}>
        {spotsRemaining > 0 ? `${spotsRemaining} spot(s) open` : 'All spots filled'}
      </Caption1>

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
        View all {candidates.length} candidates
      </Button>

      <Dialog open={galleryOpen} onOpenChange={(_, data) => setGalleryOpen(data.open)}>
        <DialogSurface style={{ maxWidth: '95vw', width: '1100px' }}>
          <DialogBody>
            <DialogTitle>Eligible candidates for {projectName}</DialogTitle>
            <DialogContent>
              <div className={styles.fullGrid}>
                {candidates.map((c) => (
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
