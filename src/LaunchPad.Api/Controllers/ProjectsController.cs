using FluentValidation;
using LaunchPad.Api.Authorization;
using LaunchPad.Application.Assignments;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using LaunchPad.Application.Matching;
using LaunchPad.Application.Notifications;
using LaunchPad.Application.Projects;
using LaunchPad.Application.Skills;
using LaunchPad.Application.Sponsors;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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
    private readonly IAssignmentRepository _assignments;
    private readonly IProjectInterestRepository _projectInterests;
    private readonly IAppUserRepository _appUsers;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly INotificationPublisher _notifications;
    private readonly IConfiguration _configuration;
    private readonly IValidator<CreateProjectRequest> _createValidator;
    private readonly IValidator<UpdateProjectRequest> _updateValidator;
    private readonly IValidator<RejectProjectRequest> _rejectValidator;
    private readonly IValidator<RateInterestRequest> _rateInterestValidator;

    public ProjectsController(
        IProjectRepository projects,
        ISponsorRepository sponsors,
        ICandidateRepository candidates,
        ISkillRepository skills,
        IAssignmentRepository assignments,
        IProjectInterestRepository projectInterests,
        IAppUserRepository appUsers,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        INotificationPublisher notifications,
        IConfiguration configuration,
        IValidator<CreateProjectRequest> createValidator,
        IValidator<UpdateProjectRequest> updateValidator,
        IValidator<RejectProjectRequest> rejectValidator,
        IValidator<RateInterestRequest> rateInterestValidator)
    {
        _projects = projects;
        _sponsors = sponsors;
        _candidates = candidates;
        _skills = skills;
        _assignments = assignments;
        _projectInterests = projectInterests;
        _appUsers = appUsers;
        _currentUser = currentUser;
        _authorization = authorization;
        _notifications = notifications;
        _configuration = configuration;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _rejectValidator = rejectValidator;
        _rateInterestValidator = rateInterestValidator;
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

    /// <summary>Open, Ops-approved projects in the signed-in Candidate's own cohort — the
    /// marketplace's browse list, each annotated with the candidate's own interest rating.</summary>
    [HttpGet("open")]
    [Authorize(Roles = Roles.Candidate)]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetOpenProjects(CancellationToken ct)
    {
        var candidate = await _candidates.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (candidate is null) return Ok(Array.Empty<ProjectDto>());

        var projects = await _projects.GetOpenByCohortAsync(candidate.CohortId, ct);
        var ratings = await _projectInterests.GetRatingsForCandidateAsync(candidate.CandidateId, ct);

        var dtos = projects.Select(ToDto).ToArray();
        foreach (var dto in dtos)
        {
            if (ratings.TryGetValue(dto.ProjectId, out var rating)) dto.MyInterestRating = rating;
        }

        return Ok(dtos);
    }

    /// <summary>One open, Ops-approved project's full detail — the marketplace's detail
    /// view. 404s for anything not currently Open+Approved, same visibility rule as
    /// GetOpenProjects, so a candidate can't browse into a draft/closed project by ID.</summary>
    [HttpGet("{id:int}/open-detail")]
    [Authorize(Roles = Roles.Candidate)]
    public async Task<ActionResult<ProjectDto>> GetOpenDetail(int id, CancellationToken ct)
    {
        var project = await _projects.GetWithSponsorAsync(id, ct);
        if (project is null
            || project.ApprovalStatus != Domain.Enums.ProjectApprovalStatus.Approved
            || project.Status != Domain.Enums.ProjectStatus.Open)
        {
            return NotFound();
        }

        var dto = ToDto(project);

        var candidate = await _candidates.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (candidate is not null)
        {
            var ratings = await _projectInterests.GetRatingsForCandidateAsync(candidate.CandidateId, ct);
            if (ratings.TryGetValue(id, out var rating)) dto.MyInterestRating = rating;
        }

        return Ok(dto);
    }

    /// <summary>Candidate rates (or re-rates) their interest in an open project — a small,
    /// non-gating signal the matching engine nudges scores with. Only allowed while the
    /// project is actually browsable (Open+Approved).</summary>
    [HttpPost("{id:int}/interest")]
    [Authorize(Roles = Roles.Candidate)]
    public async Task<ActionResult<ProjectDto>> RateInterest(int id, RateInterestRequest request, CancellationToken ct)
    {
        var validation = await _rateInterestValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(AddErrors(validation));

        var project = await _projects.GetWithSponsorAsync(id, ct);
        if (project is null
            || project.ApprovalStatus != Domain.Enums.ProjectApprovalStatus.Approved
            || project.Status != Domain.Enums.ProjectStatus.Open)
        {
            return NotFound();
        }

        var candidate = await _candidates.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (candidate is null) return Forbid();

        await _projectInterests.UpsertAsync(candidate.CandidateId, id, request.Rating, ct);
        await _projectInterests.SaveChangesAsync(ct);

        var dto = ToDto(project);
        dto.MyInterestRating = request.Rating;
        return Ok(dto);
    }

    /// <summary>All projects in a cohort, any status — the Program Ops catalog view. Distinct from
    /// GetOpenProjects (Candidate-facing, Open+Approved only).</summary>
    [HttpGet("cohort/{cohortId:int}")]
    [Authorize(Policy = Policies.ViewTalentPipeline)]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetByCohort(int cohortId, CancellationToken ct)
    {
        var projects = await _projects.GetByCohortAsync(cohortId, ct);
        return Ok(projects.Select(ToDto).ToArray());
    }

    /// <summary>PendingOps projects in a cohort — the Ops approval queue. A thin filtered
    /// view over GetByCohort's data, not a separate data source.</summary>
    [HttpGet("pending-approval")]
    [Authorize(Policy = Policies.ApproveProject)]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetPendingApproval([FromQuery] int cohortId, CancellationToken ct)
    {
        var projects = await _projects.GetByCohortAsync(cohortId, ct);
        return Ok(projects.Where(p => p.ApprovalStatus == ProjectApprovalStatus.PendingOps).Select(ToDto).ToArray());
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

    /// <summary>Sponsor submits a Draft or previously-Rejected project for Ops review:
    /// -> PendingOps. Clears any prior RejectionReason — this is a fresh submission.</summary>
    [HttpPost("{id:int}/submit")]
    [Authorize(Roles = Roles.Sponsor)]
    public async Task<ActionResult<ProjectDto>> Submit(int id, CancellationToken ct)
    {
        var project = await _projects.GetWithSponsorAsync(id, ct);
        if (project is null) return NotFound();

        var auth = await _authorization.AuthorizeAsync(User, project, Policies.ManageOwnProject);
        if (!auth.Succeeded) return Forbid();

        if (project.ApprovalStatus != Domain.Enums.ProjectApprovalStatus.Draft
            && project.ApprovalStatus != Domain.Enums.ProjectApprovalStatus.Rejected)
        {
            return BadRequest("Only a Draft or Rejected project can be submitted for approval.");
        }

        project.ApprovalStatus = Domain.Enums.ProjectApprovalStatus.PendingOps;
        project.RejectionReason = null;

        await _projects.SaveChangesAsync(ct);

        var sponsorUpn = project.Sponsor.AppUser.Upn;
        await _notifications.PublishAsync(new NotificationMessage(
            sponsorUpn,
            $"Project submitted for approval: {project.Name}",
            $"Your project \"{project.Name}\" has been submitted to Program Ops for approval. " +
            "You'll get another email once a decision is made."), ct);

        var programOpsUpn = _configuration["Notifications:ProgramOpsUpn"];
        if (!string.IsNullOrWhiteSpace(programOpsUpn))
        {
            await _notifications.PublishAsync(new NotificationMessage(
                programOpsUpn,
                $"New project pending approval: {project.Name}",
                $"{project.Sponsor.AppUser.DisplayName} submitted \"{project.Name}\" for review. " +
                "Visit the Project Approvals queue to approve or reject it."), ct);
        }

        return Ok(ToDto(project));
    }

    /// <summary>Ops approves a PendingOps project: -> Approved. Now visible to candidates
    /// via GetOpenProjects/GetOpenDetail.</summary>
    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Policies.ApproveProject)]
    public async Task<ActionResult<ProjectDto>> Approve(int id, CancellationToken ct)
    {
        var project = await _projects.GetWithSponsorAsync(id, ct);
        if (project is null) return NotFound();

        if (project.ApprovalStatus != Domain.Enums.ProjectApprovalStatus.PendingOps)
        {
            return BadRequest("Only a PendingOps project can be approved.");
        }

        project.ApprovalStatus = Domain.Enums.ProjectApprovalStatus.Approved;

        await _projects.SaveChangesAsync(ct);

        await _notifications.PublishAsync(new NotificationMessage(
            project.Sponsor.AppUser.Upn,
            $"Project approved: {project.Name}",
            $"Your project \"{project.Name}\" has been approved and is now visible to candidates on the marketplace."), ct);

        return Ok(ToDto(project));
    }

    /// <summary>Ops rejects a PendingOps project: -> Rejected, with a reason the sponsor
    /// can act on. The sponsor can edit and resubmit (Submit accepts Rejected too).</summary>
    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Policies.ApproveProject)]
    public async Task<ActionResult<ProjectDto>> Reject(int id, RejectProjectRequest request, CancellationToken ct)
    {
        var validation = await _rejectValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(AddErrors(validation));

        var project = await _projects.GetWithSponsorAsync(id, ct);
        if (project is null) return NotFound();

        if (project.ApprovalStatus != Domain.Enums.ProjectApprovalStatus.PendingOps)
        {
            return BadRequest("Only a PendingOps project can be rejected.");
        }

        project.ApprovalStatus = Domain.Enums.ProjectApprovalStatus.Rejected;
        project.RejectionReason = request.Reason;

        await _projects.SaveChangesAsync(ct);

        await _notifications.PublishAsync(new NotificationMessage(
            project.Sponsor.AppUser.Upn,
            $"Project not approved: {project.Name}",
            $"Your project \"{project.Name}\" was not approved. Reason: {request.Reason}. " +
            "You can edit and resubmit it for another review."), ct);

        return Ok(ToDto(project));
    }

    /// <summary>Proposed candidates awaiting the sponsor's pick — up to 3, from the matching engine's
    /// top-N ranking (see MatchingController.Run). Ownership-checked the same way Get/Update are.</summary>
    [HttpGet("{id:int}/matches")]
    [Authorize(Policy = Policies.ViewTalentPipeline)]
    public async Task<ActionResult<IReadOnlyList<ProjectMatchDto>>> GetMatches(int id, CancellationToken ct)
    {
        var project = await _projects.GetWithSponsorAsync(id, ct);
        if (project is null) return NotFound();

        var auth = await _authorization.AuthorizeAsync(User, project, Policies.ManageOwnProject);
        if (!auth.Succeeded) return Forbid();

        var matches = await _assignments.GetProposedByProjectAsync(id, ct);
        return Ok(matches.Select(ToMatchDto).ToArray());
    }

    /// <summary>Sponsor picks one of the proposed candidates: Proposed -> SponsorApproved. The other
    /// candidates proposed for this same project are auto-withdrawn — one project, one pick.</summary>
    [HttpPost("{id:int}/matches/{assignmentId:int}/recommend")]
    [Authorize(Roles = Roles.Sponsor)]
    public async Task<ActionResult<ProjectMatchDto>> RecommendMatch(int id, int assignmentId, CancellationToken ct)
    {
        var assignment = await _assignments.GetAsync(assignmentId, ct);
        if (assignment is null || assignment.ProjectId != id) return NotFound();

        var auth = await _authorization.AuthorizeAsync(User, assignment.Project, Policies.ManageOwnProject);
        if (!auth.Succeeded) return Forbid();

        if (assignment.Status != AssignmentStatus.Proposed)
        {
            return BadRequest("Only a Proposed match can be recommended.");
        }

        var sponsorAppUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);

        assignment.Status = AssignmentStatus.SponsorApproved;
        assignment.SponsorApprovedUtc = DateTime.UtcNow;
        assignment.SponsorApprovedBy = sponsorAppUserId;

        var siblings = await _assignments.GetProposedByProjectAsync(id, ct);
        foreach (var sibling in siblings.Where(a => a.AssignmentId != assignmentId))
        {
            sibling.Status = AssignmentStatus.Withdrawn;
        }

        await _assignments.SaveChangesAsync(ct);
        return Ok(ToMatchDto(assignment));
    }

    /// <summary>Sponsor declines one of the proposed candidates: Proposed -> Withdrawn. No cascade —
    /// the other proposed candidates for this project are unaffected.</summary>
    [HttpPost("{id:int}/matches/{assignmentId:int}/reject")]
    [Authorize(Roles = Roles.Sponsor)]
    public async Task<ActionResult<ProjectMatchDto>> RejectMatch(int id, int assignmentId, CancellationToken ct)
    {
        var assignment = await _assignments.GetAsync(assignmentId, ct);
        if (assignment is null || assignment.ProjectId != id) return NotFound();

        var auth = await _authorization.AuthorizeAsync(User, assignment.Project, Policies.ManageOwnProject);
        if (!auth.Succeeded) return Forbid();

        if (assignment.Status != AssignmentStatus.Proposed)
        {
            return BadRequest("Only a Proposed match can be rejected.");
        }

        assignment.Status = AssignmentStatus.Withdrawn;
        await _assignments.SaveChangesAsync(ct);
        return Ok(ToMatchDto(assignment));
    }

    private static ProjectMatchDto ToMatchDto(Assignment assignment) => new()
    {
        AssignmentId = assignment.AssignmentId,
        CandidateId = assignment.CandidateId,
        CandidateName = assignment.Candidate.AppUser.DisplayName,
        MatchScore = assignment.MatchScore,
        MatchRationale = assignment.MatchRationale,
    };

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
        SponsorName = project.Sponsor.AppUser.DisplayName,
        Name = project.Name,
        Description = project.Description,
        AvailabilityNeeded = project.AvailabilityNeeded,
        StartDate = project.StartDate,
        EndDate = project.EndDate,
        ApprovalStatus = project.ApprovalStatus,
        Status = project.Status,
        RejectionReason = project.RejectionReason,
        SponsorTeamsLink = $"https://teams.microsoft.com/l/chat/0/0?users={Uri.EscapeDataString(project.Sponsor.AppUser.Upn)}",
        RequiredSkills = project.Skills
            .Select(s => new ProjectSkillDto { SkillName = s.Skill.Name, Category = s.Skill.SkillCategory.Name, IsRequired = s.IsRequired })
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
