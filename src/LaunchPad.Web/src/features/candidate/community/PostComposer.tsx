import { useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Body1, Button, Card, Select, Textarea, makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { DismissRegular, ImageAddRegular } from '@fluentui/react-icons';
import { useActiveRole } from '../../../auth/ActiveRoleContext';
import { createCommunityPost } from '../../../api/community';
import { signature, useSurfaceStyles } from '../../../theme/surfaces';
import type { CommunityPostType } from '../../../api/types';

const useStyles = makeStyles({
  composer: {
    padding: tokens.spacingVerticalL,
    marginTop: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalXL,
  },
  // The composer is where a candidate starts a "launch" moment — the one place
  // outside the like button and Win/Kudos badges that carries the signature
  // accent (see theme/surfaces.ts), as the primary call to action.
  postButton: {
    backgroundColor: signature.flame,
    border: `1px solid ${signature.flame}`,
    color: signature.flameOnFlameText,
    ':hover': {
      backgroundColor: signature.flameHover,
      border: `1px solid ${signature.flameHover}`,
      color: signature.flameOnFlameText,
    },
  },
  row: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalS,
    flexWrap: 'wrap',
  },
  preview: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
    marginTop: tokens.spacingVerticalS,
  },
  previewImage: {
    maxHeight: '80px',
    borderRadius: tokens.borderRadiusMedium,
  },
  hiddenFileInput: {
    display: 'none',
  },
});

export function PostComposer() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const queryClient = useQueryClient();
  const { activeRole } = useActiveRole();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [body, setBody] = useState('');
  const [postType, setPostType] = useState<CommunityPostType>('Win');
  const [image, setImage] = useState<File | null>(null);
  const [imagePreviewUrl, setImagePreviewUrl] = useState<string | null>(null);

  const createMutation = useMutation({
    mutationFn: () => createCommunityPost({ body: body.trim(), postType, image, activeRole: activeRole ?? undefined }),
    onSuccess: () => {
      setBody('');
      setPostType('Win');
      clearImage();
      queryClient.invalidateQueries({ queryKey: ['community', 'posts'] });
    },
  });

  function handleImageChange(file: File | null) {
    if (imagePreviewUrl) URL.revokeObjectURL(imagePreviewUrl);
    setImage(file);
    setImagePreviewUrl(file ? URL.createObjectURL(file) : null);
  }

  function clearImage() {
    handleImageChange(null);
    if (fileInputRef.current) fileInputRef.current.value = '';
  }

  return (
    <Card className={mergeClasses(styles.composer, surfaces.card)}>
      <Textarea
        value={body}
        onChange={(_, data) => setBody(data.value)}
        placeholder="Share a win, ask a question, or post an update... use #hashtags to tag it"
        resize="vertical"
        style={{ width: '100%' }}
      />
      {imagePreviewUrl && (
        <div className={styles.preview}>
          <img src={imagePreviewUrl} alt="Selected upload preview" className={styles.previewImage} />
          <Button appearance="subtle" size="small" icon={<DismissRegular />} onClick={clearImage}>
            Remove
          </Button>
        </div>
      )}
      <div className={styles.row}>
        <Select value={postType} onChange={(_, data) => setPostType(data.value as CommunityPostType)}>
          <option value="Win">Win</option>
          <option value="Question">Question</option>
          <option value="Announcement">Announcement</option>
          <option value="Kudos">Kudos</option>
          <option value="Reminder">Reminder</option>
        </Select>
        <input
          ref={fileInputRef}
          type="file"
          className={styles.hiddenFileInput}
          accept="image/jpeg,image/png,image/webp"
          onChange={(e) => handleImageChange(e.target.files?.[0] ?? null)}
        />
        <Button
          appearance="secondary"
          icon={<ImageAddRegular />}
          onClick={() => fileInputRef.current?.click()}
        >
          {image ? 'Change image' : 'Add image'}
        </Button>
        <Button
          appearance="primary"
          className={styles.postButton}
          disabled={body.trim().length === 0 || createMutation.isPending}
          onClick={() => createMutation.mutate()}
        >
          {createMutation.isPending ? 'Posting...' : 'Post'}
        </Button>
      </div>
      {createMutation.isError && <Body1>Failed to post: {(createMutation.error as Error).message}</Body1>}
    </Card>
  );
}
