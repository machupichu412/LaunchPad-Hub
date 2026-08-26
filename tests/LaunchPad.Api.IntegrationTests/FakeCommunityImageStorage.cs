using System.Collections.Concurrent;
using LaunchPad.Application.Common;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Replaces ICommunityImageStorage in tests — an in-memory dictionary instead of a real Blob
/// upload/download, so a post-image round-trip can be asserted byte-for-byte through the
/// actual HTTP pipeline without touching Azure Storage. Mirrors FakeDocumentStorage's shape.
/// </summary>
public sealed class FakeCommunityImageStorage : ICommunityImageStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Content, string ContentType)> _images = new();

    public async Task<string> SaveAsync(int postId, Stream content, string contentType, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var storageKey = $"fake-image:{postId}:{Guid.NewGuid()}";
        _images[storageKey] = (buffer.ToArray(), contentType);
        return storageKey;
    }

    public Task<(Stream Content, string ContentType)?> GetAsync(string storageKey, CancellationToken ct = default)
    {
        if (!_images.TryGetValue(storageKey, out var image))
        {
            return Task.FromResult<(Stream, string)?>(null);
        }

        return Task.FromResult<(Stream, string)?>((new MemoryStream(image.Content), image.ContentType));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        _images.TryRemove(storageKey, out _);
        return Task.CompletedTask;
    }
}
