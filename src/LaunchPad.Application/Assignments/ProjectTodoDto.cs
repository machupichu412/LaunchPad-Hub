using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Assignments;

public class ProjectTodoDto
{
    public int ProjectTodoId { get; set; }
    public string Title { get; set; } = string.Empty;
    public TodoStatus Status { get; set; }
    public TodoPriority Priority { get; set; }
    public DateOnly? DueDate { get; set; }
    public ReviewType? LinkedReviewType { get; set; }
    public Checkpoint? LinkedReviewCheckpoint { get; set; }
}

public class UpdateTodoStatusRequest
{
    public TodoStatus Status { get; set; }
}

/// <summary>Sponsor (or Ops/Exec) establishes a to-do item on a candidate's assignment —
/// see AssignmentsController.CreateTodo, which restricts this specifically to non-Candidate
/// roles (a Candidate can only ever check todos off, never create their own).</summary>
public class CreateTodoRequest
{
    public string Title { get; set; } = string.Empty;
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    public DateOnly? DueDate { get; set; }
}
