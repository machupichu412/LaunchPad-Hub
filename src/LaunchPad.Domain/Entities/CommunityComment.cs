namespace LaunchPad.Domain.Entities;

public class CommunityComment
{
    public int CommunityCommentId { get; set; }
    public int CommunityPostId { get; set; }
    public int AuthorAppUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    // Same point-in-time-label reasoning as CommunityPost.AuthorRoleLabel — the role the
    // author was actively viewing as when they commented, not a live FK to Entra.
    public string? AuthorRoleLabel { get; set; }

    public CommunityPost Post { get; set; } = null!;
    public AppUser Author { get; set; } = null!;
}
