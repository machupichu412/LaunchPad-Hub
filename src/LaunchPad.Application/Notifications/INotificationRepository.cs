using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Notifications;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);

    /// <summary>Newest first, capped at <paramref name="take"/> — the bell's list, not a full history.</summary>
    Task<IReadOnlyList<Notification>> GetRecentForUserAsync(int recipientAppUserId, int take, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(int recipientAppUserId, CancellationToken ct = default);

    Task<Notification?> GetAsync(int notificationId, CancellationToken ct = default);

    Task MarkAllReadAsync(int recipientAppUserId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
