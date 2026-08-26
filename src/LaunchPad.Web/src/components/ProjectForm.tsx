import { Body1, Card, Field, Input, Select, Textarea, Title3, Button, tokens } from '@fluentui/react-components';
import { SkillTagPicker } from './SkillTagPicker';
import { useSurfaceStyles } from '../theme/surfaces';
import type { Availability } from '../api/types';

export interface ProjectFormValues {
  name: string;
  description: string;
  availabilityNeeded: Availability;
  maxCandidates: number;
  requiredSkillNames: string[];
  preferredSkillNames: string[];
}

/**
 * The one shape a project's editable fields take, everywhere they're edited —
 * Sponsor create (MyProjects), Sponsor edit (EditProject), and Ops edit
 * (OpsProjectDetail). Controlled: the parent owns state and the create/update
 * mutation, this just renders the fields (including the skill lookup, see
 * SkillTagPicker) inside one consistent Card shell.
 */
export function ProjectForm({
  heading,
  values,
  onChange,
  onSubmit,
  isSubmitting,
  submitLabel,
  errorMessage,
  successMessage,
}: {
  heading?: string;
  values: ProjectFormValues;
  onChange: (values: ProjectFormValues) => void;
  onSubmit: () => void;
  isSubmitting: boolean;
  submitLabel: string;
  errorMessage?: string;
  successMessage?: string;
}) {
  const surfaces = useSurfaceStyles();
  return (
    <Card className={surfaces.card} style={{ padding: tokens.spacingVerticalM, maxWidth: '560px' }}>
      {heading && <Title3>{heading}</Title3>}
      <Field label="Name" style={heading ? { marginTop: tokens.spacingVerticalS } : undefined}>
        <Input value={values.name} onChange={(_, data) => onChange({ ...values, name: data.value })} />
      </Field>
      <Field label="Description" style={{ marginTop: tokens.spacingVerticalS }}>
        <Textarea
          value={values.description}
          onChange={(_, data) => onChange({ ...values, description: data.value })}
          resize="vertical"
          placeholder="What will the candidate be working on?"
        />
      </Field>
      <Field label="Availability needed" style={{ marginTop: tokens.spacingVerticalS }}>
        <Select
          value={values.availabilityNeeded}
          onChange={(_, data) => onChange({ ...values, availabilityNeeded: data.value as Availability })}
        >
          <option value="PartTime">Part-time</option>
          <option value="FullTime">Full-time</option>
        </Select>
      </Field>
      <Field
        label="Max candidates"
        hint="How many spots this project has — matching and sponsor requests fill up to this many."
        style={{ marginTop: tokens.spacingVerticalS }}
      >
        <Input
          type="number"
          min={1}
          value={String(values.maxCandidates)}
          onChange={(_, data) => onChange({ ...values, maxCandidates: Math.max(1, Number(data.value) || 1) })}
        />
      </Field>
      <Field
        label="Required skills"
        hint="Weighted heavily in matching — not an absolute requirement, but candidates missing these will score much lower."
        style={{ marginTop: tokens.spacingVerticalS }}
      >
        <SkillTagPicker
          selectedNames={values.requiredSkillNames}
          onChange={(names) => onChange({ ...values, requiredSkillNames: names })}
          placeholder="e.g. React, TypeScript"
        />
      </Field>
      <Field
        label="Preferred skills"
        hint="Nice to have — weighted less than required skills."
        style={{ marginTop: tokens.spacingVerticalS }}
      >
        <SkillTagPicker
          selectedNames={values.preferredSkillNames}
          onChange={(names) => onChange({ ...values, preferredSkillNames: names })}
          placeholder="e.g. Figma, Power BI"
        />
      </Field>
      <Button
        appearance="primary"
        style={{ marginTop: tokens.spacingVerticalM }}
        disabled={values.name.trim().length === 0 || isSubmitting}
        onClick={onSubmit}
      >
        {isSubmitting ? 'Saving...' : submitLabel}
      </Button>
      {errorMessage && <Body1 style={{ display: 'block', marginTop: tokens.spacingVerticalS }}>{errorMessage}</Body1>}
      {!errorMessage && successMessage && <Body1 style={{ display: 'block', marginTop: tokens.spacingVerticalS }}>{successMessage}</Body1>}
    </Card>
  );
}
