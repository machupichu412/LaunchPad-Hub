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
  tokens,
} from '@fluentui/react-components';
import { useMsal } from '@azure/msal-react';
import { getMyCandidateDashboard } from '../../api/candidates';
import { getAssignmentTodos } from '../../api/assignments';

const useStyles = makeStyles({
  banner: {
    borderRadius: tokens.borderRadiusLarge,
    padding: tokens.spacingVerticalXXL,
    background: 'linear-gradient(135deg, #4F52B2 0%, #7A4FBB 100%)',
    color: '#ffffff',
    marginBottom: tokens.spacingVerticalXL,
  },
  bannerTitle: {
    color: '#ffffff',
  },
  bannerBody: {
    color: 'rgba(255, 255, 255, 0.85)',
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

function StatTile({ label, value }: { label: string; value: string }) {
  const styles = useStyles();
  return (
    <Card className={styles.tile}>
      <Caption1>{label}</Caption1>
      <div className={styles.tileValue}>{value}</div>
    </Card>
  );
}

export function CandidateDashboard() {
  const styles = useStyles();
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
      <div className={styles.banner}>
        <Title1 className={styles.bannerTitle}>Welcome back, {firstName}</Title1>
        <Body1 className={styles.bannerBody}>
          {project ? `You're working on ${project.projectName}.` : "You don't have an active project assignment yet."}
        </Body1>
      </div>

      <div className={styles.tileGrid}>
        <StatTile label="Active Project" value={project ? project.projectName : 'None'} />
        <StatTile label="Tasks Complete" value={`${dashboard.tasksComplete} / ${dashboard.tasksTotal}`} />
        <StatTile label="Match Score" value={dashboard.matchScore != null ? `${dashboard.matchScore}%` : '—'} />
        <StatTile label="Community Posts" value={`${dashboard.communityPostsThisWeek} this week`} />
      </div>

      {project && (
        <Card className={styles.projectCard}>
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

      <Title2>Upcoming tasks</Title2>
      {!project && <Body1>Nothing here yet — an active assignment will show your tasks.</Body1>}
      {project && upcomingTasks.length === 0 && <Body1>You're all caught up.</Body1>}
      {upcomingTasks.length > 0 && (
        <ul className={styles.taskList}>
          {upcomingTasks.map((task) => (
            <li key={task.projectTodoId}>
              <Card className={styles.tile}>
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
