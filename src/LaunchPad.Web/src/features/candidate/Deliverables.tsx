import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Badge,
  Body1,
  Button,
  Card,
  Caption1,
  Field,
  Select,
  Spinner,
  Title3,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { DocumentArrowUpRegular } from '@fluentui/react-icons';
import {
  downloadDeliverableFile,
  getAssignmentDeliverables,
  getAssignmentTodos,
  getMyAssignment,
  submitDeliverable,
} from '../../api/assignments';
import { PageHeader } from '../../components/PageHeader';
import { useSurfaceStyles } from '../../theme/surfaces';

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
  actions: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  filePicker: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  hiddenFileInput: {
    display: 'none',
  },
});

// Files stream straight to the candidate's SharePoint folder via Graph — see
// AssignmentsController.SubmitDeliverable. Title comes from this form; the file name is
// whatever the browser reports for the picked file.
export function Deliverables() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
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

  const { data: todos } = useQuery({
    queryKey: ['assignments', assignment?.assignmentId, 'todos'],
    queryFn: () => getAssignmentTodos(assignment!.assignmentId),
    enabled: assignment != null,
  });

  const [title, setTitle] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [projectTodoId, setProjectTodoId] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  const submitMutation = useMutation({
    mutationFn: () =>
      submitDeliverable(assignment!.assignmentId, {
        title: title.trim(),
        projectTodoId: projectTodoId ? Number(projectTodoId) : null,
        file: file!,
      }),
    onSuccess: () => {
      setTitle('');
      setFile(null);
      setProjectTodoId('');
      if (fileInputRef.current) fileInputRef.current.value = '';
      queryClient.invalidateQueries({ queryKey: ['assignments', assignment?.assignmentId, 'deliverables'] });
    },
  });

  const [downloadingId, setDownloadingId] = useState<number | null>(null);

  async function handleDownload(deliverableId: number, fileName: string) {
    setDownloadingId(deliverableId);
    try {
      const blob = await downloadDeliverableFile(assignment!.assignmentId, deliverableId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.click();
      URL.revokeObjectURL(url);
    } finally {
      setDownloadingId(null);
    }
  }

  if (assignmentLoading) return <Spinner label="Loading..." />;
  if (!assignment) return <Body1>You don't have an active assignment yet, so there's nowhere to submit deliverables.</Body1>;

  return (
    <>
      <PageHeader title="Deliverables" />

      <Card className={mergeClasses(styles.form, surfaces.card)}>
        <Title3>Submit a deliverable</Title3>
        <Field label="Title" style={{ marginTop: tokens.spacingVerticalS }}>
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            style={{ padding: tokens.spacingVerticalSNudge, font: 'inherit' }}
          />
        </Field>
        <Field label="File">
          <div className={styles.filePicker}>
            <input
              ref={fileInputRef}
              type="file"
              className={styles.hiddenFileInput}
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            />
            <Button
              appearance="secondary"
              icon={<DocumentArrowUpRegular />}
              onClick={() => fileInputRef.current?.click()}
            >
              Choose file
            </Button>
            <Caption1>{file ? file.name : 'No file selected'}</Caption1>
          </div>
        </Field>
        {todos && todos.length > 0 && (
          <Field label="Attach to a to-do (optional)">
            <Select value={projectTodoId} onChange={(_, data) => setProjectTodoId(data.value)}>
              <option value="">Not attached</option>
              {todos.map((todo) => (
                <option key={todo.projectTodoId} value={todo.projectTodoId}>
                  {todo.title}
                </option>
              ))}
            </Select>
          </Field>
        )}
        <Button
          appearance="primary"
          style={{ marginTop: tokens.spacingVerticalS }}
          disabled={title.trim().length === 0 || file === null || submitMutation.isPending}
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
            <Card key={d.deliverableId} className={mergeClasses(styles.item, surfaces.card)}>
              <div>
                <Body1>{d.title}</Body1>
                <br />
                <Badge appearance="outline">{d.fileName}</Badge>
                {d.projectTodoTitle && (
                  <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalXXS }}>
                    Attached to: {d.projectTodoTitle}
                  </Caption1>
                )}
              </div>
              <div className={styles.actions}>
                {d.hasFile && (
                  <Button
                    appearance="secondary"
                    size="small"
                    disabled={downloadingId === d.deliverableId}
                    onClick={() => handleDownload(d.deliverableId, d.fileName)}
                  >
                    {downloadingId === d.deliverableId ? 'Downloading...' : 'Download'}
                  </Button>
                )}
                <Badge appearance="tint" color={d.status === 'Submitted' ? 'success' : 'informative'}>
                  {d.status}
                </Badge>
              </div>
            </Card>
          ))}
        </div>
      )}
    </>
  );
}
