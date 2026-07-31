using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

/// <summary>
/// Program-wide social feed — new scope, not part of the guide's data model.
/// Any authenticated role can post/view; not cohort- or role-scoped.
/// </summary>
public class CommunityPost
{
    public int CommunityPostId { get; set; }
    public int AuthorAppUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public CommunityPostType PostType { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    // Roles live only in the Entra token, never in the DB — this is a point-in-time
    // label captured from the author's own roles when they posted, purely for the
    // feed's role badge. Not a persistent per-user role cache (which would risk
    // going stale and duplicating Entra as a second source of truth).
    public string? AuthorRoleLabel { get; set; }

    public AppUser Author { get; set; } = null!;
    public ICollection<CommunityComment> Comments { get; set; } = new List<CommunityComment>();
    public ICollection<CommunityPostReaction> Reactions { get; set; } = new List<CommunityPostReaction>();
}
