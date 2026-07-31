namespace LaunchPad.Domain.Entities;

public class CommunityComment
{
    public int CommunityCommentId { get; set; }
    public int CommunityPostId { get; set; }
    public int AuthorAppUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public CommunityPost Post { get; set; } = null!;
    public AppUser Author { get; set; } = null!;
}
