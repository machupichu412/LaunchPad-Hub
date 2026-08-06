using LaunchPad.Application.Assignments;
using LaunchPad.Application.Common;
using LaunchPad.Application.Sponsors;
using LaunchPad.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Sponsor)]
public class SponsorsController : ControllerBase
{
    private readonly ISponsorRepository _sponsors;
    private readonly IAssignmentRepository _assignments;
    private readonly ICurrentUser _currentUser;

    public SponsorsController(ISponsorRepository sponsors, IAssignmentRepository assignments, ICurrentUser currentUser)
    {
        _sponsors = sponsors;
        _assignments = assignments;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Resolves the caller's own Sponsor record. Project create/edit endpoints use
    /// this same lookup server-side — the client never supplies a SponsorId directly.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<SponsorDto>> GetMe(CancellationToken ct)
    {
        var sponsor = await _sponsors.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (sponsor is null) return NotFound();

        return Ok(new SponsorDto
        {
            SponsorId = sponsor.SponsorId,
            DisplayName = sponsor.AppUser.DisplayName,
            Organization = sponsor.Organization,
            Title = sponsor.Title,
        });
    }

    /// <summary>Candidates currently or previously committed to one of the sponsor's own
    /// projects — the roster page, and the jumping-off point for submitting reviews.</summary>
    [HttpGet("me/candidates")]
    public async Task<ActionResult<IReadOnlyList<SponsorCandidateDto>>> GetMyCandidates(CancellationToken ct)
    {
        var sponsor = await _sponsors.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (sponsor is null) return Ok(Array.Empty<SponsorCandidateDto>());

        var assignments = await _assignments.GetBySponsorAsync(sponsor.SponsorId, ct);
        return Ok(assignments.Select(ToDto).ToArray());
    }

    private static SponsorCandidateDto ToDto(Assignment assignment) => new()
    {
        AssignmentId = assignment.AssignmentId,
        CandidateId = assignment.CandidateId,
        CandidateName = assignment.Candidate.AppUser.DisplayName,
        ProjectId = assignment.ProjectId,
        ProjectName = assignment.Project.Name,
        Status = assignment.Status,
        StartDate = assignment.StartDate,
    };
}
