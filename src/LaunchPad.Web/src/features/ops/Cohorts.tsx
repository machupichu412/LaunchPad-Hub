import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Badge,
  Body1,
  Button,
  Card,
  Caption1,
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
  Title3,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { CalendarRegular, PeopleRegular, BriefcaseRegular, FolderRegular, ClipboardTaskRegular } from '@fluentui/react-icons';
import { createCohort, getCohorts, scheduleReviews, updateCohortStatus } from '../../api/cohorts';
import { PageHeader } from '../../components/PageHeader';
import { useSurfaceStyles } from '../../theme/surfaces';
import type { CohortDto, CohortStatus, Checkpoint } from '../../api/types';

function ScheduleReviewsDialog({ cohort, onClose }: { cohort: CohortDto | null; onClose: () => void }) {
  const [checkpoint, setCheckpoint] = useState<Checkpoint>('Midpoint');
  const [dueDate, setDueDate] = useState('');

  const mutation = useMutation({
    mutationFn: () => scheduleReviews(cohort!.cohortId, { checkpoint, dueDate }),
  });

  const handleClose = () => {
    mutation.reset();
    setDueDate('');
    onClose();
  };

  return (
    <Dialog open={cohort !== null} onOpenChange={(_, data) => !data.open && handleClose()}>
      <DialogSurface>
        {cohort && (
          <DialogBody>
            <DialogTitle>Schedule reviews for {cohort.name}</DialogTitle>
            <DialogContent>
              {mutation.isSuccess ? (
                <Body1>
                  Scheduled {mutation.data.todosCreated} review to-do{mutation.data.todosCreated === 1 ? '' : 's'} across{' '}
                  {mutation.data.assignmentsScheduled} assignment{mutation.data.assignmentsScheduled === 1 ? '' : 's'}.
                </Body1>
              ) : (
                <>
                  <Body1 style={{ display: 'block', marginBottom: tokens.spacingVerticalM }}>
                    Creates review to-dos for every active assignment in this cohort — candidates get one for their
                    sponsor and one for the project, sponsors get one for their candidate.
                  </Body1>
                  <Field label="Checkpoint" style={{ marginBottom: tokens.spacingVerticalM }}>
                    <Select value={checkpoint} onChange={(_, data) => setCheckpoint(data.value as Checkpoint)}>
                      <option value="Midpoint">Midpoint</option>
                      <option value="Final">Final</option>
                    </Select>
                  </Field>
                  <Field label="Due date">
                    <Input type="date" value={dueDate} onChange={(_, data) => setDueDate(data.value)} />
                  </Field>
                  {mutation.isError && <Body1>Failed to schedule reviews: {(mutation.error as Error).message}</Body1>}
                </>
              )}
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={handleClose}>
                {mutation.isSuccess ? 'Close' : 'Cancel'}
              </Button>
              {!mutation.isSuccess && (
                <Button
                  appearance="primary"
                  disabled={dueDate.length === 0 || mutation.isPending}
                  onClick={() => mutation.mutate()}
                >
                  {mutation.isPending ? <Spinner size="tiny" /> : 'Schedule'}
                </Button>
              )}
            </DialogActions>
          </DialogBody>
        )}
      </DialogSurface>
    </Dialog>
  );
}

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
  const surfaces = useSurfaceStyles();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: cohorts, isLoading, isError, error } = useQuery({
    queryKey: ['cohorts'],
    queryFn: getCohorts,
  });

  const [name, setName] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [schedulingCohort, setSchedulingCohort] = useState<CohortDto | null>(null);

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

      <Card className={mergeClasses(styles.form, surfaces.card)}>
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
          <Card key={cohort.cohortId} className={mergeClasses(styles.card, surfaces.card)}>
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
            {cohort.status === 'Active' && (
              <Button
                appearance="secondary"
                icon={<ClipboardTaskRegular />}
                style={{ marginTop: tokens.spacingVerticalS }}
                onClick={() => setSchedulingCohort(cohort)}
              >
                Schedule reviews
              </Button>
            )}
          </Card>
        ))}
      </div>
      {statusMutation.isError && <Body1>Failed to update status: {(statusMutation.error as Error).message}</Body1>}

      <ScheduleReviewsDialog cohort={schedulingCohort} onClose={() => setSchedulingCohort(null)} />
    </>
  );
}
