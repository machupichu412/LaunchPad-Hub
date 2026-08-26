import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Button,
  Caption1,
  Field,
  Select,
  Tag,
  TagPicker,
  TagPickerControl,
  TagPickerGroup,
  TagPickerInput,
  TagPickerList,
  TagPickerOption,
  tokens,
  type TagPickerProps,
} from '@fluentui/react-components';
import { AddCircleRegular } from '@fluentui/react-icons';
import { createSkill, getSkillCategories, getSkills } from '../api/skills';

const CREATE_OPTION_VALUE = '__create_new_skill__';

/**
 * A lookup over the normalized skills list — type to filter, pick a match, it
 * drops in as a tag under the input. Selected/available values are skill names
 * (strings), matching the existing name-based CreateProjectRequest/
 * UpdateCandidateProfileRequest contract (ISkillRepository.GetOrCreateByNamesAsync)
 * — no backend change needed for the picker itself.
 *
 * With `allowCreate`, typing a name that doesn't match any existing skill offers
 * an "Add as a new skill" option; picking it opens the same category-driven
 * creation flow as the onboarding SkillPicker (SkillsController.Create), rather
 * than silently falling back to "Uncategorized" the way the plain free-text
 * paths do.
 */
export function SkillTagPicker({
  selectedNames,
  onChange,
  placeholder = 'Search skills...',
  allowCreate = false,
}: {
  selectedNames: string[];
  onChange: (names: string[]) => void;
  placeholder?: string;
  allowCreate?: boolean;
}) {
  const queryClient = useQueryClient();
  const [query, setQuery] = useState('');
  const [pendingNewSkillName, setPendingNewSkillName] = useState<string | null>(null);
  const [newSkillCategoryId, setNewSkillCategoryId] = useState('');

  const { data: skills } = useQuery({
    queryKey: ['skills'],
    queryFn: getSkills,
  });
  const { data: categories, isLoading: categoriesLoading } = useQuery({
    queryKey: ['skills', 'categories'],
    queryFn: getSkillCategories,
    enabled: allowCreate,
  });

  const createMutation = useMutation({
    mutationFn: createSkill,
    onSuccess: (newSkill) => {
      queryClient.invalidateQueries({ queryKey: ['skills'] });
      onChange([...selectedNames, newSkill.name]);
      setPendingNewSkillName(null);
      setNewSkillCategoryId('');
      setQuery('');
    },
  });

  const trimmedQuery = query.trim();

  const filteredOptions = useMemo(() => {
    const lowerQuery = trimmedQuery.toLowerCase();
    return (skills ?? [])
      .map((s) => s.name)
      .filter((name) => !selectedNames.includes(name))
      .filter((name) => lowerQuery.length === 0 || name.toLowerCase().includes(lowerQuery));
  }, [skills, selectedNames, trimmedQuery]);

  const canOfferCreate =
    allowCreate &&
    trimmedQuery.length > 0 &&
    !(skills ?? []).some((s) => s.name.toLowerCase() === trimmedQuery.toLowerCase()) &&
    !selectedNames.some((n) => n.toLowerCase() === trimmedQuery.toLowerCase());

  const onOptionSelect: TagPickerProps['onOptionSelect'] = (_e, data) => {
    if (data.value === CREATE_OPTION_VALUE) {
      setPendingNewSkillName(trimmedQuery);
      return;
    }
    onChange(data.selectedOptions);
    setQuery('');
  };

  return (
    <div>
      <TagPicker onOptionSelect={onOptionSelect} selectedOptions={selectedNames}>
        <TagPickerControl>
          <TagPickerGroup>
            {selectedNames.map((name) => (
              <Tag key={name} shape="rounded" value={name}>
                {name}
              </Tag>
            ))}
          </TagPickerGroup>
          <TagPickerInput value={query} onChange={(e) => setQuery(e.target.value)} placeholder={placeholder} />
        </TagPickerControl>
        <TagPickerList>
          {filteredOptions.map((name) => (
            <TagPickerOption key={name} value={name}>
              {name}
            </TagPickerOption>
          ))}
          {canOfferCreate && (
            <TagPickerOption key={CREATE_OPTION_VALUE} value={CREATE_OPTION_VALUE} media={<AddCircleRegular />}>
              {`Add "${trimmedQuery}" as a new skill`}
            </TagPickerOption>
          )}
          {filteredOptions.length === 0 && !canOfferCreate && 'No matching skills'}
        </TagPickerList>
      </TagPicker>

      {pendingNewSkillName && (
        <div
          style={{
            display: 'flex',
            alignItems: 'flex-end',
            gap: tokens.spacingHorizontalS,
            marginTop: tokens.spacingVerticalS,
            padding: tokens.spacingVerticalM,
            backgroundColor: tokens.colorNeutralBackground2,
            borderRadius: tokens.borderRadiusMedium,
            flexWrap: 'wrap',
          }}
        >
          <Field label={`Category for "${pendingNewSkillName}"`}>
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
            size="small"
            disabled={!newSkillCategoryId || createMutation.isPending}
            onClick={() =>
              createMutation.mutate({ name: pendingNewSkillName, skillCategoryId: Number(newSkillCategoryId) })
            }
          >
            {createMutation.isPending ? 'Adding...' : 'Add skill'}
          </Button>
          <Button
            appearance="subtle"
            size="small"
            onClick={() => {
              setPendingNewSkillName(null);
              setNewSkillCategoryId('');
            }}
          >
            Cancel
          </Button>
        </div>
      )}
      {createMutation.isError && (
        <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalXXS }}>
          Failed to add skill: {(createMutation.error as Error).message}
        </Caption1>
      )}
    </div>
  );
}
