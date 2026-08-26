import { useEffect, useState, type ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Badge, Body1, Button, Card, Caption1, makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { ChatRegular, HeartFilled, HeartRegular } from '@fluentui/react-icons';
import { getCommunityPostImageBlob, toggleCommunityReaction } from '../../../api/community';
import { AppUserAvatar } from '../../../components/AppUserAvatar';
import { formatRelativeTime } from '../../../utils/formatRelativeTime';
import { signature, useSurfaceStyles } from '../../../theme/surfaces';
import { useThemeMode } from '../../../theme/ThemeModeContext';
import { CommentList } from './CommentList';
import type { CommunityPostDto, CommunityPostType } from '../../../api/types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  headerText: {
    display: 'flex',
    flexDirection: 'column',
  },
  typeBadge: {
    marginLeft: 'auto',
  },
  // The signature "liftoff" accent, reserved for the two celebratory post
  // types — see theme/surfaces.ts. Every other post type stays on Fluent's
  // own preset Badge colors. Light/dark variants are picked in JS (below) —
  // Fluent's theme is a swapped JS token object, not a CSS class, so a plain
  // CSS selector can't branch on it.
  typeBadgeSignature: {
    marginLeft: 'auto',
  },
  image: {
    width: '100%',
    maxHeight: '420px',
    objectFit: 'cover',
    borderRadius: tokens.borderRadiusLarge,
    marginTop: tokens.spacingVerticalS,
  },
  hashtagButton: {
    background: tokens.colorBrandBackground2,
    border: 'none',
    borderRadius: tokens.borderRadiusCircular,
    padding: `1px ${tokens.spacingHorizontalSNudge}`,
    margin: '0 2px',
    color: tokens.colorBrandForeground2,
    fontWeight: tokens.fontWeightSemibold,
    cursor: 'pointer',
    font: 'inherit',
    transitionProperty: 'background-color',
    transitionDuration: tokens.durationFaster,
    ':hover': {
      backgroundColor: tokens.colorBrandBackground2Hover,
    },
  },
  actionsRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalXS,
  },
  likeIconPop: {
    display: 'inline-flex',
  },
});

// Win/Kudos are the celebratory moments this redesign's one signature accent is
// reserved for (see theme/surfaces.ts) — everything else uses Fluent's own
// preset Badge colors, deliberately restrained by comparison.
const SIGNATURE_POST_TYPES: CommunityPostType[] = ['Win', 'Kudos'];
const badgeColorByType: Partial<Record<CommunityPostType, 'informative' | 'brand' | 'warning'>> = {
  Question: 'informative',
  Announcement: 'brand',
  Reminder: 'warning',
};

