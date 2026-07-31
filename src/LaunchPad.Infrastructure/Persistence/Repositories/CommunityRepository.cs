using LaunchPad.Application.Community;
using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class CommunityRepository : ICommunityRepository
{
    private readonly LaunchPadDbContext _db;
    public CommunityRepository(LaunchPadDbContext db) => _db = db;

    public async Task<IReadOnlyList<CommunityPost>> GetRecentPostsAsync(int take, CancellationToken ct = default) =>
        await _db.CommunityPosts
            .Include(p => p.Author)
            .Include(p => p.Comments).ThenInclude(c => c.Author)
            .Include(p => p.Reactions)
            .OrderByDescending(p => p.CreatedUtc)
            .Take(take)
            .ToListAsync(ct);

    public Task<CommunityPost?> GetPostAsync(int postId, CancellationToken ct = default) =>
        _db.CommunityPosts
            .Include(p => p.Reactions)
            .FirstOrDefaultAsync(p => p.CommunityPostId == postId, ct);

    public async Task<CommunityPost> AddPostAsync(CommunityPost post, CancellationToken ct = default)
    {
        await _db.CommunityPosts.AddAsync(post, ct);
        return post;
    }

    public async Task<CommunityComment> AddCommentAsync(CommunityComment comment, CancellationToken ct = default)
    {
        await _db.CommunityComments.AddAsync(comment, ct);
        return comment;
    }

    public async Task<bool> ToggleReactionAsync(int postId, int appUserId, CancellationToken ct = default)
    {
        var existing = await _db.CommunityPostReactions
            .FirstOrDefaultAsync(r => r.CommunityPostId == postId && r.AppUserId == appUserId, ct);

        if (existing is not null)
        {
            _db.CommunityPostReactions.Remove(existing);
            await _db.SaveChangesAsync(ct);
            return false;
        }

        await _db.CommunityPostReactions.AddAsync(new CommunityPostReaction { CommunityPostId = postId, AppUserId = appUserId }, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<int> CountPostsSinceAsync(DateTime sinceUtc, CancellationToken ct = default) =>
        _db.CommunityPosts.CountAsync(p => p.CreatedUtc >= sinceUtc, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
