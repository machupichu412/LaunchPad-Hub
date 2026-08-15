using FluentValidation;
using LaunchPad.Application.Assignments;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Cohorts;
using LaunchPad.Application.Common;
using LaunchPad.Application.Community;
using LaunchPad.Application.Reviews;
using LaunchPad.Application.Risk;
using LaunchPad.Application.SharePoint;
using LaunchPad.Application.Skills;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

// No class-level policy: Get/GetByCohort are Ops/Exec/Sponsor/HiringManager-only
// (ViewTalentPipeline), but Candidate must reach GetMe/UpdateMe — Candidate is
// deliberately excluded from ViewTalentPipeline, so a class-level attribute here
// would AND against each method's own [Authorize] and lock Candidates out of their
// own profile endpoints.
[ApiController]
[Route("api/[controller]")]
public class CandidatesController : ControllerBase
{
    private readonly ICandidateRepository _candidates;
    private readonly ICandidateDtoMapper _mapper;
    private readonly ISkillRepository _skills;
    private readonly ICohortRepository _cohorts;
    private readonly IAppUserRepository _appUsers;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<UpdateCandidateProfileRequest> _updateValidator;
    private readonly IValidator<CreateCandidateProfileRequest> _createValidator;
    private readonly IValidator<UpdateCandidateStatusRequest> _updateStatusValidator;
    private readonly IAssignmentRepository _assignments;
    private readonly ICommunityRepository _community;
    private readonly IProfilePictureStorage _profilePictures;
    private readonly IReviewRepository _reviews;
    private readonly IAuditLog _auditLog;
    private readonly IFolderProvisioningJobPublisher _folderProvisioning;

    public CandidatesController(
        ICandidateRepository candidates,
        ICandidateDtoMapper mapper,
        ISkillRepository skills,
        ICohortRepository cohorts,
        IAppUserRepository appUsers,
        ICurrentUser currentUser,
        IValidator<UpdateCandidateProfileRequest> updateValidator,
        IValidator<CreateCandidateProfileRequest> createValidator,
        IValidator<UpdateCandidateStatusRequest> updateStatusValidator,
        IAssignmentRepository assignments,
        ICommunityRepository community,
        IProfilePictureStorage profilePictures,
        IReviewRepository reviews,
        IAuditLog auditLog,
        IFolderProvisioningJobPublisher folderProvisioning)
    {
        _candidates = candidates;
        _mapper = mapper;
        _skills = skills;
        _cohorts = cohorts;
        _appUsers = appUsers;
        _currentUser = currentUser;
        _updateValidator = updateValidator;
        _createValidator = createValidator;
        _updateStatusValidator = updateStatusValidator;
        _assignments = assignments;
        _community = community;
        _profilePictures = profilePictures;
        _reviews = reviews;
        _auditLog = auditLog;
        _folderProvisioning = folderProvisioning;
    }

