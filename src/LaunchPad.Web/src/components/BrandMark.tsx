import { makeStyles, mergeClasses } from '@fluentui/react-components';

/**
 * The LaunchPad rocket, in the two forms the brand actually has.
 *
 * `mark` is the rocket alone — it stays legible down to favicon sizes, so it's
 * what the nav and any other small chrome uses. `full` keeps the exhaust trail
 * and only earns its place where there's real room (sign-in, initial load).
 *
 * Sized in CSS rather than by picking a file per size: the assets are 96px and
 * 320px so they still resolve sharply on a 2–4x display at the sizes used here.
 * Source of truth for both is design/launchpad-logo.svg (kept out of public/
 * so the print-scale exports aren't published with the site — see design/README.md).
 */
const useStyles = makeStyles({
  img: {
    display: 'block',
    objectFit: 'contain',
    flexShrink: 0,
  },
});

export function BrandMark({
  variant = 'mark',
  size,
  className,
}: {
  variant?: 'mark' | 'full';
  /** Rendered height in px. The mark is square; the full logo keeps its ratio. */
  size: number;
  className?: string;
}) {
  const styles = useStyles();
  const isMark = variant === 'mark';

  return (
    <img
      src={isMark ? '/brand/launchpad-mark-96.png' : '/brand/launchpad-logo-320.png'}
      // Decorative in every current use — the word "LaunchPad" is always rendered
      // as real text next to it, so naming the logo here would just repeat it.
      alt=""
      aria-hidden="true"
      height={size}
      width={isMark ? size : undefined}
      className={mergeClasses(styles.img, className)}
      style={isMark ? undefined : { height: size, width: 'auto' }}
    />
  );
}
