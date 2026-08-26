import { authedFetch } from './authedFetch';

/** null when the user has no photo set yet (server returns 404) — not an error. Mirrors
 * getMyAvatarBlob/getCandidateAvatarBlob's shape, generalized to any AppUserId. */
export async function getAppUserAvatarBlob(appUserId: number): Promise<Blob | null> {
  const response = await authedFetch(`/api/app-users/${appUserId}/avatar`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Failed to load photo for user ${appUserId}: ${response.status}`);
  return response.blob();
}
