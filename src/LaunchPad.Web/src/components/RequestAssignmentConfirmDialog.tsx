import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Spinner,
  makeStyles,
  tokens,
} from '@fluentui/react-components';

const useStyles = makeStyles({
  errorText: {
    display: 'block',
    marginTop: tokens.spacingVerticalS,
    color: tokens.colorPaletteRedForeground1,
  },
});

/**
 * The one confirmation step before a sponsor's direct assignment request actually fires —
 * shared by both the condensed gallery's quick-request button and the full candidate detail
 * dialog, so there's exactly one place this copy/behavior lives.
 */
export function RequestAssignmentConfirmDialog({
  candidateName,
  projectName,
  open,
  isSubmitting,
  errorMessage,
  onConfirm,
  onCancel,
}: {
  candidateName: string | null;
  projectName: string;
  open: boolean;
  isSubmitting: boolean;
  errorMessage?: string;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const styles = useStyles();

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onCancel()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Request {candidateName}?</DialogTitle>
          <DialogContent>
            This sends a request to add {candidateName} to {projectName}. Program Ops still needs to
            give final approval before it's official.
            {errorMessage && <div className={styles.errorText}>{errorMessage}</div>}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onCancel} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button appearance="primary" onClick={onConfirm} disabled={isSubmitting}>
              {isSubmitting ? <Spinner size="tiny" /> : 'Send request'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
