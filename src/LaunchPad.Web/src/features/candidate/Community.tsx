import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Badge,
  Body1,
  Button,
  Caption1,
  Card,
  CardHeader,
  Select,
  Spinner,
  Textarea,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { HeartFilled, HeartRegular } from '@fluentui/react-icons';
import { addCommunityComment, createCommunityPost, getCommunityPosts, toggleCommunityReaction } from '../../api/community';
import { PageHeader } from '../../components/PageHeader';
import type { CommunityPostDto, CommunityPostType } from '../../api/types';

const useStyles = makeStyles({
  composer: {
    padding: tokens.spacingVerticalL,
    marginTop: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalXL,
  },
  composerRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalS,
  },
  feed: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  postCard: {
    padding: tokens.spacingVerticalL,
  },
  commentList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    marginTop: tokens.spacingVerticalS,
    paddingLeft: tokens.spacingHorizontalM,
    borderLeftWidth: '2px',
    borderLeftStyle: 'solid',
    borderLeftColor: tokens.colorNeutralStroke2,
  },
  actionsRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalS,
  },
});

function PostCard({ post }: { post: CommunityPostDto }) {
  const styles = useStyles();
  const queryClient = useQueryClient();
  const [commentDraft, setCommentDraft] = useState('');

  const reactMutation = useMutation({
    mutationFn: () => toggleCommunityReaction(post.communityPostId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['community', 'posts'] }),
  });

  const commentMutation = useMutation({
    mutationFn: () => addCommunityComment(post.communityPostId, { body: commentDraft.trim() }),
    onSuccess: () => {
      setCommentDraft('');
      queryClient.invalidateQueries({ queryKey: ['community', 'posts'] });
    },
  });

  return (
    <Card className={styles.postCard}>
      <CardHeader
        header={<Body1><strong>{post.authorName}</strong></Body1>}
        description={
          <Caption1>
            {post.authorRoleLabel ? `${post.authorRoleLabel} · ` : ''}
            {new Date(post.createdUtc).toLocaleString()}
          </Caption1>
        }
        action={<Badge appearance="tint">{post.postType}</Badge>}
      />
      <Body1>{post.body}</Body1>

      <div className={styles.actionsRow}>
        <Button
          size="small"
          appearance="subtle"
          icon={post.hasLikedByMe ? <HeartFilled /> : <HeartRegular />}
          disabled={reactMutation.isPending}
          onClick={() => reactMutation.mutate()}
        >
          {post.likeCount}
        </Button>
      </div>

      {post.comments.length > 0 && (
        <div className={styles.commentList}>
          {post.comments.map((comment) => (
            <div key={comment.communityCommentId}>
              <Caption1><strong>{comment.authorName}</strong></Caption1>
              <Body1>{comment.body}</Body1>
            </div>
          ))}
        </div>
      )}

      <div className={styles.composerRow}>
        <Textarea
          value={commentDraft}
          onChange={(_, data) => setCommentDraft(data.value)}
          placeholder="Add a comment..."
          style={{ flexGrow: 1 }}
          resize="vertical"
        />
        <Button
          disabled={commentDraft.trim().length === 0 || commentMutation.isPending}
          onClick={() => commentMutation.mutate()}
        >
          Post
        </Button>
      </div>
    </Card>
  );
}

export function Community() {
  const styles = useStyles();
  const queryClient = useQueryClient();

  const { data: posts, isLoading, isError, error } = useQuery({
    queryKey: ['community', 'posts'],
    queryFn: getCommunityPosts,
  });

  const [body, setBody] = useState('');
  const [postType, setPostType] = useState<CommunityPostType>('Win');

  const createMutation = useMutation({
    mutationFn: () => createCommunityPost({ body: body.trim(), postType }),
    onSuccess: () => {
      setBody('');
      queryClient.invalidateQueries({ queryKey: ['community', 'posts'] });
    },
  });

  return (
    <>
      <PageHeader title="Community" />

      <Card className={styles.composer}>
        <Textarea
          value={body}
          onChange={(_, data) => setBody(data.value)}
          placeholder="Share a win, ask a question, or post an update..."
          resize="vertical"
          style={{ width: '100%' }}
        />
        <div className={styles.composerRow}>
          <Select value={postType} onChange={(_, data) => setPostType(data.value as CommunityPostType)}>
            <option value="Win">Win</option>
            <option value="Question">Question</option>
            <option value="Announcement">Announcement</option>
            <option value="Kudos">Kudos</option>
            <option value="Reminder">Reminder</option>
          </Select>
          <Button
            appearance="primary"
            disabled={body.trim().length === 0 || createMutation.isPending}
            onClick={() => createMutation.mutate()}
          >
            {createMutation.isPending ? 'Posting...' : 'Post'}
          </Button>
        </div>
        {createMutation.isError && <Body1>Failed to post: {(createMutation.error as Error).message}</Body1>}
      </Card>

      {isLoading && <Spinner label="Loading the feed..." />}
      {isError && <Body1>Failed to load the feed: {(error as Error).message}</Body1>}
      {posts && posts.length === 0 && <Body1>Nothing here yet — be the first to post.</Body1>}
      {posts && posts.length > 0 && (
        <div className={styles.feed}>
          {posts.map((post) => (
            <PostCard key={post.communityPostId} post={post} />
          ))}
        </div>
      )}
    </>
  );
}
