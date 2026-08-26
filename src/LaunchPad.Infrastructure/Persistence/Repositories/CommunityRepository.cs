using LaunchPad.Application.Community;
using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class CommunityRepository : ICommunityRepository
{
    private readonly LaunchPadDbContext _db;
    public CommunityRepository(LaunchPadDbContext db) => _db = db;

    public async Task<(IReadOnlyList<CommunityPost> Posts, bool HasMore)> GetFeedPageAsync(
        DateTime? cursorCreatedUtc, int? cursorPostId, int pageSize, string? hashtag, CancellationToken ct = default)
    {
        // Author only — never Reactions/Comments here. Counts come from the persisted
        // LikeCount/CommentCount columns; "did I like this" comes from a separate batched
        // query (GetLikedPostIdsAsync); comments themselves are fetched lazily on expand
        // (GetCommentsAsync). Loading either collection for every post on every feed page
        // is exactly the scalability problem this rewrite exists to fix.
        var query = _db.CommunityPosts.Include(p => p.Author).AsQueryable();

        if (hashtag is not null)
        {
            query = query.Where(p => p.PostHashtags.Any(ph => ph.Hashtag.Tag == hashtag));
        }

        if (cursorCreatedUtc is not null && cursorPostId is not null)
        {
            var cCreatedUtc = cursorCreatedUtc.Value;
            var cPostId = cursorPostId.Value;
            query = query.Where(p =>
                p.CreatedUtc < cCreatedUtc || (p.CreatedUtc == cCreatedUtc && p.CommunityPostId < cPostId));
        }

        // Fetch one extra row so HasMore is known without a separate COUNT query.
        var page = await query
            .OrderByDescending(p => p.CreatedUtc).ThenByDescending(p => p.CommunityPostId)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        var hasMore = page.Count > pageSize;
        return (hasMore ? page.Take(pageSize).ToList() : page, hasMore);
    }

    public Task<CommunityPost?> GetPostAsync(int postId, CancellationToken ct = default) =>
        _db.CommunityPosts.Include(p => p.Author).FirstOrDefaultAsync(p => p.CommunityPostId == postId, ct);

    public async Task<CommunityPost> AddPostAsync(CommunityPost post, CancellationToken ct = default)
    {
        await _db.CommunityPosts.AddAsync(post, ct);
        return post;
    }

    public async Task<CommunityComment> AddCommentAsync(CommunityComment comment, CancellationToken ct = default)
    {
        await _db.CommunityComments.AddAsync(comment, ct);

        // Loaded into the same tracked context and incremented in memory rather than an
        // ExecuteUpdateAsync bulk statement — EF Core's InMemory provider (used by the test
        // suite) doesn't support ExecuteUpdateAsync at all. This also means the counter bump
        // rides in the SAME SaveChangesAsync transaction as the comment insert itself (the
        // caller calls SaveChangesAsync after this), which is stronger, not weaker.
        var post = await _db.CommunityPosts.FirstOrDefaultAsync(p => p.CommunityPostId == comment.CommunityPostId, ct);
        if (post is not null) post.CommentCount++;

        return comment;
    }

    public async Task<IReadOnlyList<CommunityComment>> GetCommentsAsync(int postId, CancellationToken ct = default) =>
        await _db.CommunityComments.Include(c => c.Author)
            .Where(c => c.CommunityPostId == postId)
            .OrderBy(c => c.CreatedUtc)
            .ToListAsync(ct);

    public async Task<bool> ToggleReactionAsync(int postId, int appUserId, CancellationToken ct = default)
    {
        // Loaded into the tracked context and incremented in memory, same reasoning as
        // AddCommentAsync above — EF Core's InMemory provider (used by the test suite)
        // doesn't support ExecuteUpdateAsync. A theoretical lost-update race between two
        // concurrent likes on the same post is an acceptable tradeoff at this app's actual
        // scale (<200 concurrent users on an internal social feed, not a financial ledger).
        var post = await _db.CommunityPosts.FirstOrDefaultAsync(p => p.CommunityPostId == postId, ct);

        var existing = await _db.CommunityPostReactions
            .FirstOrDefaultAsync(r => r.CommunityPostId == postId && r.AppUserId == appUserId, ct);

        if (existing is not null)
        {
            _db.CommunityPostReactions.Remove(existing);
            if (post is not null) post.LikeCount = Math.Max(0, post.LikeCount - 1);
            await _db.SaveChangesAsync(ct);
            return false;
        }

        await _db.CommunityPostReactions.AddAsync(new CommunityPostReaction { CommunityPostId = postId, AppUserId = appUserId }, ct);
        if (post is not null) post.LikeCount++;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<HashSet<int>> GetLikedPostIdsAsync(int appUserId, IReadOnlyCollection<int> postIds, CancellationToken ct = default) =>
        _db.CommunityPostReactions
            .Where(r => r.AppUserId == appUserId && postIds.Contains(r.CommunityPostId))
            .Select(r => r.CommunityPostId)
            .ToHashSetAsync(ct);

    public async Task<Hashtag> GetOrCreateHashtagAsync(string canonicalTag, CancellationToken ct = default)
    {
        var existing = await _db.Hashtags.FirstOrDefaultAsync(h => h.Tag == canonicalTag, ct);
        if (existing is not null) return existing;

        var created = new Hashtag { Tag = canonicalTag };
        _db.Hashtags.Add(created);
        try
        {
            await _db.SaveChangesAsync(ct);
            return created;
        }
        catch (DbUpdateException)
        {
            // Another request created the same tag between our lookup and insert — the
            // unique index on Hashtag.Tag is the real race guard. Detach the failed insert
            // and hand back whoever won the race instead of surfacing a 500.
            _db.Entry(created).State = EntityState.Detached;
            var raceWinner = await _db.Hashtags.FirstOrDefaultAsync(h => h.Tag == canonicalTag, ct);
            if (raceWinner is not null) return raceWinner;
            throw;
        }
    }

    public Task<int> CountPostsSinceAsync(DateTime sinceUtc, CancellationToken ct = default) =>
        _db.CommunityPosts.CountAsync(p => p.CreatedUtc >= sinceUtc, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
