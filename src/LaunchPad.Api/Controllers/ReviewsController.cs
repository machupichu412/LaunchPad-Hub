using FluentValidation;
using LaunchPad.Application.Assignments;
using LaunchPad.Application.Common;
using LaunchPad.Application.Reviews;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

// Sponsor-only in this pass — CandidateOnSponsor reviews aren't part of this build.
// Every response is a SponsorReviewDto, never the raw ReviewDto (which still carries
// unredacted OverallScore/numeric dimensions and must stay internal/Ops-only).
[ApiController]
[Route("api/reviews")]
[Authorize(Roles = Roles.Sponsor)]
public class ReviewsController : ControllerBase
{
    private readonly IAssignmentRepository _assignments;
    private readonly IReviewRepository _reviews;
    private readonly IAppUserRepository _appUsers;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IValidator<SubmitReviewRequest> _validator;

    public ReviewsController(
        IAssignmentRepository assignments,
        IReviewRepository reviews,
        IAppUserRepository appUsers,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        IValidator<SubmitReviewRequest> validator)
    {
        _assignments = assignments;
        _reviews = reviews;
        _appUsers = appUsers;
        _currentUser = currentUser;
        _authorization = authorization;
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

        var assignment = await _assignments.GetAsync(request.AssignmentId, ct);
        if (assignment is null) return NotFound();

        var auth = await _authorization.AuthorizeAsync(User, assignment.Project, Policies.ManageOwnProject);
        if (!auth.Succeeded) return Forbid();

        if (assignment.Status != AssignmentStatus.Active)
        {
            return BadRequest("Reviews can only be submitted for an active assignment.");
        }

        var sponsorAppUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);

        var review = new Review
        {
            AssignmentId = request.AssignmentId,
            ReviewType = ReviewType.SponsorOnCandidate,
            Checkpoint = request.Checkpoint,
            SubmittedBy = sponsorAppUserId ?? 0,
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
        await _reviews.SaveChangesAsync(ct);

        return Ok(ToDto(review));
    }

    [HttpGet("assignment/{assignmentId:int}")]
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
