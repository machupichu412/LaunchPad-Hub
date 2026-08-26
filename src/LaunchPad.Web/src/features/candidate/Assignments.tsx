import { useQuery } from '@tanstack/react-query';
import {
  Badge,
  Body1,
  Caption1,
  Card,
  CardHeader,
  Spinner,
  Title3,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { getMyAssignment } from '../../api/assignments';
import { getOpenProjects } from '../../api/projects';
import { PageHeader } from '../../components/PageHeader';
import { availabilityLabel } from '../../utils/statusLabels';
import { useSurfaceStyles } from '../../theme/surfaces';

const useStyles = makeStyles({
  activeCard: {
    padding: tokens.spacingVerticalL,
    marginTop: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalXL,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
    gap: tokens.spacingHorizontalM,
  },
  projectCard: {
    padding: tokens.spacingVerticalM,
  },
});

// Candidate-facing: your current match plus a browse list of open, Ops-approved
// projects in your own cohort — distinct from the Sponsor's MyProjects page.
export function Assignments() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();

  const { data: assignment, isLoading: assignmentLoading } = useQuery({
    queryKey: ['assignments', 'mine'],
    queryFn: getMyAssignment,
  });

  const { data: openProjects, isLoading: openLoading, isError, error } = useQuery({
    queryKey: ['projects', 'open'],
    queryFn: getOpenProjects,
  });

  return (
    <>
      <PageHeader title="Assignments" />

      <Title3 block>Your match</Title3>
      {assignmentLoading && <Spinner label="Loading your match..." />}
      {!assignmentLoading && !assignment && <Body1>You don't have an active assignment yet.</Body1>}
      {assignment && (
        <Card className={mergeClasses(styles.activeCard, surfaces.card)}>
          <CardHeader
            header={<Title3>{assignment.projectName}</Title3>}
            description={
              <Caption1>
                {assignment.sponsorName}
                {assignment.sponsorOrganization ? ` · ${assignment.sponsorOrganization}` : ''}
              </Caption1>
            }
          />
          {assignment.projectDescription && <Body1>{assignment.projectDescription}</Body1>}
          <div style={{ marginTop: tokens.spacingVerticalS }}>
            <Badge appearance="tint" color="brand">{assignment.status}</Badge>
            {assignment.matchScore != null && (
              <Badge appearance="tint" color="success" style={{ marginLeft: tokens.spacingHorizontalXS }}>
                {assignment.matchScore}% match
              </Badge>
            )}
          </div>
          {assignment.matchRationale && (
            <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalS }}>
              {assignment.matchRationale}
            </Caption1>
          )}
          {assignment.projectSkills.length > 0 && (
            <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalS }}>
              Skills: {assignment.projectSkills.join(', ')}
            </Caption1>
          )}
        </Card>
      )}

      <Title3 block style={{ marginTop: tokens.spacingVerticalXL }}>Browse open projects</Title3>
      {openLoading && <Spinner label="Loading open projects..." />}
      {isError && <Body1>Failed to load open projects: {(error as Error).message}</Body1>}
      {openProjects && openProjects.length === 0 && <Body1>No open projects in your cohort right now.</Body1>}
      {openProjects && openProjects.length > 0 && (
        <div className={styles.grid} style={{ marginTop: tokens.spacingVerticalM }}>
          {openProjects.map((project) => (
            <Card key={project.projectId} className={mergeClasses(styles.projectCard, surfaces.card)}>
              <Title3>{project.name}</Title3>
              {project.description && <Body1>{project.description}</Body1>}
              <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalXS }}>
                {availabilityLabel(project.availabilityNeeded)}
              </Caption1>
              {project.requiredSkills.length > 0 && (
                <Caption1 style={{ display: 'block' }}>
                  Skills: {project.requiredSkills.map((s) => s.skillName).join(', ')}
                </Caption1>
              )}
            </Card>
          ))}
        </div>
      )}
    </>
  );
}
