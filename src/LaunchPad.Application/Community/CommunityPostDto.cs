using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Community;

public class CommunityPostDto
{
    public int CommunityPostId { get; set; }
    public int AuthorAppUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorRoleLabel { get; set; }
    public string AuthorTeamsLink { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public CommunityPostType PostType { get; set; }
    public DateTime CreatedUtc { get; set; }
    public bool HasImage { get; set; }
    public int LikeCount { get; set; }
    public bool HasLikedByMe { get; set; }
    public int CommentCount { get; set; }
}

/// <summary>One page of the cursor-paginated feed — see CommunityFeedCursor. NextCursor is
/// null when this page was the last one.</summary>
public class CommunityFeedPageDto
{
    public IReadOnlyList<CommunityPostDto> Items { get; set; } = Array.Empty<CommunityPostDto>();
    public string? NextCursor { get; set; }
}

public class CommunityCommentDto
{
    public int CommunityCommentId { get; set; }
    public int AuthorAppUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorRoleLabel { get; set; }
    public string AuthorTeamsLink { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

/// <summary>The metadata half of a multipart post — deliberately framework-agnostic (no
/// IFormFile/Stream field), same reasoning as SubmitDeliverableRequest: LaunchPad.Application
/// has no ASP.NET Core hosting reference, so the controller extracts these primitives from
/// the bound IFormFile and validates this shape; the file stream itself goes straight from
/// the controller to ICommunityImageStorage.SaveAsync without round-tripping through this DTO.</summary>
public class CreateCommunityPostRequest
{
    public string Body { get; set; } = string.Empty;
    public CommunityPostType PostType { get; set; }
    public string? ImageContentType { get; set; }
    public long? ImageLength { get; set; }

    /// <summary>The role the caller is currently viewing as (see the frontend's
    /// ActiveRoleContext) — e.g. "LaunchPad.ProgramOps". Only trusted by the controller once
    /// it's confirmed to be one of the roles actually present on the caller's own token
    /// (ICurrentUser.IsInRole); a client can't claim to post as a role it doesn't hold.</summary>
    public string? ActiveRole { get; set; }
}

public class CreateCommunityCommentRequest
{
    public string Body { get; set; } = string.Empty;

    /// <summary>Same trust boundary as CreateCommunityPostRequest.ActiveRole.</summary>
    public string? ActiveRole { get; set; }
}
