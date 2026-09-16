using LaunchPad.Application.Candidates;
using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class CandidateRepository : ICandidateRepository
{
    private readonly LaunchPadDbContext _db;
    public CandidateRepository(LaunchPadDbContext db) => _db = db;

    public Task<Candidate?> GetWithSkillsAsync(int candidateId, CancellationToken ct = default) =>
        _db.Candidates
            .Include(c => c.AppUser)
            .Include(c => c.Skills).ThenInclude(cs => cs.Skill)
            .FirstOrDefaultAsync(c => c.CandidateId == candidateId, ct);

    public Task<Candidate?> GetByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default) =>
        _db.Candidates
            .Include(c => c.AppUser)
            .Include(c => c.Skills).ThenInclude(cs => cs.Skill)
            .Where(c => c.AppUser.EntraObjectId == entraObjectId)
            .OrderByDescending(c => c.CandidateId) // most recent cohort if enrolled more than once
            .FirstOrDefaultAsync(ct);

    public Task<CandidateRisk?> GetRiskAsync(int candidateId, CancellationToken ct = default) =>
        _db.CandidateRisks.AsNoTracking().FirstOrDefaultAsync(r => r.CandidateId == candidateId, ct);

    public async Task<IReadOnlyDictionary<int, CandidateRisk>> GetRisksAsync(IReadOnlyList<int> candidateIds, CancellationToken ct = default)
    {
        if (candidateIds.Count == 0) return new Dictionary<int, CandidateRisk>();

        return await _db.CandidateRisks
            .AsNoTracking()
            .Where(r => candidateIds.Contains(r.CandidateId))
            .ToDictionaryAsync(r => r.CandidateId, ct);
    }

    public async Task<IReadOnlyList<Candidate>> GetByCohortAsync(int cohortId, CancellationToken ct = default) =>
        await _db.Candidates
            .AsNoTracking()
            .Include(c => c.AppUser)
            .Include(c => c.Skills).ThenInclude(cs => cs.Skill)
            .Where(c => c.CohortId == cohortId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Candidate>> GetByCohortsAsync(IReadOnlyList<int> cohortIds, CancellationToken ct = default) =>
        await _db.Candidates
            .AsNoTracking()
            .Include(c => c.AppUser)
            .Include(c => c.Skills).ThenInclude(cs => cs.Skill)
            .Where(c => cohortIds.Count == 0 || cohortIds.Contains(c.CohortId))
            .ToListAsync(ct);

    public async Task<Candidate> AddAsync(Candidate candidate, CancellationToken ct = default)
    {
        await _db.Candidates.AddAsync(candidate, ct);
        return candidate;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
