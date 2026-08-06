import type { ReactNode } from 'react';
import { makeStyles, tokens, Subtitle1 } from '@fluentui/react-components';
import { RocketRegular } from '@fluentui/react-icons';
import { NavMenu } from './NavMenu';
import { Header } from './Header';

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
  brandIcon: {
    display: 'flex',
    fontSize: '24px',
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
          <div className={styles.brand}>
            <span className={styles.brandIcon} aria-hidden="true">
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
