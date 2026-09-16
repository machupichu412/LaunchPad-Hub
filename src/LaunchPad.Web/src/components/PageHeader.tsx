import type { ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import { Body1, Title1, makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { useSurfaceStyles } from '../theme/surfaces';
import { hueForPath, sectionHues } from '../theme/brand';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    alignItems: 'flex-start',
    justifyContent: 'space-between',
    gap: tokens.spacingHorizontalL,
    // Section rhythm: deliberately larger than the intra-card gaps used inside
    // page content below it — see theme/surfaces.ts's doc comment on the two
    // spacing registers this app uses.
    marginBottom: tokens.spacingVerticalXXL,
  },
  text: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  title: {
    letterSpacing: '-0.01em',
  },
  // The same 3px rule the nav uses for the active destination, repeated at the top of
  // the page it leads to. Two marks of the same color at either end of the click are
  // what turn a hue into wayfinding rather than paint — you can see where you landed
  // without reading anything. Candidate routes only; Ops and Exec keep stock Fluent.
  hueRule: {
    width: '32px',
    height: '3px',
    borderRadius: tokens.borderRadiusCircular,
    marginBottom: tokens.spacingVerticalS,
  },
  actions: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    flexShrink: 0,
    paddingTop: tokens.spacingVerticalXS,
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
  const surfaces = useSurfaceStyles();
  const { pathname } = useLocation();
  const hueName = hueForPath(pathname);

  return (
    <div className={mergeClasses(styles.root, surfaces.fadeInUp)}>
      <div className={styles.text}>
        {hueName && (
          <div
            className={styles.hueRule}
            style={{ backgroundColor: sectionHues[hueName].fill }}
            aria-hidden="true"
          />
        )}
        <Title1 block className={styles.title}>
          {title}
        </Title1>
        {subtitle && <Body1 block>{subtitle}</Body1>}
      </div>
      {actions && <div className={styles.actions}>{actions}</div>}
    </div>
  );
}
