using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
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

    public Task<int?> GetIdByUpnAsync(string upn, CancellationToken ct = default) =>
        _db.AppUsers
            .Where(u => u.Upn == upn)
            .Select(u => (int?)u.AppUserId)
            .FirstOrDefaultAsync(ct);

    public Task<AppUser?> GetByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default) =>
        _db.AppUsers.FirstOrDefaultAsync(u => u.EntraObjectId == entraObjectId, ct);

    public Task<AppUser?> GetByIdAsync(int appUserId, CancellationToken ct = default) =>
        _db.AppUsers.FirstOrDefaultAsync(u => u.AppUserId == appUserId, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
