using LaunchPad.Application.Common;
using Microsoft.Extensions.Hosting;

namespace LaunchPad.Infrastructure.Storage;

/// <summary>
/// Local-dev fallback, registered when Storage:AccountUrl isn't configured — same
/// "gracefully degrade for local dev" shape as LocalDiskProfilePictureStorage.
/// </summary>
public sealed class LocalDiskCommunityImageStorage : ICommunityImageStorage
{
    private readonly string _rootPath;

    public LocalDiskCommunityImageStorage(IHostEnvironment environment)
    {
        _rootPath = Path.Combine(environment.ContentRootPath, "App_Data", "community-images");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(int postId, Stream content, string contentType, CancellationToken ct = default)
    {
        var fileName = $"{postId}_{Guid.NewGuid()}{ExtensionFor(contentType)}";
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

    // storageKey is always a bare file name this class generated itself — reject anything
    // that could escape _rootPath, same guard as LocalDiskProfilePictureStorage.
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
