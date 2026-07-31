import type { ReactNode } from 'react';
import { makeStyles, tokens, Title3 } from '@fluentui/react-components';
import { NavMenu } from './NavMenu';
import { Header } from './Header';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    minHeight: '100vh',
  },
  body: {
    display: 'flex',
    flexGrow: 1,
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
      <Header />
      <div className={styles.body}>
        <nav className={styles.nav}>
          <Title3 as="h1">LaunchPad</Title3>
          <NavMenu />
        </nav>
        <main className={styles.main}>{children}</main>
      </div>
    </div>
  );
}
