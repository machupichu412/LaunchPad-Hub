using LaunchPad.Application.Assignments;
using LaunchPad.Application.Common;
using LaunchPad.Application.Matching;
using LaunchPad.Domain.Entities;
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
    private readonly IAppUserRepository _appUsers;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLog _auditLog;
    private readonly IMatchingJobPublisher _matchingJobPublisher;

    public MatchingController(
        IAssignmentRepository assignments,
        IAppUserRepository appUsers,
        ICurrentUser currentUser,
        IAuditLog auditLog,
        IMatchingJobPublisher matchingJobPublisher)
    {
        _assignments = assignments;
        _appUsers = appUsers;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _matchingJobPublisher = matchingJobPublisher;
    }

    [HttpPost("run")]
    public async Task<ActionResult<RunMatchingResult>> Run([FromQuery] int cohortId, CancellationToken ct)
    {
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
        return Ok(ToDto(result.Assignment!));
    }

    [HttpPost("{assignmentId:int}/deny")]
    public async Task<ActionResult<PendingAssignmentDto>> Deny(int assignmentId, CancellationToken ct)
    {
        var assignment = await _assignments.GetAsync(assignmentId, ct);
        if (assignment is null) return NotFound();

        assignment.Status = AssignmentStatus.Withdrawn;
        await _assignments.SaveChangesAsync(ct);
        await _auditLog.RecordAsync(_currentUser.EntraObjectId, "Assignment", assignment.AssignmentId.ToString(), "OpsDeny", ct: ct);
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