    private async Task<SuggestedHireOutcome?> ComputeSuggestedHireOutcomeAsync(int candidateId, CandidateRisk? risk, CancellationToken ct)
    {
        var latestFinalRecommend = await _reviews.GetLatestFinalRecommendConversionAsync(candidateId, ct);
        return HireOutcomeRule.Evaluate(new HireOutcomeSignal(
            candidateId, risk?.FinalScore, latestFinalRecommend, risk?.HasPerformanceRisk ?? false, risk?.HasEngagementRisk ?? false));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.ViewTalentPipeline)]
    public async Task<ActionResult<CandidateDto>> Get(int id, CancellationToken ct)
    {
        var candidate = await _candidates.GetWithSkillsAsync(id, ct);
        if (candidate is null) return NotFound();

        // Redaction happens inside the mapper — never filter scores here or in the client.
        var risk = await _candidates.GetRiskAsync(id, ct);
        var suggestion = await ComputeSuggestedHireOutcomeAsync(id, risk, ct);
        return Ok(_mapper.ToDto(candidate, risk, suggestion, User));
    }

    /// <summary>The candidate's photo for roster/browse cards (see CandidateAvatar.tsx) —
    /// same ViewTalentPipeline gate as viewing the candidate's other info; a photo carries
    /// none of the hidden-score sensitivity CLAUDE.md's redaction rule is about, so no
    /// separate ownership check is needed beyond "can this caller see candidates at all."</summary>
    [HttpGet("{id:int}/avatar")]
    [Authorize(Policy = Policies.ViewTalentPipeline)]
    public async Task<IActionResult> GetAvatar(int id, CancellationToken ct)
    {
        var candidate = await _candidates.GetWithSkillsAsync(id, ct);
        if (candidate?.AppUser.AvatarBlobPath is not { } blobPath) return NotFound();

        var result = await _profilePictures.GetAsync(blobPath, ct);
        if (result is null) return NotFound();

        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpGet("cohort/{cohortId:int}")]
    [Authorize(Policy = Policies.ViewTalentPipeline)]
    public async Task<ActionResult<IReadOnlyList<CandidateDto>>> GetByCohort(int cohortId, CancellationToken ct)
    {
        var candidates = await _candidates.GetByCohortAsync(cohortId, ct);
        var dtos = new List<CandidateDto>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var risk = await _candidates.GetRiskAsync(candidate.CandidateId, ct);
            var suggestion = await ComputeSuggestedHireOutcomeAsync(candidate.CandidateId, risk, ct);
            dtos.Add(_mapper.ToDto(candidate, risk, suggestion, User));
        }

        return Ok(dtos);
    }

    /// <summary>
    /// Aggregated dashboard stats. "Conversion readiness" tiles from the mockup
    /// (skill growth / sponsor feedback / deliverable quality) are deliberately
    /// omitted — no historical tracking exists to compute them defensibly.
    /// </summary>
    [HttpGet("me/dashboard")]
    [Authorize(Roles = Roles.Candidate)]
    public async Task<ActionResult<CandidateDashboardDto>> GetMyDashboard(CancellationToken ct)
    {
        var candidate = await _candidates.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (candidate is null) return NotFound();

        var assignment = await _assignments.GetActiveByCandidateIdAsync(candidate.CandidateId, ct);
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var communityPostsThisWeek = await _community.CountPostsSinceAsync(weekAgo, ct);

        if (assignment is null)
        {
            return Ok(new CandidateDashboardDto { CommunityPostsThisWeek = communityPostsThisWeek });
        }

        var todos = await _assignments.GetTodosAsync(assignment.AssignmentId, ct);
        var tasksComplete = todos.Count(t => t.Status == TodoStatus.Completed);

        return Ok(new CandidateDashboardDto
        {
            ActiveProject = assignment.ToMyAssignmentDto(todos.Count, tasksComplete),
            TasksComplete = tasksComplete,
            TasksTotal = todos.Count,
            MatchScore = assignment.MatchScore,
            CommunityPostsThisWeek = communityPostsThisWeek,
        });
    }

    /// <summary>
    /// Self-service onboarding: creates the caller's own Candidate row the first time
    /// they're seen (a Candidate-role Entra token with no matching row yet — see
    /// AppUserProvisioningMiddleware, which JIT-provisions AppUser but never Candidate).
    /// AppUserId is resolved server-side from EntraObjectId, never client-supplied,
    /// same pattern ProjectsController uses for SponsorId. Cohort is auto-assigned to
    /// the single active cohort; ambiguity (zero or multiple) is a 409, not a guess.
    /// </summary>
    [HttpPost("me")]
    [Authorize(Roles = Roles.Candidate)]
    public async Task<ActionResult<CandidateDto>> CreateMe(CreateCandidateProfileRequest request, CancellationToken ct)
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

        var existing = await _candidates.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (existing is not null) return Conflict("A candidate profile already exists for this account.");

        // Request-shape validity (do the selected skills even exist) is checked before
        // any external resource-state check (active cohort count) — a malformed request
        // should fail the same way regardless of how many cohorts happen to be active.
        var allSkills = await _skills.GetAllAsync(ct);
        var requestedSkillIds = request.SkillIds.ToHashSet();
        var matchedSkills = allSkills.Where(s => requestedSkillIds.Contains(s.SkillId)).ToList();
        if (matchedSkills.Count != requestedSkillIds.Count)
        {
            return BadRequest("One or more selected skills don't exist.");
        }

        var appUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (appUserId is null) return Conflict("Your account isn't provisioned yet — try signing in again.");

        var activeCohorts = await _cohorts.GetActiveAsync(ct);
        if (activeCohorts.Count != 1)
        {
            return Conflict(activeCohorts.Count == 0
                ? "There's no active cohort to join right now — contact Program Ops."
                : "More than one cohort is active — contact Program Ops to get assigned.");
        }

        var candidate = new Candidate
        {
            AppUserId = appUserId.Value,
            CohortId = activeCohorts[0].CohortId,
            Location = request.Location,
            Availability = request.Availability,
            GraduationDate = request.GraduationDate,
            LinkedInUrl = request.LinkedInUrl,
            PortfolioUrl = request.PortfolioUrl,
            Bio = request.Bio,
            School = request.School,
            Degree = request.Degree,
            Gpa = request.Gpa,
            Status = CandidateStatus.InProgress,
            Skills = matchedSkills
                .Select(s => new CandidateSkill { SkillId = s.SkillId, Skill = s, Source = SkillSource.SelfReported })
                .ToList(),
        };

        await _candidates.AddAsync(candidate, ct);
        await _candidates.SaveChangesAsync(ct);

        await _folderProvisioning.PublishAsync(
            new FolderProvisioningJob(FolderProvisioningTargetType.Candidate, candidate.CandidateId), ct);

        var created = await _candidates.GetWithSkillsAsync(candidate.CandidateId, ct);
        var risk = await _candidates.GetRiskAsync(candidate.CandidateId, ct);
        // Candidate-only endpoint — the mapper's role gate means suggestedHireOutcome could
        // never be shown here regardless, so skip the extra query and pass null directly.
        return CreatedAtAction(nameof(GetMe), null, _mapper.ToDto(created!, risk, null, User));
    }

    /// <summary>The signed-in Candidate's own profile — resolved server-side from their EntraObjectId.</summary>
    [HttpGet("me")]
    [Authorize(Roles = Roles.Candidate)]
    public async Task<ActionResult<CandidateDto>> GetMe(CancellationToken ct)
    {
        var candidate = await _candidates.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (candidate is null) return NotFound();

        var risk = await _candidates.GetRiskAsync(candidate.CandidateId, ct);
        // Candidate-only endpoint — see CreateMe's identical reasoning above.
        return Ok(_mapper.ToDto(candidate, risk, null, User));
    }

    [HttpPut("me")]
    [Authorize(Roles = Roles.Candidate)]
    public async Task<ActionResult<CandidateDto>> UpdateMe(UpdateCandidateProfileRequest request, CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        var candidate = await _candidates.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (candidate is null) return NotFound();

        candidate.Location = request.Location;
        candidate.Availability = request.Availability;
        candidate.GraduationDate = request.GraduationDate;
        candidate.LinkedInUrl = request.LinkedInUrl;
        candidate.PortfolioUrl = request.PortfolioUrl;
        candidate.Bio = request.Bio;
        candidate.School = request.School;
        candidate.Degree = request.Degree;
        candidate.Gpa = request.Gpa;

        var skills = await _skills.GetOrCreateByNamesAsync(request.SkillNames, ct);
        candidate.Skills = skills
            .Select(s => new CandidateSkill { SkillId = s.SkillId, Skill = s, Source = SkillSource.SelfReported })
            .ToList();

        await _candidates.SaveChangesAsync(ct);

        var risk = await _candidates.GetRiskAsync(candidate.CandidateId, ct);
        // Candidate-only endpoint — see CreateMe's identical reasoning above.
        return Ok(_mapper.ToDto(candidate, risk, null, User));
    }

    /// <summary>Ops applies or overrides a candidate's hire outcome — see HireOutcomeRule for
    /// the suggestion this is meant to act on. Sets CandidateStatus directly; this is the only
    /// write path for it outside profile creation (which always starts at InProgress).</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = Roles.ProgramOps)]
    public async Task<ActionResult<CandidateDto>> UpdateStatus(int id, UpdateCandidateStatusRequest request, CancellationToken ct)
    {
        var validation = await _updateStatusValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        var candidate = await _candidates.GetWithSkillsAsync(id, ct);
        if (candidate is null) return NotFound();

        candidate.Status = request.Status;
        await _candidates.SaveChangesAsync(ct);
        await _auditLog.RecordAsync(
            _currentUser.EntraObjectId, "Candidate", id.ToString(), "StatusChange",
            reason: request.Reason, data: new { request.Status }, ct: ct);

        var risk = await _candidates.GetRiskAsync(id, ct);
        var suggestion = await ComputeSuggestedHireOutcomeAsync(id, risk, ct);
        return Ok(_mapper.ToDto(candidate, risk, suggestion, User));
    }
}
