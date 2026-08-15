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
            .Where(r => r.ReviewType == ReviewType.SponsorOnCandidate
                && r.Checkpoint == Checkpoint.Final
                && r.Assignment.CandidateId == candidateId)
            .OrderByDescending(r => r.SubmittedUtc)
            .Select(r => r.RecommendConversion)
            .FirstOrDefaultAsync(ct);
}
