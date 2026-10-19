using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Assignments;

/// <summary>Program Ops's manual override of the nightly lifecycle sweep — see
/// AssignmentsController.SetLifecycleStatus.</summary>
public class SetAssignmentStatusRequest
{
    public AssignmentStatus Status { get; set; }
    public string? Reason { get; set; }
}
