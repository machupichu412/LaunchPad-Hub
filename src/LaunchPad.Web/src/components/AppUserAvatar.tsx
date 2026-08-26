import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Avatar, type AvatarProps } from '@fluentui/react-components';
import { getAppUserAvatarBlob } from '../api/appUsers';

/**
 * Any post/comment author's photo in the Community feed — Community spans every role
 * (Candidate/ProgramOps/Sponsor), so unlike CandidateAvatar this is keyed on the shared
 * AppUserId rather than a role-specific id. Same authenticated-object-URL pattern as
 * CandidateAvatar/useMyAvatarUrl (a bare <img src> can't attach the bearer token).
 */
export function AppUserAvatar({
  appUserId,
  name,
  size = 32,
}: {
  appUserId: number;
  name: string;
  size?: AvatarProps['size'];
}) {
  const { data: blob } = useQuery({
    queryKey: ['app-users', appUserId, 'avatar'],
    queryFn: () => getAppUserAvatarBlob(appUserId),
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

  return <Avatar aria-label={name} image={url ? { src: url } : undefined} size={size} />;
}
