using FluentValidation;
using LaunchPad.Application.Cohorts;
using LaunchPad.Application.Common;
using LaunchPad.Application.SharePoint;
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
    private readonly IFolderProvisioningJobPublisher _folderProvisioning;

    public CohortsController(
        ICohortRepository cohorts,
        IValidator<CreateCohortRequest> createValidator,
        IFolderProvisioningJobPublisher folderProvisioning)
    {
        _cohorts = cohorts;
        _createValidator = createValidator;
        _folderProvisioning = folderProvisioning;
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

        await _folderProvisioning.PublishAsync(
            new FolderProvisioningJob(FolderProvisioningTargetType.Cohort, cohort.CohortId), ct);

        var created = (await _cohorts.GetAllWithCountsAsync(ct)).First(c => c.Cohort.CohortId == cohort.CohortId);
        return Ok(ToDto(created));
    }

    /// <summary>Changes a cohort's status (Planned/Active/Completed). No auto-deactivation
    /// of other cohorts — Ops decides how many are Active at once; candidate self-onboarding
    /// (CandidatesController.CreateMe) treats zero or multiple Active cohorts as ambiguous
    /// and 409s rather than guessing, so Ops is expected to keep exactly one Active when
    /// candidates need to onboard.</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = Roles.ProgramOps)]
    public async Task<ActionResult<CohortDto>> UpdateStatus(int id, UpdateCohortStatusRequest request, CancellationToken ct)
    {
        var cohort = await _cohorts.GetByIdAsync(id, ct);
        if (cohort is null) return NotFound();

        cohort.Status = request.Status;
        await _cohorts.SaveChangesAsync(ct);

        var updated = (await _cohorts.GetAllWithCountsAsync(ct)).First(c => c.Cohort.CohortId == id);
        return Ok(ToDto(updated));
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
        SharePointFolderWebUrl = summary.Cohort.SharePointFolderWebUrl,
    };
}
