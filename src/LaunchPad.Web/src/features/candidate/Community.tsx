import { useEffect, useRef, useState } from 'react';
import { useInfiniteQuery } from '@tanstack/react-query';
import { Body1, Spinner, makeStyles, tokens } from '@fluentui/react-components';
import { getCommunityFeedPage } from '../../api/community';
import { PageHeader } from '../../components/PageHeader';
import { PostComposer } from './community/PostComposer';
import { PostCard } from './community/PostCard';
import { HashtagFilterBar } from './community/HashtagFilterBar';

const useStyles = makeStyles({
  feed: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  sentinel: {
    display: 'flex',
    justifyContent: 'center',
    padding: tokens.spacingVerticalL,
  },
});

// Cursor-paginated, infinite-scrolling feed — see CommunityFeedCursor on the backend for why
// this is keyset pagination rather than offset. The sentinel div at the bottom triggers the
// next page load via IntersectionObserver once it scrolls into view.
export function Community() {
  const styles = useStyles();
  const [activeHashtag, setActiveHashtag] = useState<string | null>(null);
  const sentinelRef = useRef<HTMLDivElement>(null);

  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading, isError, error } = useInfiniteQuery({
    queryKey: ['community', 'posts', activeHashtag],
    queryFn: ({ pageParam }) => getCommunityFeedPage({ cursor: pageParam, hashtag: activeHashtag ?? undefined }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });

  useEffect(() => {
    const sentinel = sentinelRef.current;
    if (!sentinel) return;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting && hasNextPage && !isFetchingNextPage) {
          fetchNextPage();
        }
      },
      { rootMargin: '200px' },
    );
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [fetchNextPage, hasNextPage, isFetchingNextPage]);

  const posts = data?.pages.flatMap((page) => page.items) ?? [];

  return (
    <>
      <PageHeader title="Community" />

      <PostComposer />

      {activeHashtag && <HashtagFilterBar hashtag={activeHashtag} onClear={() => setActiveHashtag(null)} />}

      {isLoading && <Spinner label="Loading the feed..." />}
      {isError && <Body1>Failed to load the feed: {(error as Error).message}</Body1>}
      {!isLoading && posts.length === 0 && <Body1>Nothing here yet — be the first to post.</Body1>}

      {posts.length > 0 && (
        <div className={styles.feed}>
          {posts.map((post) => (
            <PostCard key={post.communityPostId} post={post} onHashtagClick={setActiveHashtag} />
          ))}
        </div>
      )}

      <div ref={sentinelRef} className={styles.sentinel}>
        {isFetchingNextPage && <Spinner size="tiny" label="Loading more..." />}
        {!hasNextPage && posts.length > 0 && <Body1>You're all caught up.</Body1>}
      </div>
    </>
  );
}
