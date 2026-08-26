import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Body1,
  Button,
  Card,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Input,
  Select,
  Spinner,
  Subtitle2,
  Tag,
  TagGroup,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { AddCircleRegular, DismissRegular } from '@fluentui/react-icons';
import { createSkill, deleteSkill, getSkillCategories, getSkills } from '../../api/skills';
import { PageHeader } from '../../components/PageHeader';
import { useSurfaceStyles } from '../../theme/surfaces';
import type { SkillDto } from '../../api/types';

const useStyles = makeStyles({
  addForm: {
    padding: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalXL,
    display: 'flex',
    alignItems: 'flex-end',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
  },
  categoryCard: {
    padding: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalL,
  },
  skillRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexWrap: 'wrap',
    marginTop: tokens.spacingVerticalS,
  },
});

// Program Ops's taxonomy admin: every skill grouped by category, with the
// ability to add a new one (same category-driven create as the onboarding/
// profile pickers) or remove one that's no longer wanted. Deleting a skill
// still in use on a CandidateSkill/ProjectSkill row is rejected server-side
// (DeleteBehavior.Restrict) — see SkillsController.Delete.
export function SkillsManagement() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const queryClient = useQueryClient();

  const [newSkillName, setNewSkillName] = useState('');
  const [newSkillCategoryId, setNewSkillCategoryId] = useState('');
  const [pendingDelete, setPendingDelete] = useState<SkillDto | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const { data: skills, isLoading: skillsLoading, isError: skillsError } = useQuery({
    queryKey: ['skills'],
    queryFn: getSkills,
  });
  const { data: categories, isLoading: categoriesLoading } = useQuery({
    queryKey: ['skills', 'categories'],
    queryFn: getSkillCategories,
  });

  const createMutation = useMutation({
    mutationFn: createSkill,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['skills'] });
      setNewSkillName('');
      setNewSkillCategoryId('');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteSkill,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['skills'] });
      setPendingDelete(null);
      setDeleteError(null);
    },
    onError: (error: Error) => setDeleteError(error.message),
  });

  const skillsByCategory = useMemo(() => {
    const grouped = new Map<string, SkillDto[]>();
    for (const skill of skills ?? []) {
      const bucket = grouped.get(skill.skillCategoryName) ?? [];
      bucket.push(skill);
      grouped.set(skill.skillCategoryName, bucket);
    }
    return Array.from(grouped.entries())
      .map(([name, list]) => [name, list.sort((a, b) => a.name.localeCompare(b.name))] as const)
      .sort(([a], [b]) => a.localeCompare(b));
  }, [skills]);

  function openDeleteConfirm(skill: SkillDto) {
    setDeleteError(null);
    setPendingDelete(skill);
  }

  function closeDeleteConfirm() {
    setPendingDelete(null);
    setDeleteError(null);
  }

  return (
    <>
      <PageHeader title="Skills" subtitle="Manage the normalized skill taxonomy used across candidate and project forms." />

      <Card className={mergeClasses(styles.addForm, surfaces.card)}>
        <Field label="New skill name">
          <Input value={newSkillName} onChange={(_, data) => setNewSkillName(data.value)} placeholder="e.g. Rust" />
        </Field>
        <Field label="Category">
          <Select
            value={newSkillCategoryId}
            onChange={(_, data) => setNewSkillCategoryId(data.value)}
            disabled={categoriesLoading}
          >
            <option value="" disabled>
              Choose a category
            </option>
            {categories?.map((c) => (
              <option key={c.skillCategoryId} value={c.skillCategoryId}>
                {c.name}
              </option>
            ))}
          </Select>
        </Field>
        <Button
          appearance="primary"
          icon={<AddCircleRegular />}
          disabled={newSkillName.trim().length === 0 || !newSkillCategoryId || createMutation.isPending}
          onClick={() => createMutation.mutate({ name: newSkillName.trim(), skillCategoryId: Number(newSkillCategoryId) })}
        >
          {createMutation.isPending ? 'Adding...' : 'Add skill'}
        </Button>
        {createMutation.isError && (
          <Body1 style={{ width: '100%' }}>Failed to add skill: {(createMutation.error as Error).message}</Body1>
        )}
      </Card>

      {skillsLoading && <Spinner label="Loading skills..." />}
      {skillsError && <Body1>Failed to load skills.</Body1>}
      {!skillsLoading && skillsByCategory.length === 0 && <Body1>No skills defined yet.</Body1>}

      {skillsByCategory.map(([categoryName, categorySkills]) => (
        <Card key={categoryName} className={mergeClasses(styles.categoryCard, surfaces.card)}>
          <Subtitle2>{`${categoryName} (${categorySkills.length})`}</Subtitle2>
          <TagGroup
            className={styles.skillRow}
            onDismiss={(_, data) => {
              const skill = categorySkills.find((s) => String(s.skillId) === data.value);
              if (skill) openDeleteConfirm(skill);
            }}
          >
            {categorySkills.map((skill) => (
              <Tag key={skill.skillId} shape="rounded" value={String(skill.skillId)} dismissible dismissIcon={<DismissRegular />}>
                {skill.name}
              </Tag>
            ))}
          </TagGroup>
        </Card>
      ))}

      <Dialog open={pendingDelete !== null} onOpenChange={(_, data) => !data.open && closeDeleteConfirm()}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Remove "{pendingDelete?.name}"?</DialogTitle>
            <DialogContent>
              <Body1>
                This removes the skill from the taxonomy. It can't be undone, and it will fail if any candidate or
                project still has it attached.
              </Body1>
              {deleteError && (
                <Body1 style={{ display: 'block', marginTop: tokens.spacingVerticalS, color: tokens.colorPaletteRedForeground1 }}>
                  {deleteError}
                </Body1>
              )}
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={closeDeleteConfirm} disabled={deleteMutation.isPending}>
                Cancel
              </Button>
              <Button
                appearance="primary"
                onClick={() => pendingDelete && deleteMutation.mutate(pendingDelete.skillId)}
                disabled={deleteMutation.isPending}
              >
                {deleteMutation.isPending ? 'Removing...' : 'Remove skill'}
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </>
  );
}
