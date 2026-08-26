using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Community;

public interface ICommunityRepository
{
    /// <summary>Keyset-paginated feed page, newest first, ordered by (CreatedUtc, CommunityPostId)
    /// so ties don't skip/duplicate rows across pages. cursorCreatedUtc/cursorPostId are both
    /// null for the first page. hashtag, when set, is the canonical lowercase tag to filter by.
    /// Never eager-loads Reactions/Comments — see GetLikedPostIdsAsync/GetCommentsAsync for
    /// the batched, on-demand equivalents.</summary>
    Task<(IReadOnlyList<CommunityPost> Posts, bool HasMore)> GetFeedPageAsync(
        DateTime? cursorCreatedUtc, int? cursorPostId, int pageSize, string? hashtag, CancellationToken ct = default);

    Task<CommunityPost?> GetPostAsync(int postId, CancellationToken ct = default);
    Task<CommunityPost> AddPostAsync(CommunityPost post, CancellationToken ct = default);

    /// <summary>Increments CommunityPost.CommentCount atomically alongside the insert.</summary>
    Task<CommunityComment> AddCommentAsync(CommunityComment comment, CancellationToken ct = default);

    Task<IReadOnlyList<CommunityComment>> GetCommentsAsync(int postId, CancellationToken ct = default);

    /// <summary>Adds the caller's reaction if absent, removes it if present, and adjusts
    /// CommunityPost.LikeCount atomically. Returns true if now liked.</summary>
    Task<bool> ToggleReactionAsync(int postId, int appUserId, CancellationToken ct = default);

    /// <summary>One batched lookup for "did I like this" across a whole feed page — avoids an
    /// N+1 query per post.</summary>
    Task<HashSet<int>> GetLikedPostIdsAsync(int appUserId, IReadOnlyCollection<int> postIds, CancellationToken ct = default);

    /// <summary>Looks up an existing Hashtag by its canonical (lowercase) form, creating one if
    /// absent. Racy under concurrent first-use of a brand-new tag — see the implementation's
    /// handling of the unique-index violation this can produce.</summary>
    Task<Hashtag> GetOrCreateHashtagAsync(string canonicalTag, CancellationToken ct = default);

    /// <summary>Used by CandidatesController.GetMyDashboard's "posts this week" tile.</summary>
    Task<int> CountPostsSinceAsync(DateTime sinceUtc, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
