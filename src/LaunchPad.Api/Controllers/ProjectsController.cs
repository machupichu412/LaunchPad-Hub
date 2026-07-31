using FluentValidation;
using LaunchPad.Api.Authorization;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using LaunchPad.Application.Projects;
using LaunchPad.Application.Skills;
using LaunchPad.Application.Sponsors;
using LaunchPad.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

// No class-level policy: GetOpenProjects must reach Candidate, which is
// deliberately excluded from ViewTalentPipeline — same reasoning as
// CandidatesController. Get/Update apply ViewTalentPipeline explicitly instead.
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectRepository _projects;
    private readonly ISponsorRepository _sponsors;
    private readonly ICandidateRepository _candidates;
    private readonly ISkillRepository _skills;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IValidator<CreateProjectRequest> _createValidator;
    private readonly IValidator<UpdateProjectRequest> _updateValidator;

    public ProjectsController(
        IProjectRepository projects,
        ISponsorRepository sponsors,
        ICandidateRepository candidates,
        ISkillRepository skills,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        IValidator<CreateProjectRequest> createValidator,
        IValidator<UpdateProjectRequest> updateValidator)
    {
        _projects = projects;
        _sponsors = sponsors;
        _candidates = candidates;
        _skills = skills;
        _currentUser = currentUser;
        _authorization = authorization;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.ViewTalentPipeline)]
    public async Task<ActionResult<ProjectDto>> Get(int id, CancellationToken ct)
    {
        var project = await _projects.GetWithSponsorAsync(id, ct);
        if (project is null) return NotFound();

        // Role alone can't express "own project" — resource-based authorization does.
        var auth = await _authorization.AuthorizeAsync(User, project, Policies.ManageOwnProject);
        if (!auth.Succeeded) return Forbid();

        return Ok(ToDto(project));
    }

    /// <summary>The signed-in Sponsor's own projects — resolved server-side, never trusts a client-supplied SponsorId.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = Roles.Sponsor)]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetMine(CancellationToken ct)
    {
        var sponsor = await _sponsors.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (sponsor is null) return Ok(Array.Empty<ProjectDto>());

        var projects = await _projects.GetBySponsorAsync(sponsor.SponsorId, ct);
        return Ok(projects.Select(ToDto).ToArray());
    }

    /// <summary>Open, Ops-approved projects in the signed-in Candidate's own cohort — for browsing.</summary>
    [HttpGet("open")]
    [Authorize(Roles = Roles.Candidate)]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetOpenProjects(CancellationToken ct)
    {
        var candidate = await _candidates.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (candidate is null) return Ok(Array.Empty<ProjectDto>());

        var projects = await _projects.GetOpenByCohortAsync(candidate.CohortId, ct);
        return Ok(projects.Select(ToDto).ToArray());
    }

    [HttpPost]
    [Authorize(Roles = Roles.Sponsor)]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(AddErrors(validation));

        var sponsor = await _sponsors.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (sponsor is null) return Forbid();

        var project = new Project
        {
            CohortId = request.CohortId,
            SponsorId = sponsor.SponsorId,
            Name = request.Name,
            Description = request.Description,
            AvailabilityNeeded = request.AvailabilityNeeded,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ApprovalStatus = Domain.Enums.ProjectApprovalStatus.Draft,
            Status = Domain.Enums.ProjectStatus.Open,
        };

        project.Skills = await ResolveSkillsAsync(request.RequiredSkillNames, request.PreferredSkillNames, ct);

        await _projects.AddAsync(project, ct);
        await _projects.SaveChangesAsync(ct);

        var created = await _projects.GetWithSponsorAsync(project.ProjectId, ct);
        return CreatedAtAction(nameof(Get), new { id = project.ProjectId }, ToDto(created!));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.ViewTalentPipeline)]
    public async Task<ActionResult<ProjectDto>> Update(int id, UpdateProjectRequest request, CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(AddErrors(validation));

        var project = await _projects.GetWithSponsorAsync(id, ct);
        if (project is null) return NotFound();

        var auth = await _authorization.AuthorizeAsync(User, project, Policies.ManageOwnProject);
        if (!auth.Succeeded) return Forbid();

        project.Name = request.Name;
        project.Description = request.Description;
        project.AvailabilityNeeded = request.AvailabilityNeeded;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.Skills = await ResolveSkillsAsync(request.RequiredSkillNames, request.PreferredSkillNames, ct);

        await _projects.SaveChangesAsync(ct);

        return Ok(ToDto(project));
    }

    private async Task<List<ProjectSkill>> ResolveSkillsAsync(string[] requiredNames, string[] preferredNames, CancellationToken ct)
    {
        var requiredSkills = await _skills.GetOrCreateByNamesAsync(requiredNames, ct);
        var preferredSkills = await _skills.GetOrCreateByNamesAsync(preferredNames, ct);

        var result = requiredSkills.Select(s => new ProjectSkill { SkillId = s.SkillId, Skill = s, IsRequired = true }).ToList();
        result.AddRange(preferredSkills
            .Where(s => result.All(r => r.SkillId != s.SkillId))
            .Select(s => new ProjectSkill { SkillId = s.SkillId, Skill = s, IsRequired = false }));

        return result;
    }

    private static ProjectDto ToDto(Project project) => new()
    {
        ProjectId = project.ProjectId,
        CohortId = project.CohortId,
        SponsorId = project.SponsorId,
        Name = project.Name,
        Description = project.Description,
        AvailabilityNeeded = project.AvailabilityNeeded,
        StartDate = project.StartDate,
        EndDate = project.EndDate,
        ApprovalStatus = project.ApprovalStatus,
        Status = project.Status,
        RequiredSkills = project.Skills
            .Select(s => new ProjectSkillDto { SkillName = s.Skill.Name, IsRequired = s.IsRequired })
            .ToArray()
    };

    private Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary AddErrors(FluentValidation.Results.ValidationResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }
        return ModelState;
    }
}
