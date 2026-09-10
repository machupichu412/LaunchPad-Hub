import { makeStyles, mergeClasses } from '@fluentui/react-components';

/**
 * The LaunchPad rocket, in the forms the brand actually has.
 *
 * `mark` is the rocket alone — it stays legible down to favicon sizes, so it's
 * what the nav and any other small chrome uses. `full` keeps the exhaust trail
 * and only earns its place where there's real room (sign-in, initial load).
 *
 * `mark` comes in two orientations, both dedicated isolated-rocket source art
 * (not cropped from the full logo's bounding box, which is what previously let
 * a sliver of white background sneak in): `vertical` is the rocket standing on
 * the pad, upright — the default, for static chrome like the nav icon and
 * favicons. `angled` is the same rocket tilted into its own flight path, for
 * anywhere it's already in motion, like riding the JourneyTrail.
 *
 * Sized in CSS by height, with width left to the asset's real aspect ratio —
 * neither orientation is square, and forcing one into a square box would pad
 * or squash it. Source of truth is design/Rocket.svg and design/Rocket
 * Vertical.svg (kept out of public/ — see design/README.md).
 */
const useStyles = makeStyles({
  img: {
    display: 'block',
    objectFit: 'contain',
    flexShrink: 0,
  },
});

const markSrc = {
  vertical: '/brand/launchpad-mark-96.png',
  angled: '/brand/launchpad-mark-angled-96.png',
} as const;

export function BrandMark({
  variant = 'mark',
  orientation = 'vertical',
  size,
  className,
}: {
  variant?: 'mark' | 'full';
  /** Only meaningful for `mark` — `full` always keeps the exhaust trail as drawn. */
  orientation?: 'vertical' | 'angled';
  /** Rendered height in px; width follows the asset's own aspect ratio. */
  size: number;
  className?: string;
}) {
  const styles = useStyles();
  const isMark = variant === 'mark';

  return (
    <img
      src={isMark ? markSrc[orientation] : '/brand/launchpad-logo-320.png'}
      // Decorative in every current use — the word "LaunchPad" is always rendered
      // as real text next to it, so naming the logo here would just repeat it.
      alt=""
      aria-hidden="true"
      height={size}
      className={mergeClasses(styles.img, className)}
      style={{ height: size, width: 'auto' }}
    />
  );
}
