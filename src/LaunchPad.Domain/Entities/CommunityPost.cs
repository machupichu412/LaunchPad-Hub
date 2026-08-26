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

    /// <summary>Opaque storage key for the post's single optional image, resolved via
    /// ICommunityImageStorage — never a public URL, always proxied through the API.</summary>
    public string? ImageBlobPath { get; set; }
    public string? ImageContentType { get; set; }

    // Denormalized counters, maintained transactionally by CommunityRepository
    // (ExecuteUpdateAsync) alongside the reaction/comment write itself — the feed's
    // list query must never load the full Reactions/Comments collections just to
    // count them, since that stops scaling once a post accumulates real engagement.
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }

    public AppUser Author { get; set; } = null!;
    public ICollection<CommunityComment> Comments { get; set; } = new List<CommunityComment>();
    public ICollection<CommunityPostReaction> Reactions { get; set; } = new List<CommunityPostReaction>();
    public ICollection<CommunityPostHashtag> PostHashtags { get; set; } = new List<CommunityPostHashtag>();
}
