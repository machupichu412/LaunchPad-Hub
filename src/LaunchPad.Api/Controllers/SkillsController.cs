using FluentValidation;
using LaunchPad.Application.Common;
using LaunchPad.Application.Skills;
using LaunchPad.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

/// <summary>
/// The normalized skill taxonomy — browsable by any authenticated role (skills
/// aren't sensitive data), with a Candidate-only create action backing the
/// onboarding skill picker's "add a new skill" flow. Broadening create access to
/// other roles' own pickers (Sponsor project-skill entry, etc.) is a natural future
/// extension, not needed for this pass.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SkillsController : ControllerBase
{
    private readonly ISkillRepository _skills;
    private readonly IValidator<CreateSkillRequest> _createValidator;

    public SkillsController(ISkillRepository skills, IValidator<CreateSkillRequest> createValidator)
    {
        _skills = skills;
        _createValidator = createValidator;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<SkillDto>>> Get(CancellationToken ct)
    {
        var skills = await _skills.GetAllAsync(ct);
        return Ok(skills.Select(ToDto).ToArray());
    }

    [HttpGet("categories")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<SkillCategoryDto>>> GetCategories(CancellationToken ct)
    {
        var categories = await _skills.GetCategoriesAsync(ct);
        return Ok(categories.Select(c => new SkillCategoryDto { SkillCategoryId = c.SkillCategoryId, Name = c.Name }).ToArray());
    }

    [HttpPost]
    [Authorize(Roles = Roles.Candidate)]
    public async Task<ActionResult<SkillDto>> Create(CreateSkillRequest request, CancellationToken ct)
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

        var skill = await _skills.CreateAsync(request.Name, request.SkillCategoryId, ct);
        return Ok(ToDto(skill));
    }

    private static SkillDto ToDto(Skill skill) => new()
    {
        SkillId = skill.SkillId,
        Name = skill.Name,
        SkillCategoryId = skill.SkillCategoryId,
        SkillCategoryName = skill.SkillCategory.Name,
    };
}
