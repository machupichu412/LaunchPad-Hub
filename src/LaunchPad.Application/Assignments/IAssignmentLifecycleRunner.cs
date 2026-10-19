namespace LaunchPad.Application.Assignments;

public sealed record AssignmentLifecycleResult(int Activated, int Completed);

/// <summary>
/// Moves assignments through the two status changes nobody presses a button for:
/// OpsApproved -> Active once work is due to start, and Active -> Completed once the
/// project has ended. Run nightly by AssignmentLifecycleFunction; Program Ops can also
/// force either transition for one assignment from the API when reality and the
/// calendar disagree.
/// </summary>
public interface IAssignmentLifecycleRunner
{
    Task<AssignmentLifecycleResult> RunAsync(DateOnly today, CancellationToken ct = default);
}
