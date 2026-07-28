import { useMsal } from '@azure/msal-react';
import { Button, Title2, makeStyles, tokens } from '@fluentui/react-components';
import { apiRequest } from '../auth/msalConfig';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: '100vh',
    gap: tokens.spacingVerticalM,
  },
});

export function SignInPrompt() {
  const styles = useStyles();
  const { instance } = useMsal();

  return (
    <div className={styles.root}>
      <Title2>LaunchPad</Title2>
      <Button appearance="primary" onClick={() => instance.loginRedirect(apiRequest)}>
        Sign in
      </Button>
    </div>
  );
}
