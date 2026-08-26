using FluentValidation;
using LaunchPad.Application.Assignments;
using LaunchPad.Application.Cohorts;
using LaunchPad.Application.Common;
using LaunchPad.Application.SharePoint;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CohortsController : ControllerBase
{
    private readonly ICohortRepository _cohorts;
    private readonly IAssignmentRepository _assignments;
    private readonly IValidator<CreateCohortRequest> _createValidator;
    private readonly IValidator<ScheduleReviewsRequest> _scheduleReviewsValidator;
    private readonly IFolderProvisioningJobPublisher _folderProvisioning;

    public CohortsController(
        ICohortRepository cohorts,
        IAssignmentRepository assignments,
        IValidator<CreateCohortRequest> createValidator,
        IValidator<ScheduleReviewsRequest> scheduleReviewsValidator,
        IFolderProvisioningJobPublisher folderProvisioning)
    {
        _cohorts = cohorts;
        _assignments = assignments;
        _createValidator = createValidator;
        _scheduleReviewsValidator = scheduleReviewsValidator;
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

    /// <summary>Ops schedules midpoint/final review to-dos for every Active assignment in
    /// the cohort — up to 3 per assignment (SponsorOnCandidate for the Sponsor to act on,
    /// CandidateOnSponsor + ProjectEval for the Candidate). Idempotent: re-invoking for the
    /// same checkpoint only fills in whatever's still missing, backstopped by the
    /// UX_ProjectTodo_LinkedReview_Once index. No notification fan-out — matches the
    /// existing sponsor-created to-do flow, which doesn't notify either.</summary>
    [HttpPost("{id:int}/schedule-reviews")]
    [Authorize(Roles = Roles.ProgramOps)]
    public async Task<ActionResult<ScheduleReviewsResult>> ScheduleReviews(int id, ScheduleReviewsRequest request, CancellationToken ct)
    {
        var validation = await _scheduleReviewsValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        var assignments = await _assignments.GetActiveByCohortAsync(id, ct);
        var assignmentIds = assignments.Select(a => a.AssignmentId).ToList();
        var existingKeys = await _assignments.GetLinkedReviewTodoKeysAsync(assignmentIds, request.Checkpoint, ct);

        var assignmentsScheduled = 0;
        var todosCreated = 0;
        foreach (var assignment in assignments)
        {
            var createdForThisAssignment = 0;

            async Task AddIfMissingAsync(ReviewType reviewType, string title)
            {
                if (existingKeys.Contains((assignment.AssignmentId, reviewType))) return;

                await _assignments.AddTodoAsync(new ProjectTodo
                {
                    AssignmentId = assignment.AssignmentId,
                    Title = title,
                    Status = TodoStatus.NotStarted,
                    DueDate = request.DueDate,
                    LinkedReviewType = reviewType,
                    LinkedReviewCheckpoint = request.Checkpoint,
                }, ct);
                todosCreated++;
                createdForThisAssignment++;
            }

            await AddIfMissingAsync(
                ReviewType.SponsorOnCandidate, $"Submit your {request.Checkpoint} review of {assignment.Candidate.AppUser.DisplayName}");
            await AddIfMissingAsync(
                ReviewType.CandidateOnSponsor, $"Submit your {request.Checkpoint} review of {assignment.Project.Sponsor.AppUser.DisplayName}");
            await AddIfMissingAsync(
                ReviewType.ProjectEval, $"Submit your {request.Checkpoint} review of {assignment.Project.Name}");

            if (createdForThisAssignment > 0) assignmentsScheduled++;
        }

        await _assignments.SaveChangesAsync(ct);

        return Ok(new ScheduleReviewsResult { AssignmentsScheduled = assignmentsScheduled, TodosCreated = todosCreated });
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
