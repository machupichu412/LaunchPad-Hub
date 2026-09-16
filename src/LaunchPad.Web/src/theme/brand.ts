import { createDarkTheme, createLightTheme, type BrandVariants, type Theme } from '@fluentui/react-components';

/**
 * The palette, sampled from design/launchpad-logo.svg rather than invented.
 *
 * The mark is a rocket that has *travelled* — its exhaust trail sweeps cyan through
 * green, gold, coral and pink to violet. Those six hues are the app's color vocabulary,
 * and `mist`/`frost` are the body's own shading, which is a cool lavender grey rather
 * than a neutral one. Nudging surfaces toward them is what makes the chrome agree with
 * the mark without painting a single gradient on it.
 */
export const logo = {
  /** The porthole — the focal point of the mark, and the app's primary accent. */
  azure: '#20A3E9',
  citron: '#C8C648',
  amber: '#F3B216',
  /** The rocket body and fins. */
  coral: '#EA6753',
  magenta: '#DF579E',
  violet: '#AD57E0',
  /** Body shading: a lavender-tinted grey, not a neutral one. */
  mist: '#DCDDEE',
  frost: '#F3F5F9',
} as const;

/**
 * Brand ramp generated from the logo's porthole azure, which sits at shade 100.
 *
 * It is deliberately *not* at shade 80. Fluent maps 80 to colorBrandBackground,
 * colorBrandForeground1 and colorBrandForegroundLink — and #20A3E9 measures 2.81:1 on
 * white, so putting it there would make every brand-colored label and link fail AA.
 * Shade 80 is a darkened azure at 5.20:1 instead; the logo's own azure still shows up
 * untouched as fills and as dark-mode foreground (5.18:1 on Fluent's dark surface).
 *
 * Ratios verified by calculation, not by eye.
 */
export const launchPadBrand: BrandVariants = {
  10: '#041019',
  20: '#061B29',
  30: '#072C43',
  40: '#08395A',
  50: '#094771',
  60: '#0A5588',
  70: '#0A639E',
  80: '#0A72AF',
  90: '#1284C4',
  100: '#20A3E9',
  110: '#45B4EE',
  120: '#6AC4F2',
  130: '#8ED3F6',
  140: '#B0E1F9',
  150: '#D0EDFC',
  160: '#E9F6FE',
};

/**
 * One hue per candidate destination, taken in the trail's own order.
 *
 * This is wayfinding, not decoration: eight sibling screens built from the same grey
 * Fluent parts are hard to tell apart at a glance, and a consistent hue per destination
 * is the cheapest way to answer "where am I". Ops and Executive screens deliberately
 * keep stock Fluent blue — the color story belongs to the candidate experience.
 *
 * Four variants each, because the trail is a *light* palette and most of it is
 * unreadable as text:
 *  - `fill`    the logo value. Backgrounds and large graphics only.
 *  - `onFill`  text placed on that fill. Every hue here wants dark text.
 *  - `ink`     darkened until it clears 4.5:1 on white — anything thin or textual.
 *  - `inkDark` lightened until it clears 4.5:1 on Fluent's dark surface.
 *
 * Every `ink`/`inkDark` value was solved for, not picked: see the derivation in the PR.
 */
export type SectionHue = {
  fill: string;
  onFill: string;
  ink: string;
  inkDark: string;
};

export const sectionHues = {
  azure: { fill: '#20A3E9', onFill: '#1A1A1A', ink: '#127CB5', inkDark: '#20A3E9' },
  citron: { fill: '#C8C648', onFill: '#1A1A1A', ink: '#7B7A25', inkDark: '#C8C648' },
  amber: { fill: '#F3B216', onFill: '#1A1A1A', ink: '#966C08', inkDark: '#F3B216' },
  coral: { fill: '#EA6753', onFill: '#1A1A1A', ink: '#DB341B', inkDark: '#EA6753' },
  magenta: { fill: '#DF579E', onFill: '#1A1A1A', ink: '#D72C85', inkDark: '#E164A5' },
  violet: { fill: '#AD57E0', onFill: '#1A1A1A', ink: '#A546DD', inkDark: '#BA71E5' },
} as const satisfies Record<string, SectionHue>;

export type SectionHueName = keyof typeof sectionHues;

/** Candidate destinations, each mapped to the hue that identifies it everywhere. */
export const candidateSectionHue: Record<string, SectionHueName> = {
  '/dashboard': 'azure',
  '/profile': 'violet',
  '/marketplace': 'violet',
  '/assignments': 'coral',
  '/tasks': 'amber',
  '/deliverables': 'citron',
  '/evaluations': 'magenta',
  '/community': 'azure',
};

export function hueForPath(pathname: string): SectionHueName | null {
  const match = Object.keys(candidateSectionHue)
    .filter((path) => pathname === path || pathname.startsWith(`${path}/`))
    .sort((a, b) => b.length - a.length)[0];
  return match ? candidateSectionHue[match] : null;
}

/**
 * The trail, in the order it leaves the rocket. Used by the journey indicator and
 * nowhere else — it is the one place the full spectrum is allowed to appear.
 */
export const trailOrder: readonly SectionHueName[] = ['azure', 'citron', 'amber', 'coral', 'magenta', 'violet'];

export const launchPadLightTheme: Theme = {
  ...createLightTheme(launchPadBrand),
  // The logo's own body shading, standing in for Fluent's pure greys. A ~2% lavender
  // cast: invisible named side by side, but it stops the chrome reading as a different
  // brand from the mark sitting in the corner of it.
  colorNeutralBackground2: logo.frost,
  colorNeutralBackground3: logo.frost,
  colorNeutralStroke2: logo.mist,
};

export const launchPadDarkTheme: Theme = createDarkTheme(launchPadBrand);
