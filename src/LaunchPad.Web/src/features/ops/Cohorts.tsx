import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Badge,
  Body1,
  Button,
  Card,
  Caption1,
  Field,
  Input,
  Spinner,
  Title2,
  Title3,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { CalendarRegular, PeopleRegular, BriefcaseRegular } from '@fluentui/react-icons';
import { createCohort, getCohorts } from '../../api/cohorts';

const useStyles = makeStyles({
  form: {
    padding: tokens.spacingVerticalL,
    marginTop: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalXL,
    maxWidth: '480px',
  },
  formRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))',
    gap: tokens.spacingHorizontalM,
  },
  card: {
    padding: tokens.spacingVerticalL,
  },
  statRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalM,
  },
  stat: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
  },
});

export function Cohorts() {
  const styles = useStyles();
  const queryClient = useQueryClient();

  const { data: cohorts, isLoading, isError, error } = useQuery({
    queryKey: ['cohorts'],
    queryFn: getCohorts,
  });

  const [name, setName] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');

  const createMutation = useMutation({
    mutationFn: () => createCohort({ name, startDate, endDate }),
    onSuccess: () => {
      setName('');
      setStartDate('');
      setEndDate('');
      queryClient.invalidateQueries({ queryKey: ['cohorts'] });
    },
  });

  const canCreate = name.trim().length > 0 && startDate.length > 0 && endDate.length > 0;

  return (
    <>
      <Title2>Cohorts</Title2>
      <Body1>Manage the running seasonal cohorts.</Body1>

      <Card className={styles.form}>
        <Title3>New cohort</Title3>
        <Field label="Name" style={{ marginTop: tokens.spacingVerticalS }}>
          <Input value={name} onChange={(_, data) => setName(data.value)} placeholder="LP-2027-Spring" />
        </Field>
        <div className={styles.formRow}>
          <Field label="Start date">
            <Input type="date" value={startDate} onChange={(_, data) => setStartDate(data.value)} />
          </Field>
          <Field label="End date">
            <Input type="date" value={endDate} onChange={(_, data) => setEndDate(data.value)} />
          </Field>
        </div>
        <Button
          appearance="primary"
          style={{ marginTop: tokens.spacingVerticalM }}
          disabled={!canCreate || createMutation.isPending}
          onClick={() => createMutation.mutate()}
        >
          {createMutation.isPending ? 'Creating...' : 'Create cohort'}
        </Button>
        {createMutation.isError && <Body1>Failed to create cohort: {(createMutation.error as Error).message}</Body1>}
      </Card>

      {isLoading && <Spinner label="Loading cohorts..." />}
      {isError && <Body1>Failed to load cohorts: {(error as Error).message}</Body1>}
      {cohorts && cohorts.length === 0 && <Body1>No cohorts yet.</Body1>}

      <div className={styles.grid}>
        {cohorts?.map((cohort) => (
          <Card key={cohort.cohortId} className={styles.card}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <Title3>{cohort.name}</Title3>
              <Badge appearance="tint" color={cohort.status === 'Active' ? 'success' : 'informative'}>{cohort.status}</Badge>
            </div>
            <Caption1>{cohort.programName}</Caption1>
            <div className={styles.statRow}>
              <span className={styles.stat}><PeopleRegular /> {cohort.candidateCount}</span>
              <span className={styles.stat}><BriefcaseRegular /> {cohort.projectCount}</span>
              <span className={styles.stat}><CalendarRegular /> {cohort.startDate} → {cohort.endDate}</span>
            </div>
          </Card>
        ))}
      </div>
    </>
  );
}
