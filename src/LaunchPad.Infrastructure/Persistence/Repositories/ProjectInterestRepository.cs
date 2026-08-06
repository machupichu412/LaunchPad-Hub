using LaunchPad.Application.Matching;
using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class ProjectInterestRepository : IProjectInterestRepository
{
    private readonly LaunchPadDbContext _db;
    public ProjectInterestRepository(LaunchPadDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProjectInterest>> GetByCohortAsync(int cohortId, CancellationToken ct = default) =>
        await _db.ProjectInterests
            .Where(pi => pi.Candidate.CohortId == cohortId)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, byte>> GetRatingsForCandidateAsync(int candidateId, CancellationToken ct = default) =>
        await _db.ProjectInterests
            .Where(pi => pi.CandidateId == candidateId)
            .ToDictionaryAsync(pi => pi.ProjectId, pi => pi.Rating, ct);

    public async Task<ProjectInterest> UpsertAsync(int candidateId, int projectId, byte rating, CancellationToken ct = default)
    {
        var existing = await _db.ProjectInterests
            .FirstOrDefaultAsync(pi => pi.CandidateId == candidateId && pi.ProjectId == projectId, ct);

        if (existing is not null)
        {
            existing.Rating = rating;
            existing.UpdatedUtc = DateTime.UtcNow;
            return existing;
        }

        var interest = new ProjectInterest
        {
            CandidateId = candidateId,
            ProjectId = projectId,
            Rating = rating,
            UpdatedUtc = DateTime.UtcNow,
        };
        await _db.ProjectInterests.AddAsync(interest, ct);
        return interest;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
