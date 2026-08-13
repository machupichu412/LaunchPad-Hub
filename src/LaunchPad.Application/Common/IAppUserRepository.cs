using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Common;

/// <summary>
/// Minimal lookup for the numeric AppUserId behind the caller's EntraObjectId —
/// needed wherever a foreign key (not just an identity claim) is required, e.g.
/// CommunityPost.AuthorAppUserId. ICurrentUser stays a pure claims-reader; this is
/// the one DB-backed hop past it.
/// </summary>
public interface IAppUserRepository
{
    Task<int?> GetIdByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default);

    /// <summary>Same lookup, keyed by UPN instead — needed by CompositeNotificationPublisher,
    /// which only has a NotificationMessage.ToUpn (a config-only address like
    /// Notifications:ProgramOpsUpn may not resolve to any AppUser, which is fine).</summary>
    Task<int?> GetIdByUpnAsync(string upn, CancellationToken ct = default);

    /// <summary>The full row, not just the id — needed by MeController's avatar
    /// endpoints, which read and write AvatarBlobPath.</summary>
    Task<AppUser?> GetByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
