using System.Text.Json;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence;

public sealed class AuditLog : IAuditLog
{
    private readonly LaunchPadDbContext _db;
    private readonly IAppUserRepository _appUsers;

    public AuditLog(LaunchPadDbContext db, IAppUserRepository appUsers)
    {
        _db = db;
        _appUsers = appUsers;
    }

    public async Task RecordAsync(
        Guid actorEntraObjectId,
        string entityName,
        string entityId,
        string action,
        string? reason = null,
        object? data = null,
        CancellationToken ct = default)
    {
        // Guid.Empty means "system-triggered, no human actor" (nightly jobs) — the
        // same fallback CurrentUser uses for an unauthenticated request's EntraObjectId,
        // and mirrors the ?? 0 pattern controllers already use for SubmittedBy/etc.
        var actorAppUserId = actorEntraObjectId == Guid.Empty
            ? (int?)null
            : await _appUsers.GetIdByEntraObjectIdAsync(actorEntraObjectId, ct);

        var auditEvent = new AuditEvent
        {
            ActorAppUserId = actorAppUserId ?? 0,
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            Reason = reason,
            DataJson = data is null ? null : JsonSerializer.Serialize(data),
        };

        await _db.AuditEvents.AddAsync(auditEvent, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlySet<int>> GetEntityIdsWithLatestActionAsync(
        string entityName,
        string action,
        CancellationToken ct = default)
    {
        // Grouped/ordered in memory rather than translated to SQL — audit volume per
        // entity type is small at this app's scale (~2,000 users), and the InMemory
        // provider (tests, local demo) can't reliably translate the GroupBy+OrderBy
        // shape this needs anyway.
        var events = await _db.AuditEvents
            .Where(e => e.EntityName == entityName)
            .ToListAsync(ct);

        return events
            .GroupBy(e => e.EntityId)
            .Where(g => g.OrderByDescending(e => e.OccurredUtc).First().Action == action)
            .Select(g => int.Parse(g.Key))
            .ToHashSet();
    }
}
