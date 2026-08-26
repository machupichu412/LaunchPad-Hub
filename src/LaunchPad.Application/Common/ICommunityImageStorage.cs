namespace LaunchPad.Application.Common;

/// <summary>
/// Stores/retrieves the raw bytes behind CommunityPost.ImageBlobPath — same shape as
/// IProfilePictureStorage, kept as a separate interface (not reused/overloaded) so avatar and
/// post-image lifecycles/quotas can diverge later without coupling. The storage key returned
/// by SaveAsync is opaque; every read is proxied by CommunityController, never a public URL.
/// </summary>
public interface ICommunityImageStorage
{
    Task<string> SaveAsync(int postId, Stream content, string contentType, CancellationToken ct = default);

    Task<(Stream Content, string ContentType)?> GetAsync(string storageKey, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
