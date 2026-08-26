import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Badge, Body1, Button, Card, Caption1, Spinner, Title2, makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { ArrowLeftRegular, ChatRegular, StarFilled, StarRegular } from '@fluentui/react-icons';
import { getOpenProjectDetail, rateProjectInterest } from '../../api/projects';
import { useSurfaceStyles } from '../../theme/surfaces';

const useStyles = makeStyles({
  backRow: {
    marginBottom: tokens.spacingVerticalM,
  },
  card: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    maxWidth: '720px',
  },
  skillRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexWrap: 'wrap',
  },
  starRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXXS,
  },
  starButton: {
    minWidth: 'unset',
    padding: 0,
    fontSize: '22px',
  },
  actionsRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalS,
  },
});

// Marketplace detail view: full description, both skill tiers, an interest-rating
// control, and a direct Teams link to the sponsor (server-computed from their UPN —
// see ProjectsController.ToDto). No email/phone number is exposed, just the deep link.
export function ProjectDetail() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const projectId = Number(id);
  const queryClient = useQueryClient();

  const { data: project, isLoading, isError, error } = useQuery({
    queryKey: ['projects', projectId, 'open-detail'],
    queryFn: () => getOpenProjectDetail(projectId),
    enabled: !Number.isNaN(projectId),
  });

  const rateMutation = useMutation({
    mutationFn: (rating: number) => rateProjectInterest(projectId, { rating }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'open-detail'] }),
  });

  return (
    <>
      <div className={styles.backRow}>
        <Button appearance="subtle" icon={<ArrowLeftRegular />} onClick={() => navigate('/marketplace')}>
          Back to marketplace
        </Button>
      </div>

      {isLoading && <Spinner label="Loading project..." />}
      {isError && <Body1>Failed to load project: {(error as Error).message}</Body1>}

      {project && (
        <Card className={mergeClasses(styles.card, surfaces.card)}>
          <div>
            <Title2 block>{project.name}</Title2>
            <Caption1 block style={{ marginTop: tokens.spacingVerticalXXS }}>
              {project.sponsorName} &middot; {project.availabilityNeeded === 'FullTime' ? 'Full-time' : 'Part-time'}
            </Caption1>
          </div>

          {project.description && <Body1>{project.description}</Body1>}

          <div>
            <Body1>
              <strong>Required skills</strong>
            </Body1>
            <div className={styles.skillRow} style={{ marginTop: tokens.spacingVerticalXS }}>
              {project.requiredSkills.filter((s) => s.isRequired).map((s) => (
                <Badge key={s.skillName} appearance="filled" color="brand">
                  {s.skillName}
                </Badge>
              ))}
            </div>
          </div>

          {project.requiredSkills.some((s) => !s.isRequired) && (
            <div>
              <Body1>
                <strong>Preferred skills</strong>
              </Body1>
              <div className={styles.skillRow} style={{ marginTop: tokens.spacingVerticalXS }}>
                {project.requiredSkills.filter((s) => !s.isRequired).map((s) => (
                  <Badge key={s.skillName} appearance="outline">
                    {s.skillName}
                  </Badge>
                ))}
              </div>
            </div>
          )}

          <div>
            <Body1>
              <strong>Your interest</strong>
            </Body1>
            <div className={styles.starRow} style={{ marginTop: tokens.spacingVerticalXS }}>
              {[1, 2, 3, 4, 5].map((star) => (
                <Button
                  key={star}
                  appearance="transparent"
                  className={styles.starButton}
                  disabled={rateMutation.isPending}
                  aria-label={`Rate ${star} of 5`}
                  icon={project.myInterestRating != null && star <= project.myInterestRating ? <StarFilled /> : <StarRegular />}
                  onClick={() => rateMutation.mutate(star)}
                />
              ))}
            </div>
          </div>

          <div className={styles.actionsRow}>
            <Button appearance="primary" icon={<ChatRegular />} onClick={() => window.open(project.sponsorTeamsLink, '_blank', 'noopener')}>
              Contact sponsor on Teams
            </Button>
          </div>
          {rateMutation.isError && <Body1>Failed to save your rating: {(rateMutation.error as Error).message}</Body1>}
        </Card>
      )}
    </>
  );
}
