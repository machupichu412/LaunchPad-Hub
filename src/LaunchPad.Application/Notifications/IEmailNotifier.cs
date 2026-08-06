namespace LaunchPad.Application.Notifications;

/// <summary>
/// Sends one email. The Functions NotificationFunction is the only caller — the API never
/// calls this directly (see INotificationPublisher). Implementations must no-op (log and
/// return) when Graph isn't configured, but propagate real Graph failures so Service Bus
/// retries/dead-letters them rather than silently losing the notification.
/// </summary>
public interface IEmailNotifier
{
    Task SendAsync(string toUpn, string subject, string body, CancellationToken ct = default);
}
