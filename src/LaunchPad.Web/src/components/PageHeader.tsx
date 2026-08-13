import type { ReactNode } from 'react';
import { Body1, Title2, makeStyles, tokens } from '@fluentui/react-components';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    alignItems: 'flex-start',
    justifyContent: 'space-between',
    gap: tokens.spacingHorizontalL,
    marginBottom: tokens.spacingVerticalL,
  },
  text: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  actions: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    flexShrink: 0,
    paddingTop: tokens.spacingVerticalXXS,
  },
});

/**
 * Fluent's typography primitives (Title2, Body1, ...) render `display: inline` by
 * default — nothing stacks them unless each one opts into `block` individually, so a
 * bare `<Title2>X</Title2><Body1>Y</Body1>` runs both onto one line instead of a
 * title with a subtitle underneath. This is the one place that gets it right; pages
 * should compose it for their top-of-page title instead of reaching for Title2/Body1
 * directly.
 */
export function PageHeader({
  title,
  subtitle,
  actions,
}: {
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
}) {
  const styles = useStyles();
  return (
    <div className={styles.root}>
      <div className={styles.text}>
        <Title2 block>{title}</Title2>
        {subtitle && <Body1 block>{subtitle}</Body1>}
      </div>
      {actions && <div className={styles.actions}>{actions}</div>}
    </div>
  );
}
