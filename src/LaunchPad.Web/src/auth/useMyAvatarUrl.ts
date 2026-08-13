import { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getMyAvatarBlob } from '../api/avatar';

/**
 * /api/me/avatar is an authenticated endpoint — a bare <img src="/api/me/avatar">
 * can't attach the bearer token, so this fetches the bytes via authedFetch (through
 * getMyAvatarBlob) and hands back an object URL instead. Revokes the previous URL
 * whenever a new one replaces it or the component unmounts, so this can't leak
 * blob: URLs across repeated uploads.
 */
export function useMyAvatarUrl() {
  const { data: blob, isLoading } = useQuery({
    queryKey: ['me', 'avatar'],
    queryFn: getMyAvatarBlob,
  });

  const [url, setUrl] = useState<string | undefined>(undefined);

  useEffect(() => {
    if (!blob) {
      setUrl(undefined);
      return;
    }
    const objectUrl = URL.createObjectURL(blob);
    setUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [blob]);

  return { url, isLoading };
}

/** Call after an upload/delete so every consumer of useMyAvatarUrl (Header,
 * Onboarding) refetches and picks up the new photo. */
export function useInvalidateMyAvatar() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: ['me', 'avatar'] });
}
