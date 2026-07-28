import type { ReactNode } from 'react';
import { makeStyles, tokens, Title3 } from '@fluentui/react-components';
import { NavMenu } from './NavMenu';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    minHeight: '100vh',
  },
  nav: {
    width: '240px',
    flexShrink: 0,
    borderRightWidth: '1px',
    borderRightStyle: 'solid',
    borderRightColor: tokens.colorNeutralStroke2,
    padding: tokens.spacingHorizontalM,
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
      <nav className={styles.nav}>
        <Title3 as="h1">LaunchPad</Title3>
        <NavMenu />
      </nav>
      <main className={styles.main}>{children}</main>
    </div>
  );
}
