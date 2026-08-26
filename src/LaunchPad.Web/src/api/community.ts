import { authedFetch } from './authedFetch';
import type {
  CommunityCommentDto,
  CommunityFeedPageDto,
  CommunityPostType,
  CreateCommunityCommentRequest,
} from './types';

/** Cursor-paginated feed page — see useInfiniteQuery usage in Community.tsx. cursor is
 * opaque (the previous page's nextCursor); omit it for the first page. */
export async function getCommunityFeedPage(params: {
  cursor?: string;
  pageSize?: number;
  hashtag?: string;
}): Promise<CommunityFeedPageDto> {
  const query = new URLSearchParams();
  if (params.cursor) query.set('cursor', params.cursor);
  if (params.pageSize) query.set('pageSize', String(params.pageSize));
  if (params.hashtag) query.set('hashtag', params.hashtag);

  const response = await authedFetch(`/api/community/posts?${query.toString()}`);
  if (!response.ok) throw new Error(`Failed to load community posts: ${response.status}`);
  return response.json() as Promise<CommunityFeedPageDto>;
}

export async function getCommunityPostComments(postId: number): Promise<CommunityCommentDto[]> {
  const response = await authedFetch(`/api/community/posts/${postId}/comments`);
  if (!response.ok) throw new Error(`Failed to load comments: ${response.status}`);
  return response.json() as Promise<CommunityCommentDto[]>;
}

export async function createCommunityPost(params: {
  body: string;
  postType: CommunityPostType;
  image: File | null;
  /** The role the caller is currently viewing as (see useActiveRole) — recorded as the
   * post's author role label. The server only trusts this if it's actually one of the
   * caller's own held roles. */
  activeRole?: string;
}): Promise<void> {
  const formData = new FormData();
  formData.set('body', params.body);
  formData.set('postType', params.postType);
  if (params.activeRole) formData.set('activeRole', params.activeRole);
  if (params.image) formData.set('image', params.image);

  // No explicit Content-Type here — authedFetch skips its JSON default for a FormData
  // body so the browser can set its own multipart boundary.
  const response = await authedFetch('/api/community/posts', { method: 'POST', body: formData });
  if (!response.ok) throw new Error(`Failed to create post: ${response.status}`);
}

export async function addCommunityComment(
  postId: number,
  request: CreateCommunityCommentRequest,
): Promise<CommunityCommentDto> {
  const response = await authedFetch(`/api/community/posts/${postId}/comments`, {
    method: 'POST',
    body: JSON.stringify(request),
  });
  if (!response.ok) throw new Error(`Failed to add comment: ${response.status}`);
  return response.json() as Promise<CommunityCommentDto>;
}

export async function toggleCommunityReaction(postId: number): Promise<{ liked: boolean }> {
  const response = await authedFetch(`/api/community/posts/${postId}/reactions`, { method: 'POST' });
  if (!response.ok) throw new Error(`Failed to react to post: ${response.status}`);
  return response.json() as Promise<{ liked: boolean }>;
}

/** Fetches a post's image as an authenticated blob — mirrors getCandidateAvatarBlob's shape. */
export async function getCommunityPostImageBlob(postId: number): Promise<Blob | null> {
  const response = await authedFetch(`/api/community/posts/${postId}/image`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Failed to load post image: ${response.status}`);
  return response.blob();
}
