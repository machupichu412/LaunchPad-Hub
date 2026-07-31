using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Assignments;

public class ProjectTodoDto
{
    public int ProjectTodoId { get; set; }
    public string Title { get; set; } = string.Empty;
    public TodoStatus Status { get; set; }
    public TodoPriority Priority { get; set; }
    public DateOnly? DueDate { get; set; }
}

public class UpdateTodoStatusRequest
{
    public TodoStatus Status { get; set; }
}
