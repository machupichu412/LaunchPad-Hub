using FluentValidation;
using LaunchPad.Api.Authorization;
using LaunchPad.Application.Assignments;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
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
    private readonly IValidator<CreateDeliverableRequest> _deliverableValidator;

    public AssignmentsController(
        IAssignmentRepository assignments,
        ICandidateRepository candidates,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        IValidator<UpdateTodoStatusRequest> todoValidator,
        IValidator<CreateDeliverableRequest> deliverableValidator)
    {
        _assignments = assignments;
        _candidates = candidates;
        _currentUser = currentUser;
        _authorization = authorization;
        _todoValidator = todoValidator;
        _deliverableValidator = deliverableValidator;
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

    [HttpPost("{id:int}/deliverables")]
    public async Task<ActionResult<DeliverableDto>> CreateDeliverable(int id, CreateDeliverableRequest request, CancellationToken ct)
    {
        var validation = await _deliverableValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(AddErrors(validation));

        var auth = await AuthorizeAssignmentAsync(id, ct);
        if (auth.Result is not null) return auth.Result;

        var deliverable = new Deliverable
        {
            AssignmentId = id,
            Title = request.Title,
            FileName = request.FileName,
            Status = DeliverableStatus.Submitted,
        };

        await _assignments.AddDeliverableAsync(deliverable, ct);
        await _assignments.SaveChangesAsync(ct);

        return Ok(deliverable.ToDto());
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
