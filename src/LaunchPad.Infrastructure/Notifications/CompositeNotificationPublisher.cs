using LaunchPad.Application.Common;
using LaunchPad.Application.Notifications;
using LaunchPad.Domain.Entities;

namespace LaunchPad.Infrastructure.Notifications;

/// <summary>
/// The INotificationPublisher every call site actually talks to. Splits each publish
/// into two independent halves: an in-app Notification row, written synchronously here
/// so the bell works even where Service Bus isn't provisioned (this local sandbox
/// included); and the existing best-effort async email path via
/// ServiceBusNotificationPublisher, unchanged. A ToUpn that doesn't resolve to any
/// AppUser (e.g. the config-only Notifications:ProgramOpsUpn) just skips the in-app
/// half — there's no recipient row to attach it to.
/// </summary>
public sealed class CompositeNotificationPublisher : INotificationPublisher
{
    private readonly INotificationRepository _notifications;
    private readonly IAppUserRepository _appUsers;
    private readonly ServiceBusNotificationPublisher _emailPublisher;

    public CompositeNotificationPublisher(
        INotificationRepository notifications,
        IAppUserRepository appUsers,
        ServiceBusNotificationPublisher emailPublisher)
    {
        _notifications = notifications;
        _appUsers = appUsers;
        _emailPublisher = emailPublisher;
    }

    public async Task PublishAsync(NotificationMessage message, CancellationToken ct = default)
    {
        var recipientAppUserId = await _appUsers.GetIdByUpnAsync(message.ToUpn, ct);
        if (recipientAppUserId.HasValue)
        {
            await _notifications.AddAsync(new Notification
            {
                RecipientAppUserId = recipientAppUserId.Value,
                Subject = message.Subject,
                Body = message.Body,
            }, ct);
            await _notifications.SaveChangesAsync(ct);
        }

        await _emailPublisher.PublishAsync(message, ct);
    }
}
