import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Body1, Button, Subtitle1, makeStyles, tokens } from '@fluentui/react-components';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'flex-start',
    gap: tokens.spacingVerticalM,
    padding: tokens.spacingHorizontalXL,
    maxWidth: '640px',
  },
});

function ErrorFallback({ onRetry }: { onRetry: () => void }) {
  const styles = useStyles();

  return (
    <div className={styles.root} role="alert">
      <Subtitle1>Something went wrong on this page</Subtitle1>
      <Body1>
        The rest of the app is still working. Try again, or use the navigation to go somewhere else.
      </Body1>
      <Button appearance="primary" onClick={onRetry}>
        Try again
      </Button>
    </div>
  );
}

/**
 * Without this, any render-time throw unmounts the whole React tree and leaves a blank
 * white page with no navigation and nothing to click.
 *
 * Deliberately shows no error text: the message can carry API response bodies or internal
 * detail, and a user can act on "try again" but not on a stack trace. The real error goes
 * to the console (and to whatever collects it) instead.
 *
 * Still a class component because React has no hook equivalent of componentDidCatch.
 */
export class ErrorBoundary extends Component<{ children: ReactNode }, { hasError: boolean }> {
  state = { hasError: false };

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled render error:', error, info.componentStack);
  }

  render() {
    if (this.state.hasError) {
      // Clearing the flag re-renders the children; if the cause was transient (a bad
      // response already refetched) the page recovers without a full reload.
      return <ErrorFallback onRetry={() => this.setState({ hasError: false })} />;
    }

    return this.props.children;
  }
}
