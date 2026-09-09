import type { ReactNode } from 'react';
import { makeStyles, tokens, Subtitle1 } from '@fluentui/react-components';
import { NavMenu } from './NavMenu';
import { Header } from './Header';
import { BrandMark } from './BrandMark';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    minHeight: '100vh',
    backgroundColor: tokens.colorNeutralBackground1,
  },
  body: {
    display: 'flex',
    flexGrow: 1,
    // The rail is a fixed 244px at every width, which pushed the whole page sideways on
    // a phone — the content started off-screen and the body scrolled horizontally. Below
    // the breakpoint the shell stacks and the rail becomes a horizontal strip instead.
    '@media (max-width: 767px)': {
      flexDirection: 'column',
    },
  },
  nav: {
    width: '244px',
    flexShrink: 0,
    '@media (max-width: 767px)': {
      width: '100%',
      borderRightStyle: 'none',
      borderBottomWidth: tokens.strokeWidthThin,
      borderBottomStyle: 'solid',
      borderBottomColor: tokens.colorNeutralStroke2,
      // Keeps the strip from growing tall enough to bury the page under itself.
      position: 'sticky',
      top: 0,
      zIndex: 1,
    },
    backgroundColor: tokens.colorNeutralBackground3,
    borderRightWidth: tokens.strokeWidthThin,
    borderRightStyle: 'solid',
    borderRightColor: tokens.colorNeutralStroke2,
    padding: tokens.spacingHorizontalM,
  },
  brand: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    color: tokens.colorBrandForeground1,
  },
  // The one signature moment (see theme/surfaces.ts) — the rocket "launches" on
  // hover, a literal nod to the product's own name rather than a decorative
  // flourish. The logo carries its own colors now, so unlike the old Fluent-icon
  // version there's no per-theme accent to swap — one rule covers both themes.
  brandHover: {
    ':hover .lp-brand-icon': {
      transform: 'translate(2px, -3px) rotate(-8deg)',
    },
  },
  brandIcon: {
    display: 'flex',
    transitionProperty: 'transform',
    transitionDuration: tokens.durationSlow,
    transitionTimingFunction: tokens.curveEasyEase,
    '@media (prefers-reduced-motion: reduce)': {
      transitionProperty: 'none',
    },
  },
  main: {
    flexGrow: 1,
    padding: tokens.spacingHorizontalXL,
    // Nothing inside a page should be able to widen the document itself; wide children
    // (tables, card grids) scroll within their own container instead.
    minWidth: 0,
    '@media (max-width: 767px)': {
      padding: tokens.spacingHorizontalM,
    },
  },
});

export function AppShell({ children }: { children: ReactNode }) {
  const styles = useStyles();
  return (
    <div className={styles.root}>
      <Header />
      <div className={styles.body}>
        <nav className={styles.nav}>
          <div className={`${styles.brand} ${styles.brandHover}`}>
            <span className={`${styles.brandIcon} lp-brand-icon`}>
              <BrandMark size={26} />
            </span>
            <Subtitle1 as="h1">LaunchPad</Subtitle1>
          </div>
          <NavMenu />
        </nav>
        <main className={styles.main}>{children}</main>
      </div>
    </div>
  );
}
