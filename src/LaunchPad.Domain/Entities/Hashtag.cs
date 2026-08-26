namespace LaunchPad.Domain.Entities;

/// <summary>
/// A canonical, lowercase tag extracted from post bodies (see HashtagExtractor) — shared/reused
/// across posts, normalized here specifically so the feed can filter by tag with an indexed
/// lookup instead of scanning post bodies with LIKE. Display casing is never stored; the
/// frontend re-highlights hashtags from the original post Body text.
/// </summary>
public class Hashtag
{
    public int HashtagId { get; set; }
    public string Tag { get; set; } = string.Empty;

    public ICollection<CommunityPostHashtag> PostHashtags { get; set; } = new List<CommunityPostHashtag>();
}
