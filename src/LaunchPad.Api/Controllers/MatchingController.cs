using LaunchPad.Application.Assignments;
using LaunchPad.Application.Common;
using LaunchPad.Application.Matching;
using LaunchPad.Application.Projects;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

// Real two-stage flow: Run proposes up to 3 ranked candidates per open project
// (Proposed); the sponsor recommends one on their own project (ProjectsController's
// matches actions, Proposed -> SponsorApproved); Ops approves here from
// SponsorApproved only (-> OpsApproved). "Run matching" calls the existing, pure
// MatchingEngine synchronously instead of going through the (unimplemented)
// CohortMatchingFunction/Service Bus path, mirroring how local demo already
// bypasses that async plumbing.
[ApiController]
[Route("api/matching")]
[Authorize(Policy = Policies.ApproveMatch)]
public class MatchingController : ControllerBase
{
    private readonly IAssignmentRepository _assignments;
    private readonly IProjectRepository _projects;
    private readonly IProjectInterestRepository _projectInterests;
    private readonly IMatchingEngine _matchingEngine;
    private readonly IAppUserRepository _appUsers;
    private readonly ICurrentUser _currentUser;

    public MatchingController(
        IAssignmentRepository assignments,
        IProjectRepository projects,
        IProjectInterestRepository projectInterests,
        IMatchingEngine matchingEngine,
        IAppUserRepository appUsers,
        ICurrentUser currentUser)
    {
        _assignments = assignments;
        _projects = projects;
        _projectInterests = projectInterests;
        _matchingEngine = matchingEngine;
        _appUsers = appUsers;
        _currentUser = currentUser;
    }

    [HttpPost("run")]
    public async Task<ActionResult<RunMatchingResult>> Run([FromQuery] int cohortId, CancellationToken ct)
    {
        var openProjects = await _projects.GetOpenByCohortAsync(cohortId, ct);
        var eligibleCandidates = await _assignments.GetEligibleCandidatesForMatchingAsync(cohortId, ct);
        var projectIdsAlreadyInReview = (await _assignments.GetProjectIdsWithPendingMatchesAsync(cohortId, ct)).ToHashSet();
        var performanceScores = await _assignments.GetAveragePerformanceScoresByCohortAsync(cohortId, ct);
        var interestsByCandidate = (await _projectInterests.GetByCohortAsync(cohortId, ct))
            .GroupBy(i => i.CandidateId)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<int, byte>)g.ToDictionary(i => i.ProjectId, i => i.Rating));

        var matchCandidates = eligibleCandidates
            .Select(c => new MatchCandidate(
                c.CandidateId,
                c.Availability,
                c.Skills.Select(cs => cs.SkillId).ToArray(),
                c.GraduationDate,
                performanceScores.TryGetValue(c.CandidateId, out var performance) ? performance : null,
                interestsByCandidate.TryGetValue(c.CandidateId, out var interests) ? interests : null))
            .ToArray();

        var proposedCount = 0;
        foreach (var project in openProjects)
        {
            // Already has proposals awaiting sponsor/ops review — don't pile on more.
            if (projectIdsAlreadyInReview.Contains(project.ProjectId)) continue;

            var matchProject = new MatchProject(
                project.ProjectId,
                project.AvailabilityNeeded,
                project.Skills.Select(s => (s.SkillId, s.IsRequired)).ToArray(),
                project.EndDate);

            // Top 3 ranked candidates per project — the sponsor picks one from the
            // shortlist rather than being handed a single take-it-or-leave-it match.
            // A candidate merely Proposed elsewhere can still appear here; nothing is
            // decided until a sponsor recommends and Ops approves (see
            // GetEligibleCandidatesForMatchingAsync).
            foreach (var result in _matchingEngine.RankTopMatches(matchProject, matchCandidates, topN: 3))
            {
                var assignment = new Assignment
                {
                    ProjectId = project.ProjectId,
                    CandidateId = result.CandidateId,
                    MatchScore = result.Score,
                    MatchRationale = result.Rationale,
                    Status = AssignmentStatus.Proposed,
                };
                await _assignments.AddAsync(assignment, ct);
                proposedCount++;
            }
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

        if (assignment.Status != AssignmentStatus.SponsorApproved)
        {
            return BadRequest("This assignment must be recommended by the sponsor before Ops can approve it.");
        }

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

        // The candidate is committed now — withdraw any other pending offer they
        // hold on other projects (they were still Proposed/SponsorApproved there
        // because "eligible for matching" only excludes a live commitment).
        var otherPending = await _assignments.GetPendingAssignmentsForCandidateAsync(assignment.CandidateId, ct);
        foreach (var other in otherPending.Where(a => a.AssignmentId != assignment.AssignmentId))
        {
            other.Status = AssignmentStatus.Withdrawn;
        }

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
