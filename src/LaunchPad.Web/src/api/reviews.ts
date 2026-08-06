import { authedFetch } from './authedFetch';
import type { SponsorReviewDto, SubmitReviewRequest } from './types';

export async function submitReview(request: SubmitReviewRequest): Promise<SponsorReviewDto> {
  const response = await authedFetch('/api/reviews', { method: 'POST', body: JSON.stringify(request) });
  if (!response.ok) throw new Error(`Failed to submit review: ${response.status}`);
  return response.json() as Promise<SponsorReviewDto>;
}

export async function getReviewsForAssignment(assignmentId: number): Promise<SponsorReviewDto[]> {
  const response = await authedFetch(`/api/reviews/assignment/${assignmentId}`);
  if (!response.ok) throw new Error(`Failed to load reviews: ${response.status}`);
  return response.json() as Promise<SponsorReviewDto[]>;
}
