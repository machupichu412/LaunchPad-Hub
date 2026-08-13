import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Badge, Body1, Button, Card, Spinner, Title3, makeStyles, tokens } from '@fluentui/react-components';
import { getAssignmentTodos, getMyAssignment, updateTodoStatus } from '../../api/assignments';
import { PageHeader } from '../../components/PageHeader';
import type { ProjectTodoDto, TodoStatus } from '../../api/types';

const useStyles = makeStyles({
  board: {
    display: 'grid',
    gridTemplateColumns: 'repeat(3, 1fr)',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalM,
  },
  column: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  card: {
    padding: tokens.spacingVerticalM,
  },
});

const columns: { status: TodoStatus; label: string }[] = [
  { status: 'NotStarted', label: 'To Do' },
  { status: 'InProgress', label: 'In Progress' },
  { status: 'Completed', label: 'Done' },
];

const nextStatus: Record<TodoStatus, TodoStatus | null> = {
  NotStarted: 'InProgress',
  InProgress: 'Completed',
  Completed: null,
};

const priorityColor: Record<ProjectTodoDto['priority'], 'danger' | 'warning' | 'informative'> = {
  High: 'danger',
  Medium: 'warning',
  Low: 'informative',
};

export function Tasks() {
  const styles = useStyles();
  const queryClient = useQueryClient();

  const { data: assignment, isLoading: assignmentLoading } = useQuery({
    queryKey: ['assignments', 'mine'],
    queryFn: getMyAssignment,
  });

  const { data: todos, isLoading: todosLoading } = useQuery({
    queryKey: ['assignments', assignment?.assignmentId, 'todos'],
    queryFn: () => getAssignmentTodos(assignment!.assignmentId),
    enabled: assignment != null,
  });

  const advanceMutation = useMutation({
    mutationFn: (todo: ProjectTodoDto) =>
      updateTodoStatus(assignment!.assignmentId, todo.projectTodoId, { status: nextStatus[todo.status]! }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['assignments', assignment?.assignmentId, 'todos'] }),
  });

  if (assignmentLoading) return <Spinner label="Loading your tasks..." />;
  if (!assignment) return <Body1>You don't have an active assignment yet, so there are no tasks to show.</Body1>;

  return (
    <>
      <PageHeader title="Tasks" />
      {todosLoading && <Spinner label="Loading tasks..." />}
      {todos && (
        <div className={styles.board}>
          {columns.map((column) => (
            <div key={column.status} className={styles.column}>
              <Title3>{column.label}</Title3>
              {todos
                .filter((t) => t.status === column.status)
                .map((todo) => (
                  <Card key={todo.projectTodoId} className={styles.card}>
                    <Body1>{todo.title}</Body1>
                    <div style={{ marginTop: tokens.spacingVerticalXS }}>
                      <Badge appearance="tint" color={priorityColor[todo.priority]}>
                        {todo.priority}
                      </Badge>
                      {todo.dueDate && (
                        <Badge appearance="outline" style={{ marginLeft: tokens.spacingHorizontalXS }}>
                          Due {todo.dueDate}
                        </Badge>
                      )}
                    </div>
                    {nextStatus[todo.status] && (
                      <Button
                        size="small"
                        style={{ marginTop: tokens.spacingVerticalS }}
                        disabled={advanceMutation.isPending}
                        onClick={() => advanceMutation.mutate(todo)}
                      >
                        Advance to {nextStatus[todo.status]}
                      </Button>
                    )}
                  </Card>
                ))}
            </div>
          ))}
        </div>
      )}
    </>
  );
}
