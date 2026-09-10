import { Body1, Caption1, makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { BrandMark } from '../../components/BrandMark';
import { sectionHues, logo } from '../../theme/brand';
import { useThemeMode } from '../../theme/ThemeModeContext';
import type { AssignmentStatus } from '../../api/types';

/**
 * Where the candidate is in the program, drawn as the logo's own exhaust trail.
 *
 * The mark is a rocket that has travelled and left a spectrum behind it, and a cohort is
 * a journey with real milestones — so the trail does a job here rather than decorating
 * one. It is the single place in the app allowed to show the full spectrum; every other
 * surface gets one flat hue.
 *
 * Every stage maps to actual AssignmentStatus, so nothing here is inferred or padded to
 * make the track look fuller than the candidate's real position.
 */

const STAGES = [
  { key: 'profile', label: 'Profile ready', hue: 'azure' },
  { key: 'matched', label: 'Matched', hue: 'amber' },
  { key: 'underway', label: 'Underway', hue: 'coral' },
  { key: 'wrapped', label: 'Wrapped', hue: 'violet' },
] as const;

/** Index of the furthest stage reached, 0-3. */
function stageFor(status: AssignmentStatus | null): number {
  switch (status) {
    case null:
    case undefined:
      return 0;
    case 'Proposed':
    case 'SponsorApproved':
      return 1;
    case 'OpsApproved':
    case 'Active':
      return 2;
    case 'Completed':
      return 3;
    case 'Withdrawn':
      // Back in the pool, not further along. Showing this as progress would be a lie.
      return 0;
    default:
      return 0;
  }
}

/** Plain-language status. Says what is happening and, where there is one, what to do. */
function statusLine(status: AssignmentStatus | null, projectName: string | null): string {
  switch (status) {
    case 'Proposed':
      return 'Your match is with the sponsor for review.';
    case 'SponsorApproved':
      return 'Your sponsor said yes. Program Ops is confirming the placement.';
    case 'OpsApproved':
    case 'Active':
      return projectName ? `You're building ${projectName}.` : "You're on a project.";
    case 'Completed':
      return projectName ? `You wrapped ${projectName}.` : 'You wrapped your project.';
    case 'Withdrawn':
      return "That placement ended. You're back in the pool for the next round of matching.";
    default:
      return 'Rate projects in the marketplace — it helps matching find the right fit for you.';
  }
}

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    // Held to a readable measure rather than stretched across the page. Full-bleed, a
    // six-hue gradient stops reading as an instrument and starts reading as a loading bar.
    maxWidth: '560px',
  },
  trackArea: {
    position: 'relative',
    height: '10px',
    marginTop: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalXS,
  },
  track: {
    position: 'absolute',
    inset: 0,
    borderRadius: tokens.borderRadiusCircular,
  },
  fill: {
    position: 'absolute',
    inset: 0,
    borderRadius: tokens.borderRadiusCircular,
    // The trail's own order, cyan out through violet.
    backgroundImage: `linear-gradient(90deg, ${sectionHues.azure.fill} 0%, ${sectionHues.citron.fill} 26%, ${sectionHues.amber.fill} 46%, ${sectionHues.coral.fill} 66%, ${sectionHues.magenta.fill} 84%, ${sectionHues.violet.fill} 100%)`,
    // Always spans the whole track and is revealed by clipping, rather than being a
    // narrow element that stretches. Each hue therefore stays pinned to its own stage:
    // the trail is uncovered as the candidate advances, instead of a bar that recolors
    // itself every time progress changes.
    transitionProperty: 'clip-path',
    transitionDuration: '900ms',
    transitionTimingFunction: tokens.curveDecelerateMid,
    '@media (prefers-reduced-motion: reduce)': {
      transitionDuration: '1ms',
    },
  },
  rocket: {
    position: 'absolute',
    top: '50%',
    // The angled art is already tilted into its own flight path — riding the
    // leading edge of the trail needs only centering, no extra rotation on top.
    transform: 'translate(-50%, -50%)',
    transitionProperty: 'left',
    transitionDuration: '900ms',
    transitionTimingFunction: tokens.curveDecelerateMid,
    '@media (prefers-reduced-motion: reduce)': {
      transitionDuration: '1ms',
    },
  },
  stops: {
    display: 'grid',
    gridTemplateColumns: 'repeat(4, 1fr)',
    gap: tokens.spacingHorizontalXS,
  },
  stop: {
    display: 'flex',
    flexDirection: 'column',
    gap: '2px',
    // Each label sits under its own point on the track.
    alignItems: 'flex-start',
    ':last-child': {
      alignItems: 'flex-end',
      textAlign: 'right',
    },
  },
  stopLabel: {
    fontWeight: tokens.fontWeightSemibold,
  },
  stopLabelFuture: {
    color: tokens.colorNeutralForeground4,
    fontWeight: tokens.fontWeightRegular,
  },
  status: {
    color: tokens.colorNeutralForeground2,
  },
});

export function JourneyTrail({
  status,
  projectName,
}: {
  status: AssignmentStatus | null;
  projectName: string | null;
}) {
  const styles = useStyles();
  const { mode } = useThemeMode();
  const reached = stageFor(status);

  // Sits at the stage's own point on the track rather than filling to the end, so the
  // rocket has somewhere left to go until the journey is genuinely finished.
  const percent = (reached / (STAGES.length - 1)) * 100;

  return (
    <div className={styles.root}>
      <Body1 block className={styles.status}>
        {statusLine(status, projectName)}
      </Body1>

      <div
        className={styles.trackArea}
        role="group"
        aria-label={`Program progress: ${STAGES[reached].label}, stage ${reached + 1} of ${STAGES.length}`}
      >
        <div
          className={styles.track}
          style={{ backgroundColor: mode === 'dark' ? tokens.colorNeutralBackground4 : logo.mist }}
          aria-hidden="true"
        />
        <div
          className={styles.fill}
          style={{ clipPath: `inset(0 ${100 - percent}% 0 0 round 999px)` }}
          aria-hidden="true"
        />
        <span className={styles.rocket} style={{ left: `${percent}%` }} aria-hidden="true">
          <BrandMark size={34} orientation="angled" />
        </span>
      </div>

      <div className={styles.stops} aria-hidden="true">
        {STAGES.map((stage, index) => {
          const passed = index <= reached;
          const hue = sectionHues[stage.hue];
          return (
            <div key={stage.key} className={styles.stop}>
              <Caption1
                className={mergeClasses(styles.stopLabel, !passed && styles.stopLabelFuture)}
                style={passed ? { color: mode === 'dark' ? hue.inkDark : hue.ink } : undefined}
              >
                {stage.label}
              </Caption1>
            </div>
          );
        })}
      </div>

    </div>
  );
}
