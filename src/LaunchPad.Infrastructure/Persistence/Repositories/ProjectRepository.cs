using LaunchPad.Application.Projects;
using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly LaunchPadDbContext _db;
    public ProjectRepository(LaunchPadDbContext db) => _db = db;

    public Task<Project?> GetWithSponsorAsync(int projectId, CancellationToken ct = default) =>
        _db.Projects
            .Include(p => p.Sponsor).ThenInclude(s => s.AppUser)
            .FirstOrDefaultAsync(p => p.ProjectId == projectId, ct);

    public async Task<IReadOnlyList<Project>> GetByCohortAsync(int cohortId, CancellationToken ct = default) =>
        await _db.Projects.Where(p => p.CohortId == cohortId).ToListAsync(ct);

    public async Task<IReadOnlyList<Project>> GetBySponsorAsync(int sponsorId, CancellationToken ct = default) =>
        await _db.Projects.Where(p => p.SponsorId == sponsorId).ToListAsync(ct);
}
