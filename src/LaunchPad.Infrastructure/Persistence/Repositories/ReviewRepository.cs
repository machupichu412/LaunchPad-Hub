using LaunchPad.Application.Reviews;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class ReviewRepository : IReviewRepository
{
    private readonly LaunchPadDbContext _db;
    public ReviewRepository(LaunchPadDbContext db) => _db = db;

    public async Task<Review> AddAsync(Review review, CancellationToken ct = default)
    {
        await _db.Reviews.AddAsync(review, ct);
        return review;
    }

    public async Task<IReadOnlyList<Review>> GetByAssignmentAsync(int assignmentId, CancellationToken ct = default) =>
        await _db.Reviews.Where(r => r.AssignmentId == assignmentId).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<bool?> GetLatestFinalRecommendConversionAsync(int candidateId, CancellationToken ct = default) =>
        await _db.Reviews
            .AsNoTracking()
            .Where(r => r.ReviewType == ReviewType.SponsorOnCandidate
                && r.Checkpoint == Checkpoint.Final
                && r.Assignment.CandidateId == candidateId)
            .OrderByDescending(r => r.SubmittedUtc)
            .Select(r => r.RecommendConversion)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<int, bool?>> GetLatestFinalRecommendConversionsAsync(
        IReadOnlyList<int> candidateIds, CancellationToken ct = default)
    {
        if (candidateIds.Count == 0) return new Dictionary<int, bool?>();

        // Projects the three columns needed rather than grouping in SQL: "latest row per
        // group" has no clean EF translation, and the row count here is bounded by Final
        // reviews for one cohort — small, and one round trip instead of one per candidate.
        var rows = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.ReviewType == ReviewType.SponsorOnCandidate
                && r.Checkpoint == Checkpoint.Final
                && candidateIds.Contains(r.Assignment.CandidateId))
            .Select(r => new { r.Assignment.CandidateId, r.SubmittedUtc, r.RecommendConversion })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.CandidateId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.SubmittedUtc).First().RecommendConversion);
    }
}
