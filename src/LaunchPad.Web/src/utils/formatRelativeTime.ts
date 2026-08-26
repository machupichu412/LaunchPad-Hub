/** "2h ago" / "3d ago" style relative timestamp for feed items — falls back to a plain
 * locale date once something is old enough that "ago" phrasing stops being useful. */
export function formatRelativeTime(isoDate: string): string {
  const then = new Date(isoDate).getTime();
  const now = Date.now();
  const diffSeconds = Math.max(0, Math.floor((now - then) / 1000));

  if (diffSeconds < 60) return 'just now';

  const diffMinutes = Math.floor(diffSeconds / 60);
  if (diffMinutes < 60) return `${diffMinutes}m ago`;

  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) return `${diffHours}h ago`;

  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 7) return `${diffDays}d ago`;

  return new Date(isoDate).toLocaleDateString();
}
