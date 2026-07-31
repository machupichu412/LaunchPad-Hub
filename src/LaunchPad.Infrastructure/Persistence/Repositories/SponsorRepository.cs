using LaunchPad.Application.Sponsors;
using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class SponsorRepository : ISponsorRepository
{
    private readonly LaunchPadDbContext _db;
    public SponsorRepository(LaunchPadDbContext db) => _db = db;

    public Task<Sponsor?> GetByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default) =>
        _db.Sponsors
            .Include(s => s.AppUser)
            .FirstOrDefaultAsync(s => s.AppUser.EntraObjectId == entraObjectId, ct);
}
