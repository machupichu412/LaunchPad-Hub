using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Community;

public interface ICommunityRepository
{
    Task<IReadOnlyList<CommunityPost>> GetRecentPostsAsync(int take, CancellationToken ct = default);
    Task<CommunityPost?> GetPostAsync(int postId, CancellationToken ct = default);
    Task<CommunityPost> AddPostAsync(CommunityPost post, CancellationToken ct = default);
    Task<CommunityComment> AddCommentAsync(CommunityComment comment, CancellationToken ct = default);

    /// <summary>Adds the caller's reaction if absent, removes it if present. Returns true if now liked.</summary>
    Task<bool> ToggleReactionAsync(int postId, int appUserId, CancellationToken ct = default);

    Task<int> CountPostsSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
