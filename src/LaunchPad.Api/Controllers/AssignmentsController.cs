using FluentValidation;
using LaunchPad.Api.Authorization;
using LaunchPad.Application.Assignments;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using LaunchPad.Application.SharePoint;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

// Class-level role gate (Candidate/ProgramOps/Executive) plus a per-action
// resource-based ownership check (ManageOwnAssignment) — same defense-in-depth
// shape as ProjectsController. GetMine has no resource to check yet (no id in the
// route), so it narrows further to Candidate-only via its own attribute.
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.ViewOwnAssignment)]
public class AssignmentsController : ControllerBase
{
    private readonly IAssignmentRepository _assignments;
    private readonly ICandidateRepository _candidates;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IValidator<UpdateTodoStatusRequest> _todoValidator;
    private readonly IValidator<CreateTodoRequest> _createTodoValidator;
    private readonly IValidator<SubmitDeliverableRequest> _deliverableValidator;
    private readonly IFolderProvisioner _folderProvisioner;
    private readonly IDocumentStorage _documentStorage;

    public AssignmentsController(
        IAssignmentRepository assignments,
        ICandidateRepository candidates,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        IValidator<UpdateTodoStatusRequest> todoValidator,
        IValidator<CreateTodoRequest> createTodoValidator,
        IValidator<SubmitDeliverableRequest> deliverableValidator,
        IFolderProvisioner folderProvisioner,
        IDocumentStorage documentStorage)
    {
        _assignments = assignments;
        _candidates = candidates;
        _currentUser = currentUser;
        _authorization = authorization;
        _todoValidator = todoValidator;
        _createTodoValidator = createTodoValidator;
        _deliverableValidator = deliverableValidator;
        _folderProvisioner = folderProvisioner;
        _documentStorage = documentStorage;
    }

    /// <summary>The signed-in Candidate's own active assignment — resolved server-side, never a client-supplied ID.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = Roles.Candidate)]
    public async Task<ActionResult<MyAssignmentDto>> GetMine(CancellationToken ct)
    {
        var candidate = await _candidates.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (candidate is null) return NotFound();

        var assignment = await _assignments.GetActiveByCandidateIdAsync(candidate.CandidateId, ct);
        if (assignment is null) return NotFound();

        var todos = await _assignments.GetTodosAsync(assignment.AssignmentId, ct);
        return Ok(assignment.ToMyAssignmentDto(todos.Count, todos.Count(t => t.Status == TodoStatus.Completed)));
    }

    [HttpGet("{id:int}/todos")]
    public async Task<ActionResult<IReadOnlyList<ProjectTodoDto>>> GetTodos(int id, CancellationToken ct)
    {
        var auth = await AuthorizeAssignmentAsync(id, ct);
        if (auth.Result is not null) return auth.Result;

        var todos = await _assignments.GetTodosAsync(id, ct);
        return Ok(todos.Select(t => t.ToDto()).ToArray());
    }

    /// <summary>Sponsor (or Ops/Exec) establishes a to-do item — never a Candidate, who can
    /// only check todos off via UpdateTodoStatus below.</summary>
    [HttpPost("{id:int}/todos")]
    public async Task<ActionResult<ProjectTodoDto>> CreateTodo(int id, CreateTodoRequest request, CancellationToken ct)
    {
        var validation = await _createTodoValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(AddErrors(validation));

        if (User.IsInRole(Roles.Candidate)) return Forbid();

        var auth = await AuthorizeAssignmentAsync(id, ct);
        if (auth.Result is not null) return auth.Result;

        var todo = new ProjectTodo
        {
            AssignmentId = id,
            Title = request.Title,
            Priority = request.Priority,
            DueDate = request.DueDate,
            Status = TodoStatus.NotStarted,
        };

        await _assignments.AddTodoAsync(todo, ct);
        await _assignments.SaveChangesAsync(ct);

        return Ok(todo.ToDto());
    }

    [HttpPatch("{id:int}/todos/{todoId:int}")]
    public async Task<ActionResult<ProjectTodoDto>> UpdateTodoStatus(int id, int todoId, UpdateTodoStatusRequest request, CancellationToken ct)
    {
        var validation = await _todoValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(AddErrors(validation));

        var auth = await AuthorizeAssignmentAsync(id, ct);
        if (auth.Result is not null) return auth.Result;

        var todo = await _assignments.GetTodoAsync(id, todoId, ct);
        if (todo is null) return NotFound();

        todo.Status = request.Status;
        todo.CompletedUtc = request.Status == TodoStatus.Completed ? DateTime.UtcNow : null;
        await _assignments.SaveChangesAsync(ct);

        return Ok(todo.ToDto());
    }

    [HttpGet("{id:int}/deliverables")]
    public async Task<ActionResult<IReadOnlyList<DeliverableDto>>> GetDeliverables(int id, CancellationToken ct)
    {
        var auth = await AuthorizeAssignmentAsync(id, ct);
        if (auth.Result is not null) return auth.Result;

        var deliverables = await _assignments.GetDeliverablesAsync(id, ct);
        return Ok(deliverables.Select(d => d.ToDto()).ToArray());
    }

