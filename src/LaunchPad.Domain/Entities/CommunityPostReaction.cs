namespace LaunchPad.Domain.Entities;

/// <summary>
/// Composite key (CommunityPostId, AppUserId) — real per-user like tracking so a
/// double-like is structurally impossible and "did I like this" is a plain lookup,
/// not a derived guess from a counter.
/// </summary>
public class CommunityPostReaction
{
    public int CommunityPostId { get; set; }
    public int AppUserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public CommunityPost Post { get; set; } = null!;
    public AppUser AppUser { get; set; } = null!;
}
