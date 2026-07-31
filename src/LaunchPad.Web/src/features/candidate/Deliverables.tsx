import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Badge,
  Body1,
  Button,
  Card,
  Field,
  Input,
  Spinner,
  Title2,
  Title3,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { createDeliverable, getAssignmentDeliverables, getMyAssignment } from '../../api/assignments';

const useStyles = makeStyles({
  form: {
    padding: tokens.spacingVerticalL,
    marginTop: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalXL,
    maxWidth: '420px',
  },
  list: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  item: {
    padding: tokens.spacingVerticalM,
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
});

// Metadata-only — no Blob Storage locally, matching the same deferred-infra
// scoping already used for resume upload. Title/FileName are recorded for real.
export function Deliverables() {
  const styles = useStyles();
  const queryClient = useQueryClient();

  const { data: assignment, isLoading: assignmentLoading } = useQuery({
    queryKey: ['assignments', 'mine'],
    queryFn: getMyAssignment,
  });

  const { data: deliverables, isLoading: deliverablesLoading } = useQuery({
    queryKey: ['assignments', assignment?.assignmentId, 'deliverables'],
    queryFn: () => getAssignmentDeliverables(assignment!.assignmentId),
    enabled: assignment != null,
  });

  const [title, setTitle] = useState('');
  const [fileName, setFileName] = useState('');

  const submitMutation = useMutation({
    mutationFn: () => createDeliverable(assignment!.assignmentId, { title: title.trim(), fileName: fileName.trim() }),
    onSuccess: () => {
      setTitle('');
      setFileName('');
      queryClient.invalidateQueries({ queryKey: ['assignments', assignment?.assignmentId, 'deliverables'] });
    },
  });

  if (assignmentLoading) return <Spinner label="Loading..." />;
  if (!assignment) return <Body1>You don't have an active assignment yet, so there's nowhere to submit deliverables.</Body1>;

  return (
    <>
      <Title2>Deliverables</Title2>

      <Card className={styles.form}>
        <Title3>Submit a deliverable</Title3>
        <Field label="Title" style={{ marginTop: tokens.spacingVerticalS }}>
          <Input value={title} onChange={(_, data) => setTitle(data.value)} />
        </Field>
        <Field label="File name">
          <Input
            value={fileName}
            onChange={(_, data) => setFileName(data.value)}
            placeholder="wireframes-v1.pdf"
          />
        </Field>
        <Button
          appearance="primary"
          style={{ marginTop: tokens.spacingVerticalS }}
          disabled={title.trim().length === 0 || fileName.trim().length === 0 || submitMutation.isPending}
          onClick={() => submitMutation.mutate()}
        >
          {submitMutation.isPending ? 'Submitting...' : 'Submit'}
        </Button>
        {submitMutation.isError && (
          <Body1>Failed to submit: {(submitMutation.error as Error).message}</Body1>
        )}
      </Card>

      {deliverablesLoading && <Spinner label="Loading your submissions..." />}
      {deliverables && deliverables.length === 0 && <Body1>No deliverables submitted yet.</Body1>}
      {deliverables && deliverables.length > 0 && (
        <div className={styles.list}>
          {deliverables.map((d) => (
            <Card key={d.deliverableId} className={styles.item}>
              <div>
                <Body1>{d.title}</Body1>
                <br />
                <Badge appearance="outline">{d.fileName}</Badge>
              </div>
              <Badge appearance="tint" color={d.status === 'Submitted' ? 'success' : 'informative'}>
                {d.status}
              </Badge>
            </Card>
          ))}
        </div>
      )}
    </>
  );
}
