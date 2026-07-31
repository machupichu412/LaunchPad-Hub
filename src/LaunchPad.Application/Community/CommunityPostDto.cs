using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Community;

public class CommunityPostDto
{
    public int CommunityPostId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorRoleLabel { get; set; }
    public string Body { get; set; } = string.Empty;
    public CommunityPostType PostType { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int LikeCount { get; set; }
    public bool HasLikedByMe { get; set; }
    public IReadOnlyList<CommunityCommentDto> Comments { get; set; } = Array.Empty<CommunityCommentDto>();
}

public class CommunityCommentDto
{
    public int CommunityCommentId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

public class CreateCommunityPostRequest
{
    public string Body { get; set; } = string.Empty;
    public CommunityPostType PostType { get; set; }
}

public class CreateCommunityCommentRequest
{
    public string Body { get; set; } = string.Empty;
}
