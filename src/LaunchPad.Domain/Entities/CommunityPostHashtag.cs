namespace LaunchPad.Domain.Entities;

/// <summary>
/// Composite key (CommunityPostId, HashtagId) — same shape as CommunityPostReaction.
/// </summary>
public class CommunityPostHashtag
{
    public int CommunityPostId { get; set; }
    public int HashtagId { get; set; }

    public CommunityPost Post { get; set; } = null!;
    public Hashtag Hashtag { get; set; } = null!;
}
