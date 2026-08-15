import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Badge,
  Body1,
  Button,
  Card,
  Caption1,
  Field,
  Input,
  Select,
  Spinner,
  Title3,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { CalendarRegular, PeopleRegular, BriefcaseRegular, FolderRegular } from '@fluentui/react-icons';
import { createCohort, getCohorts, updateCohortStatus } from '../../api/cohorts';
import { PageHeader } from '../../components/PageHeader';
import type { CohortStatus } from '../../api/types';

const useStyles = makeStyles({
  form: {
    padding: tokens.spacingVerticalL,
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
  const navigate = useNavigate();
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

  const statusMutation = useMutation({
    mutationFn: ({ cohortId, status }: { cohortId: number; status: CohortStatus }) => updateCohortStatus(cohortId, status),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cohorts'] }),
  });

  return (
    <>
      <PageHeader title="Cohorts" subtitle="Manage the running seasonal cohorts." />

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
            <Field label="Status" style={{ marginTop: tokens.spacingVerticalM }}>
              <Select
                value={cohort.status}
                disabled={statusMutation.isPending}
                onChange={(_, data) => statusMutation.mutate({ cohortId: cohort.cohortId, status: data.value as CohortStatus })}
              >
                <option value="Planned">Planned</option>
                <option value="Active">Active</option>
                <option value="Completed">Completed</option>
              </Select>
            </Field>
            <Button
              appearance="secondary"
              icon={<PeopleRegular />}
              style={{ marginTop: tokens.spacingVerticalM }}
              onClick={() => navigate(`/pipeline/${cohort.cohortId}`)}
            >
              View candidates
            </Button>
            {cohort.sharePointFolderWebUrl && (
              <Button
                appearance="secondary"
                icon={<FolderRegular />}
                style={{ marginTop: tokens.spacingVerticalS }}
                onClick={() => window.open(cohort.sharePointFolderWebUrl!, '_blank', 'noopener')}
              >
                View in SharePoint
              </Button>
            )}
          </Card>
        ))}
      </div>
      {statusMutation.isError && <Body1>Failed to update status: {(statusMutation.error as Error).message}</Body1>}
    </>
  );
}
