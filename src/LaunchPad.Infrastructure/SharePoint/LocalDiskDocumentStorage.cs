using LaunchPad.Application.SharePoint;
using Microsoft.Extensions.Hosting;

namespace LaunchPad.Infrastructure.SharePoint;

/// <summary>
/// Local-dev fallback, registered when SharePoint:SiteId isn't configured — mirrors
/// LocalDiskProfilePictureStorage's shape exactly, including the path-traversal guard.
/// folderItemId is whatever LocalDiskFolderProvisioner returned (a relative directory path);
/// the opaque fileItemId this class returns is that same directory plus a generated file name.
/// </summary>
public sealed class LocalDiskDocumentStorage : IDocumentStorage
{
    private readonly string _rootPath;

    public LocalDiskDocumentStorage(IHostEnvironment environment)
    {
        _rootPath = Path.Combine(environment.ContentRootPath, "App_Data", "sharepoint");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(
        string folderItemId, string fileName, Stream content, string contentType, long contentLength, CancellationToken ct = default)
    {
        var folderPath = ResolvePath(folderItemId) ?? throw new InvalidOperationException("Invalid folder ID.");
        Directory.CreateDirectory(folderPath);

        var storedFileName = $"{Guid.NewGuid()}_{fileName}";
        var filePath = Path.Combine(folderPath, storedFileName);

        await using var fileStream = File.Create(filePath);
        await content.CopyToAsync(fileStream, ct);

        return Path.Combine(folderItemId, storedFileName).Replace('\\', '/');
    }

    public Task<(Stream Content, string ContentType)?> GetAsync(string fileItemId, CancellationToken ct = default)
    {
        var filePath = ResolvePath(fileItemId);
        if (filePath is null || !File.Exists(filePath))
        {
            return Task.FromResult<(Stream, string)?>(null);
        }

        Stream stream = File.OpenRead(filePath);
        return Task.FromResult<(Stream, string)?>((stream, ContentTypeFor(filePath)));
    }

    public Task DeleteAsync(string fileItemId, CancellationToken ct = default)
    {
        var filePath = ResolvePath(fileItemId);
        if (filePath is not null && File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }

    // fileItemId is always a relative path this class (or LocalDiskFolderProvisioner) itself
    // generated — reject anything that could escape _rootPath, same guard as
    // LocalDiskProfilePictureStorage.
    private string? ResolvePath(string relativePath)
    {
        if (relativePath.Contains("..") || Path.IsPathRooted(relativePath)) return null;
        return Path.Combine(_rootPath, relativePath);
    }

    private static string ContentTypeFor(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".zip" => "application/zip",
        _ => "application/octet-stream",
    };
}
