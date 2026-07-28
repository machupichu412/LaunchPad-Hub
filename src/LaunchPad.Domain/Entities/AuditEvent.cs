namespace LaunchPad.Domain.Entities;

/// <summary>
/// Captures actor intent (who did what and why) for every approval, status change,
/// and score recalculation. Complements — does not replace — SQL temporal tables,
/// which capture row history but not intent.
/// </summary>
public class AuditEvent
{
    public long AuditEventId { get; set; }
    public int ActorAppUserId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? DataJson { get; set; }
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
}