    /// <summary>Streams a candidate's deliverable file straight to their SharePoint folder via
    /// Graph. Self-heals the folder inline (synchronously) if it hasn't been provisioned yet by
    /// the time they need to upload — the one place this feature provisions synchronously,
    /// justified by the candidate being blocked on this exact action right now (see the plan's
    /// design-decisions section).</summary>
    [HttpPost("{id:int}/deliverables")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 110 * 1024 * 1024)]
    public async Task<ActionResult<DeliverableDto>> SubmitDeliverable(
        int id, [FromForm] string title, [FromForm] int? projectTodoId, [FromForm] IFormFile? file, CancellationToken ct)
    {
        var request = new SubmitDeliverableRequest
        {
            Title = title,
            FileName = file?.FileName ?? string.Empty,
            ContentType = file?.ContentType ?? string.Empty,
            ContentLength = file?.Length ?? 0,
            ProjectTodoId = projectTodoId,
        };

        var validation = await _deliverableValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(AddErrors(validation));

        var (assignment, authResult) = await AuthorizeAssignmentAsync(id, ct);
        if (authResult is not null) return authResult;

        ProjectTodo? attachedTodo = null;
        if (request.ProjectTodoId is int todoId)
        {
            attachedTodo = await _assignments.GetTodoAsync(id, todoId, ct);
            if (attachedTodo is null) return BadRequest("That to-do item doesn't belong to this assignment.");
        }

        var candidate = assignment!.Candidate;
        if (candidate.SharePointFolderId is null)
        {
            var (folderId, webUrl) = await _folderProvisioner.EnsureCandidateFolderAsync(
                candidate.Cohort.Name, candidate.AppUser.DisplayName, ct);
            candidate.SharePointFolderId = folderId;
            candidate.SharePointFolderWebUrl = webUrl;
        }

        await using var stream = file!.OpenReadStream();
        var sharePointItemId = await _documentStorage.SaveAsync(
            candidate.SharePointFolderId, request.FileName, stream, request.ContentType, request.ContentLength, ct);

        var deliverable = new Deliverable
        {
            AssignmentId = id,
            Title = request.Title,
            FileName = request.FileName,
            ProjectTodoId = request.ProjectTodoId,
            ProjectTodo = attachedTodo,
            Status = DeliverableStatus.Submitted,
            SharePointItemId = sharePointItemId,
        };

        await _assignments.AddDeliverableAsync(deliverable, ct);
        // Saves the new deliverable and the candidate's backfilled folder fields together.
        await _assignments.SaveChangesAsync(ct);

        return Ok(deliverable.ToDto());
    }

    /// <summary>Proxies a deliverable's file content from SharePoint — mirrors
    /// CandidatesController.GetAvatar's shape. The client never talks to Graph directly.</summary>
    [HttpGet("{id:int}/deliverables/{deliverableId:int}/file")]
    public async Task<IActionResult> GetDeliverableFile(int id, int deliverableId, CancellationToken ct)
    {
        var auth = await AuthorizeAssignmentAsync(id, ct);
        if (auth.Result is not null) return auth.Result;

        var deliverable = await _assignments.GetDeliverableAsync(id, deliverableId, ct);
        if (deliverable?.SharePointItemId is not { } itemId) return NotFound();

        var result = await _documentStorage.GetAsync(itemId, ct);
        if (result is null) return NotFound();

        return File(result.Value.Content, result.Value.ContentType, deliverable.FileName);
    }

    /// <summary>Candidate-safe evaluation view — never returns a numeric or star rating. See CandidateEvaluationDto.</summary>
    [HttpGet("{id:int}/evaluations")]
    public async Task<ActionResult<IReadOnlyList<CandidateEvaluationDto>>> GetEvaluations(int id, CancellationToken ct)
    {
        var auth = await AuthorizeAssignmentAsync(id, ct);
        if (auth.Result is not null) return auth.Result;

        var reviews = await _assignments.GetCandidateEvaluationsAsync(id, ct);
        return Ok(reviews.Select(r => r.ToCandidateEvaluationDto()).ToArray());
    }

    private async Task<(Assignment? Assignment, ActionResult? Result)> AuthorizeAssignmentAsync(int assignmentId, CancellationToken ct)
    {
        var assignment = await _assignments.GetWithOwnershipDetailsAsync(assignmentId, ct);
        if (assignment is null) return (null, NotFound());

        var auth = await _authorization.AuthorizeAsync(User, assignment, Policies.ManageOwnAssignment);
        if (!auth.Succeeded) return (null, Forbid());

        return (assignment, null);
    }

    private Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary AddErrors(FluentValidation.Results.ValidationResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }
        return ModelState;
    }
}
