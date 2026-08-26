import { makeStyles, tokens } from '@fluentui/react-components';

/**
 * The one signature accent this redesign spends deliberately — a warm "liftoff"
 * coral, echoing the product's own name/rocket mark. Reserved for community and
 * candidate celebratory moments (reactions, Win posts, streaks) and the brand
 * mark's hover state. Program Ops/Exec chrome stays on Fluent's stock
 * Communication Blue throughout; this never appears there.
 *
 * Split into background-safe and text-safe variants because the vibrant `flame`
 * itself fails WCAG AA (4.5:1) as foreground text/icon color against both a
 * white/near-white surface (~2.7:1) and its own subtle-tint badge background
 * (~2.4:1) — verified by contrast calculation, not assumed. Use `flame`/
 * `flameHover` only as a *background* fill (paired with `flameOnFlameText` for
 * any text on top of it); use `flameTextLight`/`flameTextDark` whenever the
 * accent itself is the foreground color against a light/dark subtle surface.
 */
export const signature = {
  flame: '#FF7A45',
  flameHover: '#E85A28',
  /** Text/icon color when placed directly ON a `flame`-filled surface — ~7.8:1. */
  flameOnFlameText: '#2B1400',
  /** Text/icon color when `flame` itself needs to read as foreground against a
   * light neutral or `flameSubtleLight` background — ~4.9:1. */
  flameTextLight: '#B34A1E',
  /** Text/icon color when the accent needs to read as foreground against a dark
   * neutral or `flameSubtleDark` background — ~6.7:1. */
  flameTextDark: '#FF9466',
  flameSubtleLight: '#FFF1EA',
  flameSubtleDark: '#3A2418',
};

/**
 * Shared hover/focus elevation for anything that's a card-shaped click target
 * (post cards, project cards, candidate cards, cohort cards). Flat and tonal at
 * rest — Fluent's own Card default — lifts on hover/focus-visible so the
 * interaction actually has a state, and respects prefers-reduced-motion.
 */
export const useSurfaceStyles = makeStyles({
  card: {
    borderRadius: tokens.borderRadiusXLarge,
  },
  interactive: {
    borderRadius: tokens.borderRadiusXLarge,
    cursor: 'pointer',
    transitionProperty: 'transform, box-shadow, border-color',
    transitionDuration: tokens.durationNormal,
    transitionTimingFunction: tokens.curveEasyEase,
    ':hover': {
      transform: 'translateY(-2px)',
      boxShadow: tokens.shadow8,
    },
    ':focus-visible': {
      transform: 'translateY(-2px)',
      boxShadow: tokens.shadow8,
      outlineStyle: 'solid',
      outlineWidth: '2px',
      outlineColor: tokens.colorBrandStroke1,
      outlineOffset: '2px',
    },
    '@media (prefers-reduced-motion: reduce)': {
      transitionProperty: 'none',
    },
  },
  fadeInUp: {
    animationDuration: tokens.durationSlower,
    animationTimingFunction: tokens.curveDecelerateMid,
    animationFillMode: 'backwards',
    animationName: {
      from: { opacity: 0, transform: 'translateY(6px)' },
      to: { opacity: 1, transform: 'translateY(0)' },
    },
    '@media (prefers-reduced-motion: reduce)': {
      animationName: 'none',
    },
  },
  /** The one playful "liftoff" micro-motion — a like-button pop, the brand-mark
   * hover, and similar single-shot celebratory moments. Not looped. */
  pop: {
    animationDuration: '260ms',
    animationTimingFunction: tokens.curveEasyEase,
    animationName: {
      '0%': { transform: 'scale(1)' },
      '45%': { transform: 'scale(1.3)' },
      '100%': { transform: 'scale(1)' },
    },
    '@media (prefers-reduced-motion: reduce)': {
      animationName: 'none',
    },
  },
});