// Same shape as the backend's HashtagExtractor — a '#' not preceded by a word character or
// another '#', followed by 1-50 word characters. Purely a display concern: this highlights
// hashtags straight from the post's own Body text (preserving the author's original casing)
// rather than reading anything back from the server.
const HASHTAG_PATTERN = /(?<![\w#])#([A-Za-z0-9_]{1,50})/g;

function renderBodyWithHashtags(body: string, className: string, onHashtagClick: (tag: string) => void): ReactNode[] {
  const pattern = new RegExp(HASHTAG_PATTERN);
  const parts: ReactNode[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;
  let key = 0;

  while ((match = pattern.exec(body)) !== null) {
    const tag = match[1];
    if (match.index > lastIndex) parts.push(body.slice(lastIndex, match.index));

    if (/[A-Za-z]/.test(tag)) {
      const lowerTag = tag.toLowerCase();
      parts.push(
        <button key={key++} type="button" className={className} onClick={() => onHashtagClick(lowerTag)}>
          #{tag}
        </button>,
      );
    } else {
      parts.push(match[0]);
    }
    lastIndex = pattern.lastIndex;
  }
  if (lastIndex < body.length) parts.push(body.slice(lastIndex));
  return parts;
}

export function PostCard({ post, onHashtagClick }: { post: CommunityPostDto; onHashtagClick: (tag: string) => void }) {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const { mode } = useThemeMode();
  const queryClient = useQueryClient();
  const [commentsExpanded, setCommentsExpanded] = useState(false);
  const [justLiked, setJustLiked] = useState(false);

  const reactMutation = useMutation({
    mutationFn: () => toggleCommunityReaction(post.communityPostId),
    onSuccess: (result) => {
      if (result.liked) setJustLiked(true);
      queryClient.invalidateQueries({ queryKey: ['community', 'posts'] });
    },
  });

  const isSignatureType = SIGNATURE_POST_TYPES.includes(post.postType);

  const { data: imageBlob } = useQuery({
    queryKey: ['community', 'posts', post.communityPostId, 'image'],
    queryFn: () => getCommunityPostImageBlob(post.communityPostId),
    enabled: post.hasImage,
  });

  const [imageUrl, setImageUrl] = useState<string | undefined>(undefined);
  useEffect(() => {
    if (!imageBlob) {
      setImageUrl(undefined);
      return;
    }
    const objectUrl = URL.createObjectURL(imageBlob);
    setImageUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [imageBlob]);

  return (
    <Card className={mergeClasses(styles.card, surfaces.card, surfaces.fadeInUp)}>
      <div className={styles.header}>
        <AppUserAvatar appUserId={post.authorAppUserId} name={post.authorName} />
        <div className={styles.headerText}>
          <Body1><strong>{post.authorName}</strong></Body1>
          <Caption1>
            {post.authorRoleLabel ? `${post.authorRoleLabel} · ` : ''}
            {formatRelativeTime(post.createdUtc)}
          </Caption1>
        </div>
        {isSignatureType ? (
          <Badge
            appearance="tint"
            className={styles.typeBadgeSignature}
            style={{
              backgroundColor: mode === 'dark' ? signature.flameSubtleDark : signature.flameSubtleLight,
              color: mode === 'dark' ? signature.flameTextDark : signature.flameTextLight,
            }}
          >
            {post.postType}
          </Badge>
        ) : (
          <Badge appearance="tint" color={badgeColorByType[post.postType]} className={styles.typeBadge}>
            {post.postType}
          </Badge>
        )}
        {post.authorTeamsLink && (
          <Button
            appearance="subtle"
            size="small"
            icon={<ChatRegular />}
            title="Message on Teams"
            onClick={() => window.open(post.authorTeamsLink, '_blank', 'noopener')}
          />
        )}
      </div>

      <Body1>{renderBodyWithHashtags(post.body, styles.hashtagButton, onHashtagClick)}</Body1>

      {post.hasImage && imageUrl && <img src={imageUrl} alt="" className={styles.image} />}

      <div className={styles.actionsRow}>
        <Button
          size="small"
          appearance="subtle"
          icon={
            <span
              className={justLiked ? mergeClasses(styles.likeIconPop, surfaces.pop) : styles.likeIconPop}
              onAnimationEnd={() => setJustLiked(false)}
              style={post.hasLikedByMe ? { color: mode === 'dark' ? signature.flameTextDark : signature.flameTextLight } : undefined}
            >
              {post.hasLikedByMe ? <HeartFilled /> : <HeartRegular />}
            </span>
          }
          disabled={reactMutation.isPending}
          onClick={() => reactMutation.mutate()}
        >
          {post.likeCount}
        </Button>
        <Button size="small" appearance="subtle" onClick={() => setCommentsExpanded((v) => !v)}>
          {post.commentCount === 0
            ? 'Comment'
            : commentsExpanded
              ? 'Hide comments'
              : post.commentCount === 1
                ? 'View 1 comment'
                : `View all ${post.commentCount} comments`}
        </Button>
      </div>

      {commentsExpanded && <CommentList postId={post.communityPostId} />}
    </Card>
  );
}
