import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Body1, Button, Caption1, Field, Input, Select, Spinner, Subtitle2, makeStyles, tokens } from '@fluentui/react-components';
import { AddCircleRegular, SearchRegular } from '@fluentui/react-icons';
import { createSkill, getSkillCategories, getSkills } from '../../api/skills';

const useStyles = makeStyles({
  search: {
    maxWidth: '360px',
  },
  category: {
    marginTop: tokens.spacingVerticalL,
  },
  chipRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexWrap: 'wrap',
    marginTop: tokens.spacingVerticalXS,
  },
  addForm: {
    display: 'flex',
    alignItems: 'flex-end',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    flexWrap: 'wrap',
  },
});

// Browse-by-category skill multi-select. "Can't find your skill?" opens a small
// inline form to add it under a chosen category — a deliberate, category-driven
// creation distinct from the free-text "Uncategorized" fallback MyProfile.tsx's
// edit screen still uses for typed skill names.
export function SkillPicker({
  selectedSkillIds,
  onChange,
}: {
  selectedSkillIds: number[];
  onChange: (skillIds: number[]) => void;
}) {
  const styles = useStyles();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [showAddForm, setShowAddForm] = useState(false);
  const [newSkillName, setNewSkillName] = useState('');
  const [newSkillCategoryId, setNewSkillCategoryId] = useState('');

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
    onSuccess: (newSkill) => {
      queryClient.invalidateQueries({ queryKey: ['skills'] });
      onChange([...selectedSkillIds, newSkill.skillId]);
      setNewSkillName('');
      setNewSkillCategoryId('');
      setShowAddForm(false);
    },
  });

  const toggleSkill = (skillId: number) => {
    onChange(
      selectedSkillIds.includes(skillId)
        ? selectedSkillIds.filter((id) => id !== skillId)
        : [...selectedSkillIds, skillId],
    );
  };

  const skillsByCategory = useMemo(() => {
    const filtered = (skills ?? []).filter((s) => s.name.toLowerCase().includes(search.trim().toLowerCase()));
    const grouped = new Map<string, typeof filtered>();
    for (const skill of filtered) {
      const bucket = grouped.get(skill.skillCategoryName) ?? [];
      bucket.push(skill);
      grouped.set(skill.skillCategoryName, bucket);
    }
    return Array.from(grouped.entries()).sort(([a], [b]) => a.localeCompare(b));
  }, [skills, search]);

  return (
    <div>
      <Input
        className={styles.search}
        contentBefore={<SearchRegular />}
        placeholder="Search skills..."
        value={search}
        onChange={(_, data) => setSearch(data.value)}
      />

      {skillsLoading && <Spinner label="Loading skills..." style={{ marginTop: tokens.spacingVerticalM }} />}
      {skillsError && <Body1>Failed to load skills.</Body1>}
      {!skillsLoading && skillsByCategory.length === 0 && (
        <Body1 style={{ display: 'block', marginTop: tokens.spacingVerticalM }}>No skills match "{search}".</Body1>
      )}

      {skillsByCategory.map(([categoryName, categorySkills]) => (
        <div key={categoryName} className={styles.category}>
          <Subtitle2>{categoryName}</Subtitle2>
          <div className={styles.chipRow}>
            {categorySkills.map((skill) => (
              <Button
                key={skill.skillId}
                size="small"
                shape="rounded"
                appearance={selectedSkillIds.includes(skill.skillId) ? 'primary' : 'outline'}
                onClick={() => toggleSkill(skill.skillId)}
              >
                {skill.name}
              </Button>
            ))}
          </div>
        </div>
      ))}

      {!showAddForm && (
        <Button
          appearance="subtle"
          icon={<AddCircleRegular />}
          style={{ marginTop: tokens.spacingVerticalL }}
          onClick={() => setShowAddForm(true)}
        >
          Can't find your skill? Add it
        </Button>
      )}

      {showAddForm && (
        <div className={styles.addForm}>
          <Field label="Skill name">
            <Input value={newSkillName} onChange={(_, data) => setNewSkillName(data.value)} placeholder="e.g. Rust" />
          </Field>
          <Field label="Category">
            <Select value={newSkillCategoryId} onChange={(_, data) => setNewSkillCategoryId(data.value)} disabled={categoriesLoading}>
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
            disabled={newSkillName.trim().length === 0 || !newSkillCategoryId || createMutation.isPending}
            onClick={() => createMutation.mutate({ name: newSkillName.trim(), skillCategoryId: Number(newSkillCategoryId) })}
          >
            {createMutation.isPending ? 'Adding...' : 'Add skill'}
          </Button>
          <Button
            appearance="subtle"
            onClick={() => {
              setShowAddForm(false);
              setNewSkillName('');
              setNewSkillCategoryId('');
            }}
          >
            Cancel
          </Button>
        </div>
      )}
      {createMutation.isError && (
        <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalXS }}>
          Failed to add skill: {(createMutation.error as Error).message}
        </Caption1>
      )}
    </div>
  );
}
