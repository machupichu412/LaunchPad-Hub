using FluentValidation;
using LaunchPad.Application.Common;
using LaunchPad.Application.Community;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LaunchPad.Api.Controllers;

/// <summary>
/// Program-wide social feed — any authenticated role can post/view, matching the
/// mockup's mixed-role feed. New scope, not part of the guide's data model.
///
/// Feed reads are cursor-paginated (see CommunityFeedCursor) rather than offset-paginated —
/// a naive Skip/Take gets slower as the offset grows, while a keyset cursor stays O(page size)
/// no matter how deep into the feed a client has scrolled. List queries never eager-load the
/// Reactions/Comments collections (see CommunityRepository.GetFeedPageAsync); like/comment
/// counts come from persisted counters, and "did I like this" is one batched query per page,
/// not one query per post.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommunityController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    private readonly ICommunityRepository _community;
    private readonly IAppUserRepository _appUsers;
    private readonly ICurrentUser _currentUser;
    private readonly ICommunityImageStorage _images;
    private readonly IValidator<CreateCommunityPostRequest> _postValidator;
    private readonly IValidator<CreateCommunityCommentRequest> _commentValidator;

    public CommunityController(
        ICommunityRepository community,
        IAppUserRepository appUsers,
        ICurrentUser currentUser,
        ICommunityImageStorage images,
        IValidator<CreateCommunityPostRequest> postValidator,
        IValidator<CreateCommunityCommentRequest> commentValidator)
    {
        _community = community;
        _appUsers = appUsers;
        _currentUser = currentUser;
        _images = images;
        _postValidator = postValidator;
        _commentValidator = commentValidator;
    }

    [HttpGet("posts")]
    public async Task<ActionResult<CommunityFeedPageDto>> GetPosts(
        [FromQuery] string? cursor, [FromQuery] int? pageSize, [FromQuery] string? hashtag, CancellationToken ct)
    {
        var myAppUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);

        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var canonicalHashtag = string.IsNullOrWhiteSpace(hashtag) ? null : hashtag.Trim().ToLowerInvariant();

        // A malformed/tampered cursor just restarts the feed from the top rather than
        // erroring — a client-side bug shouldn't be able to hard-break the feed.
        DateTime? cursorCreatedUtc = null;
        int? cursorPostId = null;
        if (CommunityFeedCursor.TryDecode(cursor, out var decodedCreatedUtc, out var decodedPostId))
        {
            cursorCreatedUtc = decodedCreatedUtc;
            cursorPostId = decodedPostId;
        }

        var (posts, hasMore) = await _community.GetFeedPageAsync(cursorCreatedUtc, cursorPostId, size, canonicalHashtag, ct);

        var likedPostIds = myAppUserId is int callerId
            ? await _community.GetLikedPostIdsAsync(callerId, posts.Select(p => p.CommunityPostId).ToArray(), ct)
            : new HashSet<int>();

        var items = posts.Select(p => ToDto(p, likedPostIds.Contains(p.CommunityPostId))).ToArray();
        var nextCursor = hasMore && posts.Count > 0
            ? CommunityFeedCursor.Encode(posts[^1].CreatedUtc, posts[^1].CommunityPostId)
            : null;

        return Ok(new CommunityFeedPageDto { Items = items, NextCursor = nextCursor });
    }

    [HttpGet("posts/{id:int}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommunityCommentDto>>> GetComments(int id, CancellationToken ct)
    {
        var comments = await _community.GetCommentsAsync(id, ct);
        return Ok(comments.Select(ToDto).ToArray());
    }

    [HttpPost("posts")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 8 * 1024 * 1024)]
    [EnableRateLimiting(RateLimitPolicies.CommunityWrite)]
    public async Task<ActionResult<CommunityPostDto>> CreatePost(
        [FromForm] string body, [FromForm] CommunityPostType postType, [FromForm] string? activeRole,
        [FromForm] IFormFile? image, CancellationToken ct)
    {
        var request = new CreateCommunityPostRequest
        {
            Body = body,
            PostType = postType,
            ActiveRole = activeRole,
            ImageContentType = image?.ContentType,
            ImageLength = image?.Length,
        };

        var validation = await _postValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(AddErrors(validation));

        var myAppUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (myAppUserId is null) return Forbid();

        var post = new CommunityPost
        {
            AuthorAppUserId = myAppUserId.Value,
            Body = request.Body,
            PostType = request.PostType,
            AuthorRoleLabel = ResolveAuthorRoleLabel(request.ActiveRole),
        };

        foreach (var tag in HashtagExtractor.Extract(request.Body))
        {
            var hashtag = await _community.GetOrCreateHashtagAsync(tag, ct);
            post.PostHashtags.Add(new CommunityPostHashtag { Post = post, Hashtag = hashtag });
        }

        await _community.AddPostAsync(post, ct);
        await _community.SaveChangesAsync(ct);

        // Uploaded after the post itself has an id — the storage key is namespaced by
        // CommunityPostId (see ICommunityImageStorage), so this can only happen post-save.
        if (image is not null)
        {
            await using var stream = image.OpenReadStream();
            post.ImageBlobPath = await _images.SaveAsync(post.CommunityPostId, stream, image.ContentType, ct);
            post.ImageContentType = image.ContentType;
            await _community.SaveChangesAsync(ct);
        }

        var created = await _community.GetPostAsync(post.CommunityPostId, ct);
        return Ok(ToDto(created!, hasLikedByMe: false));
    }

    [HttpPost("posts/{id:int}/comments")]
    [EnableRateLimiting(RateLimitPolicies.CommunityWrite)]
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
            AuthorRoleLabel = ResolveAuthorRoleLabel(request.ActiveRole),
        };

        await _community.AddCommentAsync(comment, ct);
        await _community.SaveChangesAsync(ct);

        var author = await _appUsers.GetByIdAsync(myAppUserId.Value, ct);
        return Ok(new CommunityCommentDto
        {
            CommunityCommentId = comment.CommunityCommentId,
            AuthorAppUserId = myAppUserId.Value,
            AuthorName = author?.DisplayName ?? User.Identity?.Name ?? "You",
            AuthorRoleLabel = comment.AuthorRoleLabel,
            AuthorTeamsLink = TeamsLinkFor(author?.Upn),
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

    /// <summary>Proxies a post's image content — mirrors AssignmentsController.GetDeliverableFile's
    /// shape. The client never talks to Blob Storage directly.</summary>
    [HttpGet("posts/{id:int}/image")]
    public async Task<IActionResult> GetPostImage(int id, CancellationToken ct)
    {
        var post = await _community.GetPostAsync(id, ct);
        if (post?.ImageBlobPath is not { } blobPath) return NotFound();

        var result = await _images.GetAsync(blobPath, ct);
        if (result is null) return NotFound();

        return File(result.Value.Content, result.Value.ContentType);
    }

    private static CommunityPostDto ToDto(CommunityPost post, bool hasLikedByMe) => new()
    {
        CommunityPostId = post.CommunityPostId,
        AuthorAppUserId = post.AuthorAppUserId,
        AuthorName = post.Author.DisplayName,
        AuthorRoleLabel = post.AuthorRoleLabel,
        AuthorTeamsLink = TeamsLinkFor(post.Author.Upn),
        Body = post.Body,
        PostType = post.PostType,
        CreatedUtc = post.CreatedUtc,
        HasImage = post.ImageBlobPath is not null,
        LikeCount = post.LikeCount,
        HasLikedByMe = hasLikedByMe,
        CommentCount = post.CommentCount,
    };

    private static CommunityCommentDto ToDto(CommunityComment comment) => new()
    {
        CommunityCommentId = comment.CommunityCommentId,
        AuthorAppUserId = comment.AuthorAppUserId,
        AuthorName = comment.Author.DisplayName,
        AuthorRoleLabel = comment.AuthorRoleLabel,
        AuthorTeamsLink = TeamsLinkFor(comment.Author.Upn),
        Body = comment.Body,
        CreatedUtc = comment.CreatedUtc,
    };

    private static string TeamsLinkFor(string? upn) => string.IsNullOrWhiteSpace(upn)
        ? string.Empty
        : $"https://teams.microsoft.com/l/chat/0/0?users={Uri.EscapeDataString(upn)}";

    /// <summary>The frontend's ActiveRoleContext lets a multi-role user (e.g. Candidate +
    /// ProgramOps) switch which perspective they're viewing as — the role label on a post/
    /// comment should reflect whichever role they were actively viewing as when they posted,
    /// not just the first role listed on their token. Only trusted once confirmed to be one
    /// of the roles actually present on the caller's own token (a client can't claim to post
    /// as a role it doesn't hold); falls back to the token's first role otherwise, matching
    /// the original behavior.</summary>
    private string? ResolveAuthorRoleLabel(string? requestedRole)
    {
        var role = requestedRole is not null && _currentUser.IsInRole(requestedRole)
            ? requestedRole
            : _currentUser.Roles.FirstOrDefault();

        return role?.Replace("LaunchPad.", string.Empty);
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
