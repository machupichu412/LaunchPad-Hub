using LaunchPad.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class AppUserRepository : IAppUserRepository
{
    private readonly LaunchPadDbContext _db;
    public AppUserRepository(LaunchPadDbContext db) => _db = db;

    public Task<int?> GetIdByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default) =>
        _db.AppUsers
            .Where(u => u.EntraObjectId == entraObjectId)
            .Select(u => (int?)u.AppUserId)
            .FirstOrDefaultAsync(ct);
}
