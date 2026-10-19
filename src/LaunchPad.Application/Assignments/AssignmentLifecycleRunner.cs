using LaunchPad.Application.Common;
using LaunchPad.Application.Notifications;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Assignments;

public sealed class AssignmentLifecycleRunner : IAssignmentLifecycleRunner
{
    private readonly IAssignmentRepository _assignments;
    private readonly INotificationPublisher _notifications;
    private readonly IAuditLog _auditLog;

    public AssignmentLifecycleRunner(
        IAssignmentRepository assignments,
        INotificationPublisher notifications,
        IAuditLog auditLog)
    {
        _assignments = assignments;
        _notifications = notifications;
        _auditLog = auditLog;
    }

    /// <summary>The date work is due to begin: the project's own start date when it has one,
    /// otherwise the date Ops approved the assignment.</summary>
    public static DateOnly? StartsOn(Assignment assignment) =>
        assignment.Project?.StartDate ?? assignment.StartDate;

    /// <summary>The date work is due to end. No end date means the assignment never
    /// auto-completes — Ops closes it by hand rather than the job guessing.</summary>
    public static DateOnly? EndsOn(Assignment assignment) =>
        assignment.Project?.EndDate ?? assignment.EndDate;

    public static bool IsDueToStart(Assignment assignment, DateOnly today) =>
        assignment.Status == AssignmentStatus.OpsApproved && StartsOn(assignment) is { } start && start <= today;

    public static bool IsDueToComplete(Assignment assignment, DateOnly today) =>
        assignment.Status == AssignmentStatus.Active && EndsOn(assignment) is { } end && end < today;

    public async Task<AssignmentLifecycleResult> RunAsync(DateOnly today, CancellationToken ct = default)
    {
        var candidates = await _assignments.GetForLifecycleSweepAsync(ct);

        var activated = 0;
        var completed = 0;
        foreach (var assignment in candidates)
        {
            if (IsDueToStart(assignment, today))
            {
                assignment.Status = AssignmentStatus.Active;
                assignment.StartDate ??= StartsOn(assignment);
                activated++;
            }
            else if (IsDueToComplete(assignment, today))
            {
                assignment.Status = AssignmentStatus.Completed;
                assignment.EndDate ??= EndsOn(assignment);
                completed++;
            }
            else
            {
                continue;
            }

            // Actor is the system: this is a scheduled transition, not somebody's decision.
            await _auditLog.RecordAsync(
                Guid.Empty, "Assignment", assignment.AssignmentId.ToString(),
                assignment.Status == AssignmentStatus.Active ? "Activated" : "Completed",
                reason: "Scheduled transition", ct: ct);

            if (assignment.Status == AssignmentStatus.Active)
            {
                await NotifyActivatedAsync(assignment, ct);
            }
        }

        if (activated > 0 || completed > 0)
        {
            await _assignments.SaveChangesAsync(ct);
        }

        return new AssignmentLifecycleResult(activated, completed);
    }

    private async Task NotifyActivatedAsync(Assignment assignment, CancellationToken ct)
    {
        var projectName = assignment.Project?.Name ?? "your project";
        var candidateUpn = assignment.Candidate?.AppUser?.Upn;
        if (!string.IsNullOrWhiteSpace(candidateUpn))
        {
            await _notifications.PublishAsync(new NotificationMessage(
                candidateUpn,
                $"Your project has started: {projectName}",
                $"Work on \"{projectName}\" starts today. Your tasks and deliverables are in LaunchPad."), ct);
        }

        var sponsorUpn = assignment.Project?.Sponsor?.AppUser?.Upn;
        if (!string.IsNullOrWhiteSpace(sponsorUpn))
        {
            await _notifications.PublishAsync(new NotificationMessage(
                sponsorUpn,
                $"Project started: {projectName}",
                $"{assignment.Candidate?.AppUser?.DisplayName ?? "Your candidate"} is now active on \"{projectName}\"."), ct);
        }
    }
}
