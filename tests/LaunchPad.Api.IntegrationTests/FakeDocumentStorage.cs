using System.Collections.Concurrent;
using LaunchPad.Application.SharePoint;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Replaces IDocumentStorage in tests — an in-memory dictionary instead of a real Graph
/// upload/download, so a deliverable POST/GET round-trip can be asserted byte-for-byte
/// through the actual HTTP pipeline without touching SharePoint.
/// </summary>
public sealed class FakeDocumentStorage : IDocumentStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Content, string ContentType)> _files = new();

    public async Task<string> SaveAsync(
        string folderItemId, string fileName, Stream content, string contentType, long contentLength, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var itemId = $"fake-file:{Guid.NewGuid()}";
        _files[itemId] = (buffer.ToArray(), contentType);
        return itemId;
    }

    public Task<(Stream Content, string ContentType)?> GetAsync(string fileItemId, CancellationToken ct = default)
    {
        if (!_files.TryGetValue(fileItemId, out var file))
        {
            return Task.FromResult<(Stream, string)?>(null);
        }

        return Task.FromResult<(Stream, string)?>((new MemoryStream(file.Content), file.ContentType));
    }

    public Task DeleteAsync(string fileItemId, CancellationToken ct = default)
    {
        _files.TryRemove(fileItemId, out _);
        return Task.CompletedTask;
    }
}
