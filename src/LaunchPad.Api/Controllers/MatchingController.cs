using LaunchPad.Application.Assignments;
using LaunchPad.Application.Common;
using LaunchPad.Application.Matching;
using LaunchPad.Domain.Entities;
using LaunchPad.Application.Cohorts;
using LaunchPad.Application.Notifications;
using LaunchPad.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace LaunchPad.Api.Controllers;

// Real two-stage flow: a cohort-wide run proposes candidates for each open project's
// remaining spots (Proposed); the sponsor recommends one per spot on their own project
// (ProjectsController's matches actions, Proposed -> SponsorApproved) or requests a
// candidate directly from the eligible-candidates gallery (also -> SponsorApproved); Ops
// approves here from SponsorApproved only (-> OpsApproved). "Run matching" publishes a
// CohortMatchingJob for async execution (CohortMatchingFunction, or the local-dev inline
// fallback — see Program.cs's Matching:RunInlineForLocalDemo) rather than running inline.
[ApiController]
[Route("api/matching")]
[Authorize(Policy = Policies.ApproveMatch)]
public class MatchingController : ControllerBase
{
    private readonly IAssignmentRepository _assignments;
    private readonly ICohortRepository _cohorts;
    private readonly IAppUserRepository _appUsers;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLog _auditLog;
    private readonly INotificationPublisher _notifications;
    private readonly IMatchingJobPublisher _matchingJobPublisher;

    public MatchingController(
        IAssignmentRepository assignments,
        ICohortRepository cohorts,
        IAppUserRepository appUsers,
        ICurrentUser currentUser,
        IAuditLog auditLog,
        INotificationPublisher notifications,
        IMatchingJobPublisher matchingJobPublisher)
    {
        _assignments = assignments;
        _cohorts = cohorts;
        _appUsers = appUsers;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _notifications = notifications;
        _matchingJobPublisher = matchingJobPublisher;
    }

    [HttpPost("run")]
    public async Task<ActionResult<RunMatchingResult>> Run([FromQuery] int cohortId, CancellationToken ct)
    {
        // Queuing a job for a cohort that doesn't exist, or one that has finished, reported
        // success and then did nothing — the failure only showed up as an empty queue later.
        var cohort = await _cohorts.GetByIdAsync(cohortId, ct);
        if (cohort is null) return NotFound();
        if (cohort.Status == CohortStatus.Completed)
        {
            return BadRequest("That cohort has finished — matching only runs for a current cohort.");
        }

        await _matchingJobPublisher.PublishAsync(new CohortMatchingJob(cohortId, _currentUser.EntraObjectId), ct);
        return Accepted(new RunMatchingResult { Queued = true });
    }

    [HttpGet("queue")]
    public async Task<ActionResult<IReadOnlyList<PendingAssignmentDto>>> GetQueue([FromQuery] int cohortId, CancellationToken ct)
    {
        var pending = await _assignments.GetPendingByCohortAsync(cohortId, ct);
        return Ok(pending.Select(ToDto).ToArray());
    }

    [HttpPost("{assignmentId:int}/approve")]
    public async Task<ActionResult<PendingAssignmentDto>> Approve(int assignmentId, CancellationToken ct)
    {
        var opsAppUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (opsAppUserId is null) return Forbid();

        var result = await _assignments.TryOpsApproveAsync(assignmentId, opsAppUserId.Value, ct);
        switch (result.Outcome)
        {
            case OpsApproveOutcome.NotFound:
                return NotFound();
            case OpsApproveOutcome.WrongStatus:
                return BadRequest("This assignment must be recommended by the sponsor before Ops can approve it.");
            case OpsApproveOutcome.CandidateConflict:
                return Conflict("This candidate already has another active or approved assignment.");
            case OpsApproveOutcome.ProjectFull:
                return Conflict("This project's candidate spots are already full.");
        }

        await _auditLog.RecordAsync(_currentUser.EntraObjectId, "Assignment", assignmentId.ToString(), "OpsApprove", ct: ct);

        var approved = result.Assignment!;
        await _notifications.PublishAsync(new NotificationMessage(
            approved.Candidate.AppUser.Upn,
            $"You're confirmed on {approved.Project.Name}",
            $"Program Ops approved your match with \"{approved.Project.Name}\". " +
            "Your tasks and deliverables appear in LaunchPad when the project starts."), ct);
        await _notifications.PublishAsync(new NotificationMessage(
            approved.Project.Sponsor.AppUser.Upn,
            $"Match confirmed: {approved.Candidate.AppUser.DisplayName}",
            $"Program Ops approved {approved.Candidate.AppUser.DisplayName} for \"{approved.Project.Name}\"."), ct);

        return Ok(ToDto(approved));
    }

    [HttpPost("{assignmentId:int}/deny")]
    public async Task<ActionResult<PendingAssignmentDto>> Deny(int assignmentId, CancellationToken ct)
    {
        var assignment = await _assignments.GetAsync(assignmentId, ct);
        if (assignment is null) return NotFound();

        // Deny is the other half of the Ops queue decision, so it gets the same precondition
        // Approve has. Without it, a deny aimed at an already-approved or active assignment
        // silently withdrew a candidate from work they had been given.
        if (assignment.Status != AssignmentStatus.SponsorApproved)
        {
            return BadRequest("Only an assignment waiting for Ops approval can be denied.");
        }

        assignment.Status = AssignmentStatus.Withdrawn;
        await _assignments.SaveChangesAsync(ct);
        await _auditLog.RecordAsync(_currentUser.EntraObjectId, "Assignment", assignment.AssignmentId.ToString(), "OpsDeny", ct: ct);

        // The sponsor picked this candidate, so they are the one who needs to know it didn't
        // stand. The candidate was never told the match was confirmed, so they aren't told
        // it was undone either — they simply return to the pool.
        await _notifications.PublishAsync(new NotificationMessage(
            assignment.Project.Sponsor.AppUser.Upn,
            $"Match not approved: {assignment.Candidate.AppUser.DisplayName}",
            $"Program Ops didn't approve {assignment.Candidate.AppUser.DisplayName} for \"{assignment.Project.Name}\". " +
            "The spot is open again on your project's matches."), ct);

        return Ok(ToDto(assignment));
    }

    private static PendingAssignmentDto ToDto(Assignment assignment) => new()
    {
        AssignmentId = assignment.AssignmentId,
        CandidateId = assignment.CandidateId,
        CandidateName = assignment.Candidate.AppUser.DisplayName,
        ProjectId = assignment.ProjectId,
        ProjectName = assignment.Project.Name,
        SponsorName = assignment.Project.Sponsor.AppUser.DisplayName,
        SponsorOrganization = assignment.Project.Sponsor.Organization,
        MatchScore = assignment.MatchScore,
        MatchRationale = assignment.MatchRationale,
    };
}
