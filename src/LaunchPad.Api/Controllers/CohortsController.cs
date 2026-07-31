using FluentValidation;
using LaunchPad.Application.Cohorts;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CohortsController : ControllerBase
{
    private readonly ICohortRepository _cohorts;
    private readonly IValidator<CreateCohortRequest> _createValidator;

    public CohortsController(ICohortRepository cohorts, IValidator<CreateCohortRequest> createValidator)
    {
        _cohorts = cohorts;
        _createValidator = createValidator;
    }

    [HttpGet]
    [Authorize(Policy = Policies.ViewTalentPipeline)]
    public async Task<ActionResult<IReadOnlyList<CohortDto>>> Get(CancellationToken ct)
    {
        var cohorts = await _cohorts.GetAllWithCountsAsync(ct);
        return Ok(cohorts.Select(ToDto).ToArray());
    }

    [HttpPost]
    [Authorize(Roles = Roles.ProgramOps)]
    public async Task<ActionResult<CohortDto>> Create(CreateCohortRequest request, CancellationToken ct)
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

        var programId = await _cohorts.GetDefaultProgramIdAsync(ct);
        var cohort = new Cohort
        {
            ProgramId = programId,
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = Domain.Enums.CohortStatus.Active,
        };

        await _cohorts.AddAsync(cohort, ct);
        await _cohorts.SaveChangesAsync(ct);

        var created = (await _cohorts.GetAllWithCountsAsync(ct)).First(c => c.Cohort.CohortId == cohort.CohortId);
        return Ok(ToDto(created));
    }

    private static CohortDto ToDto(CohortSummary summary) => new()
    {
        CohortId = summary.Cohort.CohortId,
        ProgramId = summary.Cohort.ProgramId,
        ProgramName = summary.Cohort.Program.Name,
        Name = summary.Cohort.Name,
        StartDate = summary.Cohort.StartDate,
        EndDate = summary.Cohort.EndDate,
        Status = summary.Cohort.Status,
        CandidateCount = summary.CandidateCount,
        ProjectCount = summary.ProjectCount,
    };
}
