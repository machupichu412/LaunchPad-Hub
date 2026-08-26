import type { ReactNode } from 'react';
import { makeStyles, tokens, Subtitle1 } from '@fluentui/react-components';
import { RocketRegular } from '@fluentui/react-icons';
import { NavMenu } from './NavMenu';
import { Header } from './Header';
import { signature } from '../theme/surfaces';
import { useThemeMode } from '../theme/ThemeModeContext';

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
  // flourish. Split light/dark because the accent needs a different, contrast-
  // verified variant against each background (see surfaces.ts's doc comment) —
  // Fluent's theme is a swapped JS object, not a CSS class, so this can't branch
  // on the theme inside a single static rule.
  brandHoverLight: {
    ':hover .lp-brand-icon': {
      transform: 'translate(2px, -3px) rotate(-8deg)',
      color: signature.flameTextLight,
    },
  },
  brandHoverDark: {
    ':hover .lp-brand-icon': {
      transform: 'translate(2px, -3px) rotate(-8deg)',
      color: signature.flameTextDark,
    },
  },
  brandIcon: {
    display: 'flex',
    fontSize: '24px',
    transitionProperty: 'transform, color',
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
  const { mode } = useThemeMode();
  return (
    <div className={styles.root}>
      <Header />
      <div className={styles.body}>
        <nav className={styles.nav}>
          <div className={`${styles.brand} ${mode === 'dark' ? styles.brandHoverDark : styles.brandHoverLight}`}>
            <span className={`${styles.brandIcon} lp-brand-icon`} aria-hidden="true">
              <RocketRegular />
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
