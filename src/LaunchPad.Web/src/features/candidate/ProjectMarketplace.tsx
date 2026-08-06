import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Body1,
  Button,
  Card,
  Caption1,
  Input,
  Spinner,
  Switch,
  Title2,
  Title3,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { SearchRegular, StarFilled, StarRegular } from '@fluentui/react-icons';
import { getOpenProjects, rateProjectInterest } from '../../api/projects';
import type { ProjectDto } from '../../api/types';

const useStyles = makeStyles({
  search: {
    maxWidth: '420px',
    marginTop: tokens.spacingVerticalM,
  },
  toolbarRow: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalL,
  },
  categoryRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexWrap: 'wrap',
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))',
    gap: tokens.spacingHorizontalM,
  },
  card: {
    padding: tokens.spacingVerticalM,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  skillRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexWrap: 'wrap',
  },
  starRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXXS,
    marginTop: tokens.spacingVerticalXS,
  },
  starButton: {
    minWidth: 'unset',
    padding: 0,
    fontSize: '18px',
  },
});

function StarRating({
  value,
  onRate,
  disabled,
}: {
  value: number | null;
  onRate: (rating: number) => void;
  disabled?: boolean;
}) {
  const styles = useStyles();
  return (
    <div className={styles.starRow} role="group" aria-label="Rate your interest">
      {[1, 2, 3, 4, 5].map((star) => (
        <Button
          key={star}
          appearance="transparent"
          className={styles.starButton}
          disabled={disabled}
          aria-label={`Rate ${star} of 5`}
          icon={value != null && star <= value ? <StarFilled /> : <StarRegular />}
          onClick={() => onRate(star)}
        />
      ))}
    </div>
  );
}

// Candidate-facing browse screen for open, Ops-approved projects. Rating interest here
// feeds as a small nudge into the matching engine (MatchingController.Run) — never a
// gate. "View details" goes to the full description + Teams contact link.
export function ProjectMarketplace() {
  const styles = useStyles();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState<string | null>(null);
  const [favoritesOnly, setFavoritesOnly] = useState(false);

  const { data: projects, isLoading, isError, error } = useQuery({
    queryKey: ['projects', 'open'],
    queryFn: getOpenProjects,
  });

  const rateMutation = useMutation({
    mutationFn: ({ projectId, rating }: { projectId: number; rating: number }) => rateProjectInterest(projectId, { rating }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['projects', 'open'] }),
  });

  const categories = useMemo(() => {
    const set = new Set<string>();
    for (const project of projects ?? []) {
      for (const skill of project.requiredSkills) {
        if (skill.category) set.add(skill.category);
      }
    }
    return Array.from(set).sort();
  }, [projects]);

  const filtered = (projects ?? []).filter((project) => {
    const matchesSearch =
      search.trim().length === 0 ||
      project.name.toLowerCase().includes(search.toLowerCase()) ||
      project.sponsorName.toLowerCase().includes(search.toLowerCase()) ||
      project.requiredSkills.some((s) => s.skillName.toLowerCase().includes(search.toLowerCase()));
    const matchesCategory = category == null || project.requiredSkills.some((s) => s.category === category);
    const matchesFavorites = !favoritesOnly || (project.myInterestRating ?? 0) >= 4;
    return matchesSearch && matchesCategory && matchesFavorites;
  });

  return (
    <>
      <Title2>Project Marketplace</Title2>
      <Body1>Browse open projects and rate your interest — it's one of several signals used to propose matches.</Body1>

      <div className={styles.toolbarRow}>
        <Input
          className={styles.search}
          contentBefore={<SearchRegular />}
          placeholder="Search projects, skills, sponsors..."
          value={search}
          onChange={(_, data) => setSearch(data.value)}
        />
        <Switch label="Highly rated only" checked={favoritesOnly} onChange={(_, data) => setFavoritesOnly(data.checked)} />
      </div>

      {categories.length > 0 && (
        <div className={styles.categoryRow} style={{ marginBottom: tokens.spacingVerticalL }}>
          <Button size="small" shape="rounded" appearance={category == null ? 'primary' : 'outline'} onClick={() => setCategory(null)}>
            All
          </Button>
          {categories.map((c) => (
            <Button key={c} size="small" shape="rounded" appearance={category === c ? 'primary' : 'outline'} onClick={() => setCategory(c)}>
              {c}
            </Button>
          ))}
        </div>
      )}

      {isLoading && <Spinner label="Loading projects..." />}
      {isError && <Body1>Failed to load projects: {(error as Error).message}</Body1>}
      {!isLoading && filtered.length === 0 && <Body1>No projects match.</Body1>}

      <div className={styles.grid}>
        {filtered.map((project: ProjectDto) => (
          <Card key={project.projectId} className={styles.card}>
            <Title3>{project.name}</Title3>
            <Caption1>
              {project.sponsorName} &middot; {project.availabilityNeeded === 'FullTime' ? 'Full-time' : 'Part-time'}
            </Caption1>
            {project.description && <Body1>{project.description}</Body1>}
            <div className={styles.skillRow}>
              {project.requiredSkills.map((s) => (
                <Badge key={s.skillName} appearance={s.isRequired ? 'filled' : 'outline'} color={s.isRequired ? 'brand' : undefined}>
                  {s.skillName}
                </Badge>
              ))}
            </div>
            <StarRating
              value={project.myInterestRating}
              disabled={rateMutation.isPending}
              onRate={(rating) => rateMutation.mutate({ projectId: project.projectId, rating })}
            />
            <Button appearance="primary" onClick={() => navigate(`/marketplace/${project.projectId}`)}>
              View details
            </Button>
          </Card>
        ))}
      </div>
      {rateMutation.isError && <Body1>Failed to save your rating: {(rateMutation.error as Error).message}</Body1>}
    </>
  );
}
