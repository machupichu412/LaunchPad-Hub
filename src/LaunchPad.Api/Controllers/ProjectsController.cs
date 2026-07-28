using LaunchPad.Api.Authorization;
using LaunchPad.Application.Common;
using LaunchPad.Application.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.ViewTalentPipeline)]
public class ProjectsController : ControllerBase
{
    private readonly IProjectRepository _projects;
    private readonly IAuthorizationService _authorization;

    public ProjectsController(IProjectRepository projects, IAuthorizationService authorization)
    {
        _projects = projects;
        _authorization = authorization;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDto>> Get(int id, CancellationToken ct)
    {
        var project = await _projects.GetWithSponsorAsync(id, ct);
        if (project is null) return NotFound();

        // Role alone can't express "own project" — resource-based authorization does.
        var auth = await _authorization.AuthorizeAsync(User, project, Policies.ManageOwnProject);
        if (!auth.Succeeded) return Forbid();

        return Ok(new ProjectDto
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
        });
    }
}
