namespace LaunchPad.Application.Notifications;

// No redaction concerns here unlike CandidateDto — always filtered to the caller's
// own rows server-side (see NotificationsController), never another user's.
public class NotificationDto
{
    public int NotificationId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedUtc { get; set; }
}
