using FluentValidation;
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
    private readonly IAppUserRepository _appUsers;
    private readonly IValidator<CreateSponsorProfileRequest> _createValidator;

    public SponsorsController(
        ISponsorRepository sponsors,
        IAssignmentRepository assignments,
        ICurrentUser currentUser,
        IAppUserRepository appUsers,
        IValidator<CreateSponsorProfileRequest> createValidator)
    {
        _sponsors = sponsors;
        _assignments = assignments;
        _currentUser = currentUser;
        _appUsers = appUsers;
        _createValidator = createValidator;
    }

    /// <summary>
    /// Self-service onboarding: creates the caller's own Sponsor row the first time
    /// they're seen (a Sponsor-role Entra token with no matching row yet — see
    /// AppUserProvisioningMiddleware, which JIT-provisions AppUser but never Sponsor).
    /// Mirrors CandidatesController.CreateMe. AppUserId is resolved server-side from
    /// EntraObjectId, never client-supplied. No cohort assignment — Sponsor isn't
    /// cohort-scoped (see CreateSponsorProfileRequest).
    /// </summary>
    [HttpPost("me")]
    public async Task<ActionResult<SponsorDto>> CreateMe(CreateSponsorProfileRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        var existing = await _sponsors.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (existing is not null) return Conflict("A sponsor profile already exists for this account.");

        var appUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (appUserId is null) return Conflict("Your account isn't provisioned yet — try signing in again.");

        var sponsor = new Sponsor
        {
            AppUserId = appUserId.Value,
            Organization = request.Organization,
            Title = request.Title,
        };

        await _sponsors.AddAsync(sponsor, ct);
        await _sponsors.SaveChangesAsync(ct);

        var created = await _sponsors.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        return CreatedAtAction(nameof(GetMe), null, new SponsorDto
        {
            SponsorId = created!.SponsorId,
            DisplayName = created.AppUser.DisplayName,
            Organization = created.Organization,
            Title = created.Title,
        });
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
        SharePointFolderWebUrl = assignment.Candidate.SharePointFolderWebUrl,
    };
}
