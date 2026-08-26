import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Body1,
  Button,
  Card,
  Caption1,
  Input,
  Spinner,
  Title3,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { SearchRegular, FolderRegular } from '@fluentui/react-icons';
import { getProjectsByCohort } from '../../api/projects';
import { PageHeader } from '../../components/PageHeader';
import { useSurfaceStyles } from '../../theme/surfaces';
import { projectStatusLabel } from '../../utils/statusLabels';

// Demo-only: same single-cohort simplification used by TalentPipeline/MyProjects
// until real cohort selection exists.
const DEMO_COHORT_ID = 1;

const useStyles = makeStyles({
  search: {
    width: '280px',
  },
  categoryRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexWrap: 'wrap',
    marginBottom: tokens.spacingVerticalL,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
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
});

// Program Ops's admin catalog — every project in the cohort, any status. Distinct
// from the Sponsor's MyProjects (own only) and the Candidate's open-projects browse.
export function OpsProjects() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState<string | null>(null);

  const { data: projects, isLoading, isError, error } = useQuery({
    queryKey: ['projects', 'cohort', DEMO_COHORT_ID],
    queryFn: () => getProjectsByCohort(DEMO_COHORT_ID),
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
    return matchesSearch && matchesCategory;
  });

  return (
    <>
      <PageHeader
        title="Projects"
        subtitle="Browse sponsored projects across the cohort."
        actions={
          <Input
            className={styles.search}
            contentBefore={<SearchRegular />}
            placeholder="Search projects, skills, sponsors..."
            value={search}
            onChange={(_, data) => setSearch(data.value)}
          />
        }
      />

      {categories.length > 0 && (
        <div className={styles.categoryRow}>
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
        {filtered.map((project) => (
          <Card key={project.projectId} className={mergeClasses(styles.card, surfaces.card)}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: tokens.spacingHorizontalXS }}>
              <Title3 style={{ minWidth: 0, overflowWrap: 'break-word' }}>{project.name}</Title3>
              <Badge
                appearance="tint"
                color={project.status === 'Open' ? 'success' : 'informative'}
                style={{ flexShrink: 0, whiteSpace: 'nowrap' }}
              >
                {projectStatusLabel(project.status)}
              </Badge>
            </div>
            <Caption1>{project.sponsorName}</Caption1>
            {project.description && <Body1>{project.description}</Body1>}
            <div className={styles.skillRow}>
              {project.requiredSkills.map((s) => (
                <Badge key={s.skillName} appearance="outline">{s.skillName}</Badge>
              ))}
            </div>
            <Button appearance="primary" onClick={() => navigate(`/ops/projects/${project.projectId}`)}>
              Open project
            </Button>
            {project.sharePointFolderWebUrl && (
              <Button
                appearance="secondary"
                icon={<FolderRegular />}
                onClick={() => window.open(project.sharePointFolderWebUrl!, '_blank', 'noopener')}
              >
                View in SharePoint
              </Button>
            )}
          </Card>
        ))}
      </div>
    </>
  );
}
