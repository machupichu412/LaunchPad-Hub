import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { usePrefersReducedMotion } from '../utils/usePrefersReducedMotion';

const useStyles = makeStyles({
  media: {
    display: 'block',
    objectFit: 'contain',
  },
});

/**
 * The footage's own near-white stage colour. Every caller frames the footage in a
 * plate pinned to this exact value — see the component doc comment below.
 */
export const ROCKET_STAGE_COLOR = '#FCFCFA';

/**
 * The rocket mid-launch — the delivered 3D footage itself, not a static mark.
 *
 * The footage was rendered on its own near-white stage with no alpha channel, so
 * unlike every other brand asset it can't be keyed transparent and dropped onto a
 * themed background (see design/README.md). Callers are expected to frame it in
 * their own plate colored `ROCKET_STAGE_COLOR`, rather than placing it directly
 * against page background — otherwise the stage reads as a stray white box,
 * especially in dark mode.
 *
 * Plays once and holds on its last frame (no loop) — the footage's own tail end
 * re-forms the full logo's rainbow trail, so ending there rather than cycling
 * is the resting state, not a truncation. Falls back straight to that same last
 * frame under prefers-reduced-motion, same as every other motion in the app.
 */
export function RocketLaunch({ size, className }: { size: number; className?: string }) {
  const styles = useStyles();
  const reduceMotion = usePrefersReducedMotion();

  if (reduceMotion) {
    return (
      <img
        src="/brand/rocket-launch-poster-end.png"
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
      muted
      playsInline
      aria-hidden="true"
    />
  );
}
