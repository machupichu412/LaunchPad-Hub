import { Spinner, Title2, makeStyles, tokens } from '@fluentui/react-components';
import { ROCKET_STAGE_COLOR, RocketLaunch } from './RocketLaunch';

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
  porthole: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    width: '176px',
    height: '176px',
    flexShrink: 0,
    borderRadius: tokens.borderRadiusCircular,
    // Fixed rather than a theme token: the footage's own stage is a near-white that
    // was never rendered with alpha to key out (see RocketLaunch), so the plate is
    // pinned to match it exactly in both light and dark mode instead of showing a seam.
    backgroundColor: ROCKET_STAGE_COLOR,
    boxShadow: tokens.shadow16,
    overflow: 'hidden',
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
      <div className={styles.porthole}>
        <RocketLaunch size={168} />
      </div>
      <Title2>LaunchPad</Title2>
      <Spinner size="huge" label="Signing you in..." labelPosition="below" />
    </div>
  );
}
