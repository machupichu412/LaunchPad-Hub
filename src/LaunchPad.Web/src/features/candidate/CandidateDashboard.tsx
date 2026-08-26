import { useQuery } from '@tanstack/react-query';
import {
  Body1,
  Caption1,
  Card,
  CardHeader,
  ProgressBar,
  Spinner,
  Title1,
  Title2,
  Title3,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { useMsal } from '@azure/msal-react';
import { RocketRegular } from '@fluentui/react-icons';
import { getMyCandidateDashboard } from '../../api/candidates';
import { getAssignmentTodos } from '../../api/assignments';
import { useSurfaceStyles } from '../../theme/surfaces';

const useStyles = makeStyles({
  banner: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalL,
    borderRadius: tokens.borderRadiusLarge,
    padding: tokens.spacingVerticalXXL,
    backgroundColor: tokens.colorBrandBackground2,
    border: `${tokens.strokeWidthThin} solid ${tokens.colorBrandStroke2}`,
    marginBottom: tokens.spacingVerticalXL,
  },
  bannerIcon: {
    display: 'flex',
    flexShrink: 0,
    alignItems: 'center',
    justifyContent: 'center',
    width: '56px',
    height: '56px',
    borderRadius: tokens.borderRadiusCircular,
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    fontSize: '28px',
  },
  bannerTitle: {
    color: tokens.colorBrandForeground2,
  },
  bannerBody: {
    color: tokens.colorNeutralForeground2,
    marginTop: tokens.spacingVerticalXS,
  },
  tileGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(4, 1fr)',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalXL,
  },
  tile: {
    padding: tokens.spacingVerticalL,
  },
  tileValue: {
    fontSize: tokens.fontSizeHero800,
    fontWeight: tokens.fontWeightSemibold,
  },
  tileValueText: {
    fontSize: tokens.fontSizeBase600,
    fontWeight: tokens.fontWeightSemibold,
    lineHeight: tokens.lineHeightBase400,
    wordBreak: 'break-word',
  },
  projectCard: {
    padding: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalXL,
  },
  taskList: {
    listStyle: 'none',
    padding: 0,
    margin: 0,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
});

function StatTile({ label, value, textValue }: { label: string; value: string; textValue?: boolean }) {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  return (
    <Card className={mergeClasses(styles.tile, surfaces.card, surfaces.fadeInUp)}>
      <Caption1>{label}</Caption1>
      <div className={textValue ? styles.tileValueText : styles.tileValue}>{value}</div>
    </Card>
  );
}

export function CandidateDashboard() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const { accounts } = useMsal();
  const firstName = (accounts[0]?.name ?? 'there').split(' ')[0];

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
  if (isError) return <Body1>Failed to load your dashboard: {(error as Error).message}</Body1>;
  if (!dashboard) return null;

  const progress = project && project.tasksTotal > 0 ? project.tasksComplete / project.tasksTotal : 0;
  const upcomingTasks = (todos ?? [])
    .filter((t) => t.status !== 'Completed')
    .sort((a, b) => (a.dueDate ?? '').localeCompare(b.dueDate ?? ''))
    .slice(0, 5);

  return (
    <>
      <div className={mergeClasses(styles.banner, surfaces.fadeInUp)}>
        <span className={styles.bannerIcon} aria-hidden="true">
          <RocketRegular />
        </span>
        <div>
          <Title1 block className={styles.bannerTitle}>Welcome back, {firstName}</Title1>
          <Body1 block className={styles.bannerBody}>
            {project ? `You're working on ${project.projectName}.` : "You don't have an active project assignment yet."}
          </Body1>
        </div>
      </div>

      <div className={styles.tileGrid}>
        <StatTile label="Active Project" value={project ? project.projectName : 'None'} textValue />
        <StatTile label="Tasks Complete" value={`${dashboard.tasksComplete} / ${dashboard.tasksTotal}`} />
        <StatTile label="Match Score" value={dashboard.matchScore != null ? `${dashboard.matchScore}%` : '—'} />
        <StatTile label="Community Posts" value={`${dashboard.communityPostsThisWeek} this week`} />
      </div>

      {project && (
        <Card className={mergeClasses(styles.projectCard, surfaces.card)}>
          <CardHeader
            header={<Title3>{project.projectName}</Title3>}
            description={<Caption1>{project.sponsorName}{project.sponsorOrganization ? ` · ${project.sponsorOrganization}` : ''}</Caption1>}
          />
          {project.projectDescription && <Body1>{project.projectDescription}</Body1>}
          <div style={{ marginTop: tokens.spacingVerticalM }}>
            <Caption1>
              {project.tasksComplete} of {project.tasksTotal} tasks complete
            </Caption1>
            <ProgressBar value={progress} />
          </div>
          {project.projectSkills.length > 0 && (
            <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalS }}>
              Skills: {project.projectSkills.join(', ')}
            </Caption1>
          )}
        </Card>
      )}

      <Title2 block style={{ marginBottom: tokens.spacingVerticalS }}>Upcoming tasks</Title2>
      {!project && <Body1>Nothing here yet — an active assignment will show your tasks.</Body1>}
      {project && upcomingTasks.length === 0 && <Body1>You're all caught up.</Body1>}
      {upcomingTasks.length > 0 && (
        <ul className={styles.taskList}>
          {upcomingTasks.map((task) => (
            <li key={task.projectTodoId}>
              <Card className={mergeClasses(styles.tile, surfaces.card)}>
                <Body1>{task.title}</Body1>
                <Caption1>
                  {task.priority} priority{task.dueDate ? ` · due ${task.dueDate}` : ''}
                </Caption1>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
