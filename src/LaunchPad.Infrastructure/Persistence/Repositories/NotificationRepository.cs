using LaunchPad.Application.Notifications;
using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly LaunchPadDbContext _db;
    public NotificationRepository(LaunchPadDbContext db) => _db = db;

    public async Task AddAsync(Notification notification, CancellationToken ct = default) =>
        await _db.Notifications.AddAsync(notification, ct);

    public async Task<IReadOnlyList<Notification>> GetRecentForUserAsync(int recipientAppUserId, int take, CancellationToken ct = default) =>
        await _db.Notifications
            .Where(n => n.RecipientAppUserId == recipientAppUserId)
            .OrderByDescending(n => n.CreatedUtc)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> GetUnreadCountAsync(int recipientAppUserId, CancellationToken ct = default) =>
        _db.Notifications.CountAsync(n => n.RecipientAppUserId == recipientAppUserId && !n.IsRead, ct);

    public Task<Notification?> GetAsync(int notificationId, CancellationToken ct = default) =>
        _db.Notifications.FirstOrDefaultAsync(n => n.NotificationId == notificationId, ct);

    public async Task MarkAllReadAsync(int recipientAppUserId, CancellationToken ct = default)
    {
        var unread = await _db.Notifications
            .Where(n => n.RecipientAppUserId == recipientAppUserId && !n.IsRead)
            .ToListAsync(ct);

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
