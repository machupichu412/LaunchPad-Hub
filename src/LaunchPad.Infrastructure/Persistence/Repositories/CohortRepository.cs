using LaunchPad.Application.Cohorts;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class CohortRepository : ICohortRepository
{
    private readonly LaunchPadDbContext _db;
    public CohortRepository(LaunchPadDbContext db) => _db = db;

    public async Task<IReadOnlyList<CohortSummary>> GetAllWithCountsAsync(CancellationToken ct = default) =>
        await _db.Cohorts
            .Include(c => c.Program)
            .OrderByDescending(c => c.StartDate)
            .Select(c => new CohortSummary(c, c.Candidates.Count, c.Projects.Count))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Cohort>> GetActiveAsync(CancellationToken ct = default) =>
        await _db.Cohorts.Where(c => c.Status == CohortStatus.Active).ToListAsync(ct);

    public Task<int> GetDefaultProgramIdAsync(CancellationToken ct = default) =>
        _db.Programs.OrderBy(p => p.ProgramId).Select(p => p.ProgramId).FirstAsync(ct);

    public async Task<Cohort> AddAsync(Cohort cohort, CancellationToken ct = default)
    {
        await _db.Cohorts.AddAsync(cohort, ct);
        return cohort;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
