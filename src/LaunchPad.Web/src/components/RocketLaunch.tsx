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
 * Shipped as an animated WebP with a real alpha channel, not the source MP4: the
 * footage was rendered on a near-white stage that's colorimetrically identical to
 * some of the rocket's own paint (the glass dome highlight, the hull edge), so a
 * plain color-key punches holes through the rocket. The served asset is instead
 * built by flood-filling transparency in from each frame's corners — the true
 * background is one region touching every edge; the dome highlight is fully
 * enclosed inside the rocket's outline and survives untouched (see
 * design/README.md for the full pipeline).
 *
 * A finite WebP loop count (not JS) makes it play once and hold on its last frame
 * — the footage's own tail end re-forms the full logo's rainbow trail, so ending
 * there rather than cycling is the resting state, not a truncation. Falls back to
 * that same last frame, as a plain transparent PNG, under prefers-reduced-motion.
 * Each mount replays from the first frame, which is what gives callers like
 * AppShell's hover-to-relaunch its restart — no imperative video API needed.
 */
export function RocketLaunch({ size, className }: { size: number; className?: string }) {
  const styles = useStyles();
  const reduceMotion = usePrefersReducedMotion();

  return (
    <img
      src={reduceMotion ? '/brand/rocket-launch-poster-end.png' : '/brand/rocket-launch.webp'}
      alt=""
      aria-hidden="true"
      width={size}
      height={size}
      className={mergeClasses(styles.media, className)}
    />
  );
}
