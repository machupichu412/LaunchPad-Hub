import { useState, type ChangeEvent, type ReactElement } from 'react';
import AvatarEditor, { useAvatarEditor } from 'react-avatar-editor';
import {
  Body1,
  Button,
  Caption1,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  DialogTrigger,
  Slider,
  Spinner,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { uploadMyAvatar } from '../api/avatar';
import { useInvalidateMyAvatar } from '../auth/useMyAvatarUrl';

// The crop canvas's on-screen size doubles as its exported resolution (react-avatar-editor
// has no separate output-size knob) — 320px is a reasonable standard for a profile photo
// and keeps the dialog compact. The server independently caps uploads at 2 MB regardless
// (see MeController.UploadAvatar) — this isn't the only guard against an oversized image.
const EDITOR_SIZE = 320;
const MAX_PICKED_FILE_BYTES = 8 * 1024 * 1024;

const useStyles = makeStyles({
  body: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalM,
  },
  dropZone: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    gap: tokens.spacingVerticalXS,
    width: `${EDITOR_SIZE}px`,
    height: `${EDITOR_SIZE}px`,
    border: `${tokens.strokeWidthThick} dashed ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    cursor: 'pointer',
    textAlign: 'center',
    padding: tokens.spacingVerticalM,
  },
  sliderRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    width: `${EDITOR_SIZE}px`,
  },
  errorText: {
    display: 'block',
    color: tokens.colorPaletteRedForeground1,
  },
});

/**
 * The one place a profile photo gets picked, cropped, and saved — used from the
 * header's own Avatar (every role) and from candidate onboarding. `trigger` is
 * whatever clickable element should open it; deliberately not given
 * disableButtonEnhancement since triggers here (a bare Avatar) aren't already
 * interactive — Fluent's default DialogTrigger cloning adds the keyboard/focus
 * affordances a plain Avatar doesn't have on its own.
 */
export function AvatarEditorDialog({ trigger }: { trigger: ReactElement }) {
  const styles = useStyles();
  const invalidateAvatar = useInvalidateMyAvatar();
  const { ref, getImageScaledToCanvas } = useAvatarEditor();

  const [open, setOpen] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [scale, setScale] = useState(1);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | undefined>(undefined);

  const reset = () => {
    setSelectedFile(null);
    setScale(1);
    setError(undefined);
  };

  const handleFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    if (file.size > MAX_PICKED_FILE_BYTES) {
      setError('That photo is too large — please choose one under 8 MB.');
      return;
    }
    setError(undefined);
    setSelectedFile(file);
    setScale(1);
  };

  const handleSave = async () => {
    const canvas = getImageScaledToCanvas();
    if (!canvas) return;

    setIsSaving(true);
    setError(undefined);
    try {
      const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', 0.9));
      if (!blob) throw new Error('Could not process that image — try a different photo.');
      await uploadMyAvatar(blob);
      invalidateAvatar();
      setOpen(false);
      reset();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(_, data) => {
        setOpen(data.open);
        if (!data.open) reset();
      }}
    >
      <DialogTrigger>{trigger}</DialogTrigger>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Update your photo</DialogTitle>
          <DialogContent>
            <div className={styles.body}>
              {!selectedFile ? (
                <label className={styles.dropZone}>
                  <Body1>Click to choose a photo</Body1>
                  <Caption1>JPEG, PNG, or WebP — up to 8 MB</Caption1>
                  <input type="file" accept="image/jpeg,image/png,image/webp" hidden onChange={handleFileChange} />
                </label>
              ) : (
                <>
                  <AvatarEditor
                    ref={ref}
                    image={selectedFile}
                    width={EDITOR_SIZE}
                    height={EDITOR_SIZE}
                    border={20}
                    borderRadius={EDITOR_SIZE}
                    scale={scale}
                  />
                  <div className={styles.sliderRow}>
                    <Caption1>Zoom</Caption1>
                    <Slider
                      min={1}
                      max={3}
                      step={0.01}
                      value={scale}
                      onChange={(_, data) => setScale(data.value)}
                      style={{ flexGrow: 1 }}
                    />
                  </div>
                  <Button appearance="subtle" size="small" onClick={reset}>
                    Choose a different photo
                  </Button>
                </>
              )}
              {error && <Body1 className={styles.errorText}>{error}</Body1>}
            </div>
          </DialogContent>
          <DialogActions>
            <DialogTrigger disableButtonEnhancement>
              <Button appearance="secondary">Cancel</Button>
            </DialogTrigger>
            <Button appearance="primary" disabled={!selectedFile || isSaving} onClick={handleSave}>
              {isSaving ? <Spinner size="tiny" /> : 'Save'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
