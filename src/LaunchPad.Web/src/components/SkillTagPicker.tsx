import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Tag,
  TagPicker,
  TagPickerControl,
  TagPickerGroup,
  TagPickerInput,
  TagPickerList,
  TagPickerOption,
  type TagPickerProps,
} from '@fluentui/react-components';
import { getSkills } from '../api/skills';

/**
 * A lookup over the normalized skills list — type to filter, pick a match, it
 * drops in as a tag under the input. Selected/available values are skill names
 * (strings), matching the existing name-based CreateProjectRequest contract
 * (ISkillRepository.GetOrCreateByNamesAsync) — no backend change needed here.
 */
export function SkillTagPicker({
  selectedNames,
  onChange,
  placeholder = 'Search skills...',
}: {
  selectedNames: string[];
  onChange: (names: string[]) => void;
  placeholder?: string;
}) {
  const [query, setQuery] = useState('');

  const { data: skills } = useQuery({
    queryKey: ['skills'],
    queryFn: getSkills,
  });

  const filteredOptions = useMemo(() => {
    const lowerQuery = query.trim().toLowerCase();
    return (skills ?? [])
      .map((s) => s.name)
      .filter((name) => !selectedNames.includes(name))
      .filter((name) => lowerQuery.length === 0 || name.toLowerCase().includes(lowerQuery));
  }, [skills, selectedNames, query]);

  const onOptionSelect: TagPickerProps['onOptionSelect'] = (_e, data) => {
    onChange(data.selectedOptions);
    setQuery('');
  };

  return (
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
        {filteredOptions.length > 0
          ? filteredOptions.map((name) => (
              <TagPickerOption key={name} value={name}>
                {name}
              </TagPickerOption>
            ))
          : 'No matching skills'}
      </TagPickerList>
    </TagPicker>
  );
}
