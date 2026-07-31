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
}
