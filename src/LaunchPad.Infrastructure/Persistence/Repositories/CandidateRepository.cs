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

    public Task<CandidateRisk?> GetRiskAsync(int candidateId, CancellationToken ct = default) =>
        _db.CandidateRisks.FirstOrDefaultAsync(r => r.CandidateId == candidateId, ct);

    public async Task<IReadOnlyList<Candidate>> GetByCohortAsync(int cohortId, CancellationToken ct = default) =>
        await _db.Candidates
            .Include(c => c.AppUser)
            .Include(c => c.Skills).ThenInclude(cs => cs.Skill)
            .Where(c => c.CohortId == cohortId)
            .ToListAsync(ct);
}
