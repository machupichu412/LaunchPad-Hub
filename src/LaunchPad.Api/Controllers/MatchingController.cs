using LaunchPad.Application.Assignments;
using LaunchPad.Application.Common;
using LaunchPad.Application.Matching;
using LaunchPad.Application.Projects;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

// Program Ops's admin fast path: Ops approves directly from Proposed (no separate
// Sponsor-recommend stage in this pass — see the build-out plan). "Run matching"
// calls the existing, pure MatchingEngine synchronously instead of going through
// the (unimplemented) CohortMatchingFunction/Service Bus path, mirroring how local
// demo already bypasses that async plumbing.
[ApiController]
[Route("api/matching")]
[Authorize(Policy = Policies.ApproveMatch)]
public class MatchingController : ControllerBase
{
    private readonly IAssignmentRepository _assignments;
    private readonly IProjectRepository _projects;
    private readonly IMatchingEngine _matchingEngine;
    private readonly IAppUserRepository _appUsers;
    private readonly ICurrentUser _currentUser;

    public MatchingController(
        IAssignmentRepository assignments,
        IProjectRepository projects,
        IMatchingEngine matchingEngine,
        IAppUserRepository appUsers,
        ICurrentUser currentUser)
    {
        _assignments = assignments;
        _projects = projects;
        _matchingEngine = matchingEngine;
        _appUsers = appUsers;
        _currentUser = currentUser;
    }

    [HttpPost("run")]
    public async Task<ActionResult<RunMatchingResult>> Run([FromQuery] int cohortId, CancellationToken ct)
    {
        var openProjects = await _projects.GetOpenByCohortAsync(cohortId, ct);
        var eligibleCandidates = (await _assignments.GetEligibleCandidatesForMatchingAsync(cohortId, ct)).ToList();

        var proposedCount = 0;
        foreach (var project in openProjects)
        {
            if (eligibleCandidates.Count == 0) break;

            var matchProject = new MatchProject(
                project.ProjectId,
                project.AvailabilityNeeded,
                project.Skills.Select(s => (s.SkillId, s.IsRequired)).ToArray());

            var matchCandidates = eligibleCandidates
                .Select(c => new MatchCandidate(c.CandidateId, c.Availability, c.Skills.Select(cs => cs.SkillId).ToArray()))
                .ToArray();

            var results = _matchingEngine.RankTopMatches(matchProject, matchCandidates, topN: 1);
            var best = results.FirstOrDefault();
            if (best is null) continue;

            var assignment = new Assignment
            {
                ProjectId = project.ProjectId,
                CandidateId = best.CandidateId,
                MatchScore = best.Score,
                MatchRationale = best.Rationale,
                Status = AssignmentStatus.Proposed,
            };
            await _assignments.AddAsync(assignment, ct);
            proposedCount++;

            // Remove the newly-proposed candidate from the pool so a single run
            // never proposes the same person for two different projects.
            eligibleCandidates.RemoveAll(c => c.CandidateId == best.CandidateId);
        }

        await _assignments.SaveChangesAsync(ct);
        return Ok(new RunMatchingResult { ProposedCount = proposedCount });
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
        var assignment = await _assignments.GetAsync(assignmentId, ct);
        if (assignment is null) return NotFound();

        var existingLive = await _assignments.GetLiveAssignmentAsync(assignment.CandidateId, ct);
        if (existingLive is not null && existingLive.AssignmentId != assignment.AssignmentId)
        {
            return Conflict("This candidate already has another active or approved assignment.");
        }

        var opsAppUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);

        assignment.Status = AssignmentStatus.OpsApproved;
        assignment.OpsApprovedUtc = DateTime.UtcNow;
        assignment.OpsApprovedBy = opsAppUserId;
        assignment.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _assignments.SaveChangesAsync(ct);
        return Ok(ToDto(assignment));
    }

    [HttpPost("{assignmentId:int}/deny")]
    public async Task<ActionResult<PendingAssignmentDto>> Deny(int assignmentId, CancellationToken ct)
    {
        var assignment = await _assignments.GetAsync(assignmentId, ct);
        if (assignment is null) return NotFound();

        assignment.Status = AssignmentStatus.Withdrawn;
        await _assignments.SaveChangesAsync(ct);
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
