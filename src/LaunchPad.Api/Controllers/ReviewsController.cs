using FluentValidation;
using LaunchPad.Application.Assignments;
using LaunchPad.Application.Common;
using LaunchPad.Application.Reviews;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

// Submit handles all three ReviewType values (Sponsor reviewing Candidate, Candidate
// reviewing Sponsor, Candidate reviewing the project) — see its role/ownership branch
// below. No class-level role restriction because of that; GetByAssignment stays
// Sponsor-only via its own attribute (unchanged scope — viewing the other two types
// isn't needed yet). Every response is a SponsorReviewDto, never the raw ReviewDto
// (which still carries unredacted OverallScore/numeric dimensions and must stay
// internal/Ops-only) — CLAUDE.md's redaction rule applies the same regardless of who submitted.
[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IAssignmentRepository _assignments;
    private readonly IReviewRepository _reviews;
    private readonly IAppUserRepository _appUsers;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditLog _auditLog;
    private readonly IValidator<SubmitReviewRequest> _validator;

    public ReviewsController(
        IAssignmentRepository assignments,
        IReviewRepository reviews,
        IAppUserRepository appUsers,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        IAuditLog auditLog,
        IValidator<SubmitReviewRequest> validator)
    {
        _assignments = assignments;
        _reviews = reviews;
        _appUsers = appUsers;
        _currentUser = currentUser;
        _authorization = authorization;
        _auditLog = auditLog;
        _validator = validator;
    }

    [HttpPost]
    public async Task<ActionResult<SponsorReviewDto>> Submit(SubmitReviewRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        var assignment = await _assignments.GetWithOwnershipDetailsAsync(request.AssignmentId, ct);
        if (assignment is null) return NotFound();

        if (assignment.Status != AssignmentStatus.Active)
        {
            return BadRequest("Reviews can only be submitted for an active assignment.");
        }

        // Who may submit which ReviewType, and how ownership is checked, differs by type —
        // a Sponsor reviews a Candidate on a project they own; a Candidate reviews their
        // own Sponsor or their own project's assignment. Anything else is forbidden.
        int? submittedByAppUserId;
        if (request.ReviewType == ReviewType.SponsorOnCandidate)
        {
            if (!User.IsInRole(Roles.Sponsor)) return Forbid();
            var auth = await _authorization.AuthorizeAsync(User, assignment.Project, Policies.ManageOwnProject);
            if (!auth.Succeeded) return Forbid();
            submittedByAppUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        }
        else
        {
            if (!User.IsInRole(Roles.Candidate)) return Forbid();
            var auth = await _authorization.AuthorizeAsync(User, assignment, Policies.ManageOwnAssignment);
            if (!auth.Succeeded) return Forbid();
            submittedByAppUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        }

        var review = new Review
        {
            AssignmentId = request.AssignmentId,
            ReviewType = request.ReviewType,
            Checkpoint = request.Checkpoint,
            SubmittedBy = submittedByAppUserId ?? 0,
            Commitment = request.Commitment,
            Availability = request.Availability,
            Guidance = request.Guidance,
            OutputQuality = request.OutputQuality,
            Comments = request.Comments,
            Strengths = request.Strengths,
            GrowthAreas = request.GrowthAreas,
            RecommendConversion = request.RecommendConversion,
        };

        await _reviews.AddAsync(review, ct);

        // Auto-completes the matching Ops-scheduled review to-do, if one exists — see
        // ProjectTodo.LinkedReviewType's doc comment. A self-serve review submitted without
        // ever being scheduled (e.g. the sponsor's original ungated flow) simply has no
        // linked to-do to find, which is fine.
        var linkedTodo = await _assignments.GetLinkedReviewTodoAsync(request.AssignmentId, request.ReviewType, request.Checkpoint, ct);
        if (linkedTodo is not null && linkedTodo.Status != TodoStatus.Completed)
        {
            linkedTodo.Status = TodoStatus.Completed;
            linkedTodo.CompletedUtc = DateTime.UtcNow;
        }

        // Reviews and Assignments share the same scoped DbContext, so one SaveChangesAsync
        // persists both the new review and the linked to-do's completion together.
        await _reviews.SaveChangesAsync(ct);
        await _auditLog.RecordAsync(
            _currentUser.EntraObjectId, "Review", review.ReviewId.ToString(), "Submit",
            data: new { review.AssignmentId, review.ReviewType, review.Checkpoint }, ct: ct);

        return Ok(ToDto(review));
    }

    [HttpGet("assignment/{assignmentId:int}")]
    [Authorize(Roles = Roles.Sponsor)]
    public async Task<ActionResult<IReadOnlyList<SponsorReviewDto>>> GetByAssignment(int assignmentId, CancellationToken ct)
    {
        var assignment = await _assignments.GetAsync(assignmentId, ct);
        if (assignment is null) return NotFound();

        var auth = await _authorization.AuthorizeAsync(User, assignment.Project, Policies.ManageOwnProject);
        if (!auth.Succeeded) return Forbid();

        var reviews = await _reviews.GetByAssignmentAsync(assignmentId, ct);
        return Ok(reviews.Where(r => r.ReviewType == ReviewType.SponsorOnCandidate).Select(ToDto).ToArray());
    }

    private static SponsorReviewDto ToDto(Review review) => new()
    {
        ReviewId = review.ReviewId,
        AssignmentId = review.AssignmentId,
        Checkpoint = review.Checkpoint,
        SubmittedUtc = review.SubmittedUtc,
        Comments = review.Comments,
        Strengths = review.Strengths,
        GrowthAreas = review.GrowthAreas,
        RecommendConversion = review.RecommendConversion,
    };
}
