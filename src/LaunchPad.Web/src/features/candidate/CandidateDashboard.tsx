import { useQuery } from '@tanstack/react-query';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import {
  Badge,
  Body1,
  Button,
  Caption1,
  Card,
  ProgressBar,
  Spinner,

  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { useMsal } from '@azure/msal-react';
import { getActivePersona } from '../../dev/devPersonas';
import { ArrowRightRegular, CalendarLtrRegular } from '@fluentui/react-icons';
import { getMyCandidateDashboard } from '../../api/candidates';
import { getAssignmentTodos } from '../../api/assignments';
import { useSurfaceStyles } from '../../theme/surfaces';
import { sectionHues } from '../../theme/brand';
import { useThemeMode } from '../../theme/ThemeModeContext';
import { JourneyTrail } from './JourneyTrail';

const useStyles = makeStyles({
  hero: {
    padding: `${tokens.spacingVerticalXL} ${tokens.spacingHorizontalXL} ${tokens.spacingVerticalL}`,
    '@media (max-width: 767px)': {
      padding: tokens.spacingHorizontalL,
    },
    borderRadius: tokens.borderRadiusXLarge,
    backgroundColor: tokens.colorNeutralBackground1,
    border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    marginBottom: tokens.spacingVerticalXL,
  },
  greeting: {
    display: 'block',
    // The one display moment per screen. Segoe's own optical-size range, pushed to a
    // size and tracking the Fluent ramp doesn't offer, rather than an imported face
    // that would stop this looking like a Microsoft product.
    // Scales with the viewport instead of overflowing a phone at a fixed 40px.
    fontSize: 'clamp(28px, 5vw, 40px)',
    lineHeight: 1.15,
    fontWeight: tokens.fontWeightSemibold,
    letterSpacing: '-0.02em',
    marginBottom: tokens.spacingVerticalM,
  },
  columns: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
    gap: tokens.spacingHorizontalL,
    marginBottom: tokens.spacingVerticalXL,
  },
  card: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    alignItems: 'stretch',
  },
  eyebrow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
    textTransform: 'uppercase',
    letterSpacing: '0.06em',
    fontWeight: tokens.fontWeightSemibold,
  },
  eyebrowDot: {
    width: '8px',
    height: '8px',
    borderRadius: tokens.borderRadiusCircular,
    flexShrink: 0,
  },
  taskTitle: {
    display: 'block',
    fontSize: tokens.fontSizeBase500,
    lineHeight: tokens.lineHeightBase500,
    fontWeight: tokens.fontWeightSemibold,
  },
  meta: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
    color: tokens.colorNeutralForeground2,
  },
  spacer: {
    flexGrow: 1,
  },
  action: {
    alignSelf: 'flex-start',
  },
  progressRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'baseline',
    marginBottom: tokens.spacingVerticalXS,
  },
  figure: {
    fontVariantNumeric: 'tabular-nums',
    fontWeight: tokens.fontWeightSemibold,
  },
  skillRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexWrap: 'wrap',
    marginTop: tokens.spacingVerticalXS,
  },
  asideRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXL,
    flexWrap: 'wrap',
    paddingTop: tokens.spacingVerticalM,
    borderTop: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
  },
  aside: {
    display: 'flex',
    flexDirection: 'column',
  },
  asideValue: {
    fontSize: tokens.fontSizeBase400,
    fontWeight: tokens.fontWeightSemibold,
    fontVariantNumeric: 'tabular-nums',
  },
});

function greetingFor(date: Date): string {
  const hour = date.getHours();
  if (hour < 12) return 'Good morning';
  if (hour < 18) return 'Good afternoon';
  return 'Good evening';
}

/** Eyebrow label carrying the hue of wherever the card leads. */
function Eyebrow({ hue, children }: { hue: keyof typeof sectionHues; children: string }) {
  const styles = useStyles();
  const { mode } = useThemeMode();
  const color = mode === 'dark' ? sectionHues[hue].inkDark : sectionHues[hue].ink;
  return (
    <Caption1 className={styles.eyebrow} style={{ color }}>
      <span className={styles.eyebrowDot} style={{ backgroundColor: sectionHues[hue].fill }} aria-hidden="true" />
      {children}
    </Caption1>
  );
}

