import { authedFetch } from './authedFetch';

export async function uploadMyAvatar(image: Blob): Promise<void> {
  const response = await authedFetch('/api/me/avatar', {
    method: 'POST',
    headers: { 'Content-Type': image.type },
    body: image,
  });
  if (!response.ok) throw new Error(`Failed to upload photo: ${response.status}`);
}

/** null when the user has no photo set yet (server returns 404) — not an error. */
export async function getMyAvatarBlob(): Promise<Blob | null> {
  const response = await authedFetch('/api/me/avatar');
  // 204 is "no photo set", which is the common case and not a failure. 404 is still
  // accepted so a client running against an older API keeps working.
  if (response.status === 204 || response.status === 404) return null;
  if (!response.ok) throw new Error(`Failed to load photo: ${response.status}`);
  return response.blob();
}

export async function deleteMyAvatar(): Promise<void> {
  const response = await authedFetch('/api/me/avatar', { method: 'DELETE' });
  if (!response.ok) throw new Error(`Failed to remove photo: ${response.status}`);
}
