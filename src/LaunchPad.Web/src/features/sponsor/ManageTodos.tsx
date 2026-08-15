import { useState } from 'react';
import { useParams } from 'react-router-dom';
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
import { createTodo, getAssignmentDeliverables, getAssignmentTodos } from '../../api/assignments';
import { PageHeader } from '../../components/PageHeader';
import type { ProjectTodoDto, TodoPriority } from '../../api/types';

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
  list: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    marginBottom: tokens.spacingVerticalXL,
  },
  item: {
    padding: tokens.spacingVerticalM,
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
});

const priorityColor: Record<TodoPriority, 'danger' | 'warning' | 'informative'> = {
  High: 'danger',
  Medium: 'warning',
  Low: 'informative',
};

const statusColor: Record<ProjectTodoDto['status'], 'informative' | 'warning' | 'success'> = {
  NotStarted: 'informative',
  InProgress: 'warning',
  Completed: 'success',
};

/** Sponsor sets up to-do items on a candidate's assignment — reached from MyCandidates.tsx.
 * Candidates check these off in Tasks.tsx and can optionally attach a deliverable to one
 * (see Deliverables.tsx); this page shows those attachments read-only. */
export function ManageTodos() {
  const styles = useStyles();
  const { assignmentId } = useParams<{ assignmentId: string }>();
  const parsedAssignmentId = Number(assignmentId);
  const queryClient = useQueryClient();

  const { data: todos, isLoading: todosLoading, isError: todosError } = useQuery({
    queryKey: ['assignments', parsedAssignmentId, 'todos'],
    queryFn: () => getAssignmentTodos(parsedAssignmentId),
    enabled: !Number.isNaN(parsedAssignmentId),
  });

  const { data: deliverables } = useQuery({
    queryKey: ['assignments', parsedAssignmentId, 'deliverables'],
    queryFn: () => getAssignmentDeliverables(parsedAssignmentId),
    enabled: !Number.isNaN(parsedAssignmentId),
  });

  const [title, setTitle] = useState('');
  const [priority, setPriority] = useState<TodoPriority>('Medium');
  const [dueDate, setDueDate] = useState('');

  const createMutation = useMutation({
    mutationFn: () => createTodo(parsedAssignmentId, { title: title.trim(), priority, dueDate: dueDate || null }),
    onSuccess: () => {
      setTitle('');
      setPriority('Medium');
      setDueDate('');
      queryClient.invalidateQueries({ queryKey: ['assignments', parsedAssignmentId, 'todos'] });
    },
  });

  return (
    <>
      <PageHeader title="Manage to-dos" subtitle="Set up to-do items for the candidate on this project." />

      <Card className={styles.form}>
        <Title3>New to-do</Title3>
        <Field label="Title" style={{ marginTop: tokens.spacingVerticalS }}>
          <Input value={title} onChange={(_, data) => setTitle(data.value)} placeholder="Draft the wireframes" />
        </Field>
        <div className={styles.formRow}>
          <Field label="Priority">
            <Select value={priority} onChange={(_, data) => setPriority(data.value as TodoPriority)}>
              <option value="Low">Low</option>
              <option value="Medium">Medium</option>
              <option value="High">High</option>
            </Select>
          </Field>
          <Field label="Due date (optional)">
            <Input type="date" value={dueDate} onChange={(_, data) => setDueDate(data.value)} />
          </Field>
        </div>
        <Button
          appearance="primary"
          style={{ marginTop: tokens.spacingVerticalM }}
          disabled={title.trim().length === 0 || createMutation.isPending}
          onClick={() => createMutation.mutate()}
        >
          {createMutation.isPending ? 'Adding...' : 'Add to-do'}
        </Button>
        {createMutation.isError && <Body1>Failed to add to-do: {(createMutation.error as Error).message}</Body1>}
      </Card>

      {todosLoading && <Spinner label="Loading to-dos..." />}
      {todosError && <Body1>Failed to load to-dos.</Body1>}
      {todos && todos.length === 0 && <Body1>No to-dos yet — add one above.</Body1>}

      {todos && todos.length > 0 && (
        <div className={styles.list}>
          {todos.map((todo) => {
            const attached = deliverables?.filter((d) => d.projectTodoId === todo.projectTodoId) ?? [];
            return (
              <Card key={todo.projectTodoId} className={styles.item}>
                <div>
                  <Body1>{todo.title}</Body1>
                  <div style={{ marginTop: tokens.spacingVerticalXXS }}>
                    <Badge appearance="tint" color={priorityColor[todo.priority]}>
                      {todo.priority}
                    </Badge>
                    {todo.dueDate && (
                      <Badge appearance="outline" style={{ marginLeft: tokens.spacingHorizontalXS }}>
                        Due {todo.dueDate}
                      </Badge>
                    )}
                  </div>
                  {attached.length > 0 && (
                    <Caption1 style={{ display: 'block', marginTop: tokens.spacingVerticalXXS }}>
                      {attached.length} deliverable{attached.length > 1 ? 's' : ''} attached: {attached.map((d) => d.title).join(', ')}
                    </Caption1>
                  )}
                </div>
                <Badge appearance="tint" color={statusColor[todo.status]}>
                  {todo.status === 'NotStarted' ? 'Not started' : todo.status === 'InProgress' ? 'In progress' : 'Done'}
                </Badge>
              </Card>
            );
          })}
        </div>
      )}
    </>
  );
}
