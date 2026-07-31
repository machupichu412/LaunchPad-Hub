import { authedFetch } from './authedFetch';
import type { CommunityCommentDto, CommunityPostDto, CreateCommunityCommentRequest, CreateCommunityPostRequest } from './types';

export async function getCommunityPosts(): Promise<CommunityPostDto[]> {
  const response = await authedFetch('/api/community/posts');
  if (!response.ok) throw new Error(`Failed to load community posts: ${response.status}`);
  return response.json() as Promise<CommunityPostDto[]>;
}

export async function createCommunityPost(request: CreateCommunityPostRequest): Promise<CommunityPostDto> {
  const response = await authedFetch('/api/community/posts', { method: 'POST', body: JSON.stringify(request) });
  if (!response.ok) throw new Error(`Failed to create post: ${response.status}`);
  return response.json() as Promise<CommunityPostDto>;
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
