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
  },
  nav: {
    width: '244px',
    flexShrink: 0,
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
