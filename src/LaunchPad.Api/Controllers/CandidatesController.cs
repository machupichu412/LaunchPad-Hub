using FluentValidation;
using LaunchPad.Application.Assignments;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using LaunchPad.Application.Community;
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
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<UpdateCandidateProfileRequest> _updateValidator;
    private readonly IAssignmentRepository _assignments;
    private readonly ICommunityRepository _community;

    public CandidatesController(
        ICandidateRepository candidates,
        ICandidateDtoMapper mapper,
        ISkillRepository skills,
        ICurrentUser currentUser,
        IValidator<UpdateCandidateProfileRequest> updateValidator,
        IAssignmentRepository assignments,
        ICommunityRepository community)
    {
        _candidates = candidates;
        _mapper = mapper;
        _skills = skills;
        _currentUser = currentUser;
        _updateValidator = updateValidator;
        _assignments = assignments;
        _community = community;
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.ViewTalentPipeline)]
    public async Task<ActionResult<CandidateDto>> Get(int id, CancellationToken ct)
    {
        var candidate = await _candidates.GetWithSkillsAsync(id, ct);
        if (candidate is null) return NotFound();

        // Redaction happens inside the mapper — never filter scores here or in the client.
        var risk = await _candidates.GetRiskAsync(id, ct);
        return Ok(_mapper.ToDto(candidate, risk, User));
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
            dtos.Add(_mapper.ToDto(candidate, risk, User));
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

    /// <summary>The signed-in Candidate's own profile — resolved server-side from their EntraObjectId.</summary>
    [HttpGet("me")]
    [Authorize(Roles = Roles.Candidate)]
    public async Task<ActionResult<CandidateDto>> GetMe(CancellationToken ct)
    {
        var candidate = await _candidates.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (candidate is null) return NotFound();

        var risk = await _candidates.GetRiskAsync(candidate.CandidateId, ct);
        return Ok(_mapper.ToDto(candidate, risk, User));
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
        return Ok(_mapper.ToDto(candidate, risk, User));
    }
}
