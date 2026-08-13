namespace LaunchPad.Application.Common;

/// <summary>
/// Records actor intent (who did what and why) behind AuditEvent — the half of the
/// audit trail SQL temporal tables can't capture on their own (see CLAUDE.md's
/// "Audit" section and launchpad-build-guide.md §4.5/§11). Every approval, status
/// change, and score recalculation should call RecordAsync at the point the
/// transition succeeds.
/// </summary>
public interface IAuditLog
{
    Task RecordAsync(
        Guid actorEntraObjectId,
        string entityName,
        string entityId,
        string action,
        string? reason = null,
        object? data = null,
        CancellationToken ct = default);

    /// <summary>
    /// Entity ids (within entityName) whose most recently recorded action equals
    /// <paramref name="action"/> — e.g. "which candidates are currently auto-flagged."
    /// Lets a repeat-trigger job (nightly risk recalculation) dedup against its own
    /// audit trail instead of a separate status column.
    /// </summary>
    Task<IReadOnlySet<int>> GetEntityIdsWithLatestActionAsync(
        string entityName,
        string action,
        CancellationToken ct = default);
}
