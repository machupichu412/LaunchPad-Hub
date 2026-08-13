namespace LaunchPad.Domain.Entities;

/// <summary>
/// The in-app half of the notification pipeline — durable and always-on, unlike
/// ServiceBusNotificationPublisher's best-effort async email path, which silently
/// no-ops wherever Service Bus isn't provisioned (see CompositeNotificationPublisher).
/// </summary>
public class Notification
{
    public int NotificationId { get; set; }
    public int RecipientAppUserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public AppUser RecipientAppUser { get; set; } = null!;
}
