import { useState, type ReactNode } from 'react';
import { makeStyles, mergeClasses, tokens, Subtitle1 } from '@fluentui/react-components';
import { NavMenu } from './NavMenu';
import { Header } from './Header';
import { BrandMark } from './BrandMark';
import { ROCKET_STAGE_COLOR, RocketLaunch } from './RocketLaunch';
import { usePrefersReducedMotion } from '../utils/usePrefersReducedMotion';

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
    // The rail is a fixed 244px at every width, which pushed the whole page sideways on
    // a phone — the content started off-screen and the body scrolled horizontally. Below
    // the breakpoint the shell stacks and the rail becomes a horizontal strip instead.
    '@media (max-width: 767px)': {
      flexDirection: 'column',
    },
  },
  nav: {
    width: '244px',
    flexShrink: 0,
    '@media (max-width: 767px)': {
      width: '100%',
      borderRightStyle: 'none',
      borderBottomWidth: tokens.strokeWidthThin,
      borderBottomStyle: 'solid',
      borderBottomColor: tokens.colorNeutralStroke2,
      // Keeps the strip from growing tall enough to bury the page under itself.
      position: 'sticky',
      top: 0,
      zIndex: 1,
    },
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
  // The one signature moment (see theme/surfaces.ts) — the rocket actually launches
  // on hover now, using the delivered 3D footage rather than a CSS nod at motion.
  brandIconSlot: {
    position: 'relative',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    width: '28px',
    height: '26px',
    flexShrink: 0,
  },
  brandIconStatic: {
    transitionProperty: 'opacity',
    transitionDuration: tokens.durationSlow,
    transitionTimingFunction: tokens.curveEasyEase,
  },
  brandIconStaticHidden: {
    opacity: 0,
  },
  // Same porthole treatment as InitialLoadingScreen, nav-scaled: the footage's stage
  // has no alpha to key out, so without this it shows up as a stray white rectangle
  // next to the icon — especially jarring in dark mode.
  brandIconPlate: {
    position: 'absolute',
    top: '50%',
    left: '50%',
    // Allowed to overflow its slot — the launch should read at nav-icon scale
    // without shifting the wordmark next to it.
    transform: 'translate(-42%, -55%)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    width: '44px',
    height: '44px',
    borderRadius: tokens.borderRadiusCircular,
    backgroundColor: ROCKET_STAGE_COLOR,
    boxShadow: tokens.shadow8,
    overflow: 'hidden',
    pointerEvents: 'none',
  },
  main: {
    flexGrow: 1,
    padding: tokens.spacingHorizontalXL,
    // Nothing inside a page should be able to widen the document itself; wide children
    // (tables, card grids) scroll within their own container instead.
    minWidth: 0,
    '@media (max-width: 767px)': {
      padding: tokens.spacingHorizontalM,
    },
  },
});

export function AppShell({ children }: { children: ReactNode }) {
  const styles = useStyles();
  const [isHovered, setIsHovered] = useState(false);
  const reduceMotion = usePrefersReducedMotion();
  const playLaunch = isHovered && !reduceMotion;

  return (
    <div className={styles.root}>
      <Header />
      <div className={styles.body}>
        <nav className={styles.nav}>
          <div
            className={styles.brand}
            onMouseEnter={() => setIsHovered(true)}
            onMouseLeave={() => setIsHovered(false)}
          >
            <span className={styles.brandIconSlot}>
              <BrandMark
                size={26}
                className={mergeClasses(styles.brandIconStatic, playLaunch && styles.brandIconStaticHidden)}
              />
              {playLaunch && (
                <div className={styles.brandIconPlate}>
                  <RocketLaunch size={40} />
                </div>
              )}
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
