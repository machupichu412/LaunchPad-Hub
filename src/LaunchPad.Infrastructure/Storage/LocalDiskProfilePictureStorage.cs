using LaunchPad.Application.Common;
using Microsoft.Extensions.Hosting;

namespace LaunchPad.Infrastructure.Storage;

/// <summary>
/// Local-dev fallback, registered when Storage:AccountUrl isn't configured (this
/// sandbox has no real Azure Storage account) — same "gracefully degrade for local
/// dev" shape as Database:UseInMemoryForLocalDemo in Program.cs. Never used once a
/// real storage account is provisioned.
/// </summary>
public sealed class LocalDiskProfilePictureStorage : IProfilePictureStorage
{
    private readonly string _rootPath;

    public LocalDiskProfilePictureStorage(IHostEnvironment environment)
    {
        _rootPath = Path.Combine(environment.ContentRootPath, "App_Data", "avatars");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(int appUserId, Stream content, string contentType, CancellationToken ct = default)
    {
        var fileName = $"{appUserId}_{Guid.NewGuid()}{ExtensionFor(contentType)}";
        var filePath = Path.Combine(_rootPath, fileName);

        await using var fileStream = File.Create(filePath);
        await content.CopyToAsync(fileStream, ct);

        return fileName;
    }

    public Task<(Stream Content, string ContentType)?> GetAsync(string storageKey, CancellationToken ct = default)
    {
        var filePath = ResolvePath(storageKey);
        if (filePath is null || !File.Exists(filePath))
        {
            return Task.FromResult<(Stream, string)?>(null);
        }

        Stream stream = File.OpenRead(filePath);
        return Task.FromResult<(Stream, string)?>((stream, ContentTypeFor(filePath)));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var filePath = ResolvePath(storageKey);
        if (filePath is not null && File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }

    // storageKey is always a bare file name this class generated itself — reject
    // anything that could escape _rootPath. Nothing in this app currently lets a
    // caller supply an arbitrary storageKey, but this keeps the guarantee local
    // rather than relying on that staying true everywhere forever.
    private string? ResolvePath(string storageKey)
    {
        if (storageKey.Contains("..") || Path.IsPathRooted(storageKey)) return null;
        return Path.Combine(_rootPath, storageKey);
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg",
    };

    private static string ContentTypeFor(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };
}
