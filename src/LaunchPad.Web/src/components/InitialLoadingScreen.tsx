import { Spinner, Title2, makeStyles, tokens } from '@fluentui/react-components';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: '100vh',
    gap: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground1,
  },
});

/**
 * Shown while MSAL resolves the redirect response and active account (see
 * main.tsx — that resolution is awaited before the real app mounts, so without
 * this the page is blank white for however long that takes, especially right
 * after the redirect back from Entra sign-in).
 */
export function InitialLoadingScreen() {
  const styles = useStyles();

  return (
    <div className={styles.root}>
      <Title2>LaunchPad</Title2>
      <Spinner size="huge" label="Signing you in..." labelPosition="below" />
    </div>
  );
}
