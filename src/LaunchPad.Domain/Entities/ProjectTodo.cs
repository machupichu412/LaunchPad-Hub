using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

/// <summary>
/// Backs the engagement-risk signal in vCandidateRisk (stale/overdue to-dos,
/// last-completion recency). Not itself a source of truth for assignment state.
/// </summary>
public class ProjectTodo
{
    public int ProjectTodoId { get; set; }
    public int AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public TodoStatus Status { get; set; }
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    public DateOnly? DueDate { get; set; }
    public DateTime? CompletedUtc { get; set; }

    public Assignment Assignment { get; set; } = null!;
}
