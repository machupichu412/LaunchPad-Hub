namespace LaunchPad.Application.Common;

/// <summary>
/// Stores/retrieves the raw bytes behind AppUser.AvatarBlobPath. The storage key
/// returned by SaveAsync is opaque (a blob name or a local file name depending on
/// the implementation) — callers never construct a public URL from it; every read
/// goes through this interface too, proxied by the API (see MeController), so the
/// same code path works whether the backend is real Blob Storage or a local-disk
/// fallback (this sandbox has no Azure Storage account — see Program.cs).
/// </summary>
public interface IProfilePictureStorage
{
    Task<string> SaveAsync(int appUserId, Stream content, string contentType, CancellationToken ct = default);

    Task<(Stream Content, string ContentType)?> GetAsync(string storageKey, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
