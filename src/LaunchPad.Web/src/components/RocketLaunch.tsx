import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { usePrefersReducedMotion } from '../utils/usePrefersReducedMotion';

const useStyles = makeStyles({
  media: {
    display: 'block',
    objectFit: 'contain',
  },
});

/**
 * The rocket mid-launch — the delivered 3D footage itself, not a static mark.
 *
 * The footage was rendered on its own near-white stage with no alpha channel, so
 * unlike every other brand asset it can't be keyed transparent and dropped onto a
 * themed background (see design/README.md). Callers are expected to frame it in
 * their own plate that matches the footage's stage colour, rather than placing it
 * directly against page background.
 *
 * Falls back to a single still frame under prefers-reduced-motion, same as every
 * other motion in the app.
 */
export function RocketLaunch({ size, className }: { size: number; className?: string }) {
  const styles = useStyles();
  const reduceMotion = usePrefersReducedMotion();

  if (reduceMotion) {
    return (
      <img
        src="/brand/rocket-launch-poster.png"
        alt=""
        aria-hidden="true"
        width={size}
        height={size}
        className={mergeClasses(styles.media, className)}
      />
    );
  }

  return (
    <video
      width={size}
      height={size}
      className={mergeClasses(styles.media, className)}
      src="/brand/rocket-launch.mp4"
      poster="/brand/rocket-launch-poster.png"
      autoPlay
      loop
      muted
      playsInline
      aria-hidden="true"
    />
  );
}
