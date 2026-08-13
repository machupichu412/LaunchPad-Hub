import { Body1, Card, Field, Input, Select, Textarea, Title3, Button, tokens } from '@fluentui/react-components';
import { SkillTagPicker } from './SkillTagPicker';
import type { Availability } from '../api/types';

export interface ProjectFormValues {
  name: string;
  description: string;
  availabilityNeeded: Availability;
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
  return (
    <Card style={{ padding: tokens.spacingVerticalM, maxWidth: '560px' }}>
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
        label="Required skills"
        hint="Candidates must have all of these to be matched."
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
        hint="Nice to have, not required to match."
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
