import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Body1, Button, Caption1, Spinner, Textarea, makeStyles, tokens } from '@fluentui/react-components';
import { ChatRegular } from '@fluentui/react-icons';
import { useActiveRole } from '../../../auth/ActiveRoleContext';
import { addCommunityComment, getCommunityPostComments } from '../../../api/community';
import { AppUserAvatar } from '../../../components/AppUserAvatar';

const useStyles = makeStyles({
  list: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    marginTop: tokens.spacingVerticalS,
    paddingLeft: tokens.spacingHorizontalM,
    borderLeftWidth: '2px',
    borderLeftStyle: 'solid',
    borderLeftColor: tokens.colorNeutralStroke2,
  },
  comment: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    alignItems: 'flex-start',
  },
  commentHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXXS,
  },
  composerRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalS,
  },
});

/** Lazily fetched on expand (see PostCard's "View N comments" toggle) — comments no longer
 * ride along with every feed page, which is what let the feed query drop its Comments include. */
export function CommentList({ postId }: { postId: number }) {
  const styles = useStyles();
  const queryClient = useQueryClient();
  const { activeRole } = useActiveRole();
  const [draft, setDraft] = useState('');

  const { data: comments, isLoading } = useQuery({
    queryKey: ['community', 'posts', postId, 'comments'],
    queryFn: () => getCommunityPostComments(postId),
  });

  const commentMutation = useMutation({
    mutationFn: () => addCommunityComment(postId, { body: draft.trim(), activeRole: activeRole ?? undefined }),
    onSuccess: () => {
      setDraft('');
      queryClient.invalidateQueries({ queryKey: ['community', 'posts', postId, 'comments'] });
      // Broad prefix match — refreshes every feed page (any hashtag filter) so this post's
      // commentCount tile updates too.
      queryClient.invalidateQueries({ queryKey: ['community', 'posts'] });
    },
  });

  return (
    <div className={styles.list}>
      {isLoading && <Spinner size="tiny" label="Loading comments..." />}
      {comments?.map((comment) => (
        <div key={comment.communityCommentId} className={styles.comment}>
          <AppUserAvatar appUserId={comment.authorAppUserId} name={comment.authorName} size={24} />
          <div>
            <div className={styles.commentHeader}>
              <Caption1>
                <strong>{comment.authorName}</strong>
                {comment.authorRoleLabel ? ` · ${comment.authorRoleLabel}` : ''}
              </Caption1>
              {comment.authorTeamsLink && (
                <Button
                  appearance="subtle"
                  size="small"
                  icon={<ChatRegular />}
                  title="Message on Teams"
                  onClick={() => window.open(comment.authorTeamsLink, '_blank', 'noopener')}
                />
              )}
            </div>
            <Body1>{comment.body}</Body1>
          </div>
        </div>
      ))}
      <div className={styles.composerRow}>
        <Textarea
          value={draft}
          onChange={(_, data) => setDraft(data.value)}
          placeholder="Add a comment..."
          style={{ flexGrow: 1 }}
          resize="vertical"
        />
        <Button
          disabled={draft.trim().length === 0 || commentMutation.isPending}
          onClick={() => commentMutation.mutate()}
        >
          Post
        </Button>
      </div>
    </div>
  );
}
