import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Avatar, type AvatarProps } from '@fluentui/react-components';
import { getCandidateAvatarBlob } from '../api/candidates';

/**
 * A candidate's photo on a roster/browse card ("baseball card") — TalentPipeline,
 * MyCandidates. Fetched via the authenticated /api/candidates/{id}/avatar proxy,
 * same reasoning as useMyAvatarUrl (a bare <img src> can't attach the bearer
 * token): turn the response into an object URL, revoked on cleanup.
 *
 * Deliberately never passes `name` to the underlying Avatar — Fluent computes
 * initials from `name` whenever there's no image, and the ask here is specifically
 * a generic icon fallback, not initials. `name` is still used for the accessible
 * label via aria-label.
 */
export function CandidateAvatar({
  candidateId,
  name,
  size = 48,
}: {
  candidateId: number;
  name: string;
  size?: AvatarProps['size'];
}) {
  const { data: blob } = useQuery({
    queryKey: ['candidates', candidateId, 'avatar'],
    queryFn: () => getCandidateAvatarBlob(candidateId),
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