export function CandidateDashboard() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const navigate = useNavigate();
  const { accounts } = useMsal();
  // No stand-in name. "Welcome back, there" read as a bug because it was one.
  const firstName = (getActivePersona()?.displayName ?? accounts[0]?.name)?.trim().split(' ')[0] || null;

  const { data: dashboard, isLoading, isError, error } = useQuery({
    queryKey: ['candidates', 'me', 'dashboard'],
    queryFn: getMyCandidateDashboard,
  });

  const project = dashboard?.activeProject ?? null;

  const { data: todos } = useQuery({
    queryKey: ['assignments', project?.assignmentId, 'todos'],
    queryFn: () => getAssignmentTodos(project!.assignmentId),
    enabled: project != null,
  });

  if (isLoading) return <Spinner label="Loading your dashboard..." />;
  if (isError) return <Body1>Your dashboard didn't load: {(error as Error).message}</Body1>;
  if (!dashboard) return null;

  const progress = project && project.tasksTotal > 0 ? project.tasksComplete / project.tasksTotal : 0;
  const openTasks = (todos ?? [])
    .filter((t) => t.status !== 'Completed')
    .sort((a, b) => (a.dueDate ?? '9999').localeCompare(b.dueDate ?? '9999'));
  const nextTask = openTasks[0] ?? null;

  return (
    <>
      <div className={mergeClasses(styles.hero, surfaces.fadeInUp)}>
        <span className={styles.greeting}>
          {firstName ? `${greetingFor(new Date())}, ${firstName}` : greetingFor(new Date())}
        </span>
        <JourneyTrail status={project?.status ?? null} projectName={project?.projectName ?? null} />
      </div>

      <div className={styles.columns}>
        <Card className={mergeClasses(styles.card, surfaces.card, surfaces.fadeInUp)}>
          <Eyebrow hue="amber">Next up</Eyebrow>
          {nextTask ? (
            <>
              <span className={styles.taskTitle}>{nextTask.title}</span>
              <span className={styles.meta}>
                <CalendarLtrRegular />
                <Caption1>
                  {nextTask.dueDate ? `Due ${nextTask.dueDate}` : 'No due date'} · {nextTask.priority} priority
                </Caption1>
              </span>
              <div className={styles.spacer} />
              <Button
                appearance="primary"
                className={styles.action}
                icon={<ArrowRightRegular />}
                iconPosition="after"
                onClick={() => navigate('/tasks')}
              >
                Open tasks
              </Button>
            </>
          ) : project ? (
            <>
              <span className={styles.taskTitle}>You're all caught up.</span>
              <Body1>Nothing is open on {project.projectName} right now.</Body1>
              <div className={styles.spacer} />
              <Button className={styles.action} onClick={() => navigate('/deliverables')}>
                Review your deliverables
              </Button>
            </>
          ) : (
            <>
              <span className={styles.taskTitle}>No tasks yet</span>
              <Body1>Tasks appear here once you're staffed to a project.</Body1>
              <div className={styles.spacer} />
              <Button appearance="primary" className={styles.action} onClick={() => navigate('/marketplace')}>
                Browse projects
              </Button>
            </>
          )}
        </Card>

        <Card className={mergeClasses(styles.card, surfaces.card, surfaces.fadeInUp)}>
          <Eyebrow hue="coral">Your project</Eyebrow>
          {project ? (
            <>
              <span className={styles.taskTitle}>{project.projectName}</span>
              <Caption1>
                {project.sponsorName}
                {project.sponsorOrganization ? ` · ${project.sponsorOrganization}` : ''}
              </Caption1>

              <div style={{ marginTop: tokens.spacingVerticalS }}>
                <div className={styles.progressRow}>
                  <Caption1>Tasks complete</Caption1>
                  <Caption1 className={styles.figure}>
                    {project.tasksComplete} of {project.tasksTotal}
                  </Caption1>
                </div>
                <ProgressBar value={progress} thickness="large" />
              </div>

              {project.projectSkills.length > 0 && (
                <div className={styles.skillRow}>
                  {project.projectSkills.slice(0, 4).map((skill) => (
                    <Badge key={skill} appearance="tint" color="informative">
                      {skill}
                    </Badge>
                  ))}
                </div>
              )}
            </>
          ) : (
            <>
              <span className={styles.taskTitle}>Not staffed yet</span>
              <Body1>
                Rate the projects you'd like to work on — your interest is one of the signals matching uses.
              </Body1>
              <div className={styles.spacer} />
              <Button className={styles.action} onClick={() => navigate('/marketplace')}>
                Rate projects
              </Button>
            </>
          )}
        </Card>
      </div>

      <div className={styles.asideRow}>
        {dashboard.matchScore != null && (
          <div className={styles.aside}>
            <Caption1>Match strength</Caption1>
            <span className={styles.asideValue}>{dashboard.matchScore}%</span>
          </div>
        )}
        <div className={styles.aside}>
          <Caption1>Open tasks</Caption1>
          <span className={styles.asideValue}>{openTasks.length}</span>
        </div>
        <div className={styles.aside}>
          <Caption1>Your posts this week</Caption1>
          <span className={styles.asideValue}>{dashboard.communityPostsThisWeek}</span>
        </div>
        <div className={styles.aside}>
          <Caption1>Community</Caption1>
          <RouterLink to="/community" style={{ color: tokens.colorBrandForegroundLink }}>
            See what others are sharing
          </RouterLink>
        </div>
      </div>
    </>
  );
}
