using FluentValidation;
using LaunchPad.Application.Common;
using LaunchPad.Application.Community;
using LaunchPad.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

/// <summary>
/// Program-wide social feed — any authenticated role can post/view, matching the
/// mockup's mixed-role feed. New scope, not part of the guide's data model.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommunityController : ControllerBase
{
    private readonly ICommunityRepository _community;
    private readonly IAppUserRepository _appUsers;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateCommunityPostRequest> _postValidator;
    private readonly IValidator<CreateCommunityCommentRequest> _commentValidator;

    public CommunityController(
        ICommunityRepository community,
        IAppUserRepository appUsers,
        ICurrentUser currentUser,
        IValidator<CreateCommunityPostRequest> postValidator,
        IValidator<CreateCommunityCommentRequest> commentValidator)
    {
        _community = community;
        _appUsers = appUsers;
        _currentUser = currentUser;
        _postValidator = postValidator;
        _commentValidator = commentValidator;
    }

    [HttpGet("posts")]
    public async Task<ActionResult<IReadOnlyList<CommunityPostDto>>> GetPosts(CancellationToken ct)
    {
        var myAppUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        var posts = await _community.GetRecentPostsAsync(take: 50, ct);

        return Ok(posts.Select(p => ToDto(p, myAppUserId)).ToArray());
    }

    [HttpPost("posts")]
    public async Task<ActionResult<CommunityPostDto>> CreatePost(CreateCommunityPostRequest request, CancellationToken ct)
    {
        var validation = await _postValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(AddErrors(validation));

        var myAppUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (myAppUserId is null) return Forbid();

        var post = new CommunityPost
        {
            AuthorAppUserId = myAppUserId.Value,
            Body = request.Body,
            PostType = request.PostType,
            AuthorRoleLabel = _currentUser.Roles.FirstOrDefault()?.Replace("LaunchPad.", string.Empty),
        };

        await _community.AddPostAsync(post, ct);
        await _community.SaveChangesAsync(ct);

        var created = await _community.GetPostAsync(post.CommunityPostId, ct);
        return Ok(ToDto(created!, myAppUserId));
    }

    [HttpPost("posts/{id:int}/comments")]
    public async Task<ActionResult<CommunityCommentDto>> AddComment(int id, CreateCommunityCommentRequest request, CancellationToken ct)
    {
        var validation = await _commentValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(AddErrors(validation));

        var post = await _community.GetPostAsync(id, ct);
        if (post is null) return NotFound();

        var myAppUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (myAppUserId is null) return Forbid();

        var comment = new CommunityComment
        {
            CommunityPostId = id,
            AuthorAppUserId = myAppUserId.Value,
            Body = request.Body,
        };

        await _community.AddCommentAsync(comment, ct);
        await _community.SaveChangesAsync(ct);

        return Ok(new CommunityCommentDto
        {
            CommunityCommentId = comment.CommunityCommentId,
            AuthorName = User.Identity?.Name ?? "You",
            Body = comment.Body,
            CreatedUtc = comment.CreatedUtc,
        });
    }

    [HttpPost("posts/{id:int}/reactions")]
    public async Task<ActionResult<object>> ToggleReaction(int id, CancellationToken ct)
    {
        var post = await _community.GetPostAsync(id, ct);
        if (post is null) return NotFound();

        var myAppUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (myAppUserId is null) return Forbid();

        var liked = await _community.ToggleReactionAsync(id, myAppUserId.Value, ct);
        return Ok(new { liked });
    }

    private static CommunityPostDto ToDto(CommunityPost post, int? myAppUserId) => new()
    {
        CommunityPostId = post.CommunityPostId,
        AuthorName = post.Author.DisplayName,
        AuthorRoleLabel = post.AuthorRoleLabel,
        Body = post.Body,
        PostType = post.PostType,
        CreatedUtc = post.CreatedUtc,
        LikeCount = post.Reactions.Count,
        HasLikedByMe = myAppUserId.HasValue && post.Reactions.Any(r => r.AppUserId == myAppUserId.Value),
        Comments = post.Comments
            .OrderBy(c => c.CreatedUtc)
            .Select(c => new CommunityCommentDto
            {
                CommunityCommentId = c.CommunityCommentId,
                AuthorName = c.Author.DisplayName,
                Body = c.Body,
                CreatedUtc = c.CreatedUtc,
            })
            .ToArray(),
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
