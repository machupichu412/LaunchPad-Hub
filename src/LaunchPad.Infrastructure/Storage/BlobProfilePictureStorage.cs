using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LaunchPad.Application.Common;
using Microsoft.Extensions.Configuration;

namespace LaunchPad.Infrastructure.Storage;

/// <summary>
/// Real implementation — writes into the existing "artifacts" container (see
/// infra/modules/storage.bicep, already sized for resumes/deliverables/recordings)
/// under an avatars/{appUserId}/{guid}.{ext} prefix, via managed identity — no
/// connection string, same convention as ServiceBusNotificationPublisher. Only
/// registered when Storage:AccountUrl is configured (see Program.cs); this sandbox
/// has no real Azure Storage account, so LocalDiskProfilePictureStorage is what
/// actually runs here.
/// </summary>
public sealed class BlobProfilePictureStorage : IProfilePictureStorage
{
    private const string ContainerName = "artifacts";
    private readonly BlobContainerClient _container;

    public BlobProfilePictureStorage(IConfiguration configuration)
    {
        var accountUrl = configuration["Storage:AccountUrl"]!;
        var serviceClient = new BlobServiceClient(new Uri(accountUrl), new DefaultAzureCredential());
        _container = serviceClient.GetBlobContainerClient(ContainerName);
    }

    public async Task<string> SaveAsync(int appUserId, Stream content, string contentType, CancellationToken ct = default)
    {
        var blobName = $"avatars/{appUserId}/{Guid.NewGuid()}{ExtensionFor(contentType)}";
        var blobClient = _container.GetBlobClient(blobName);
        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            ct);
        return blobName;
    }

    public async Task<(Stream Content, string ContentType)?> GetAsync(string storageKey, CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(storageKey);
        if (!await blobClient.ExistsAsync(ct)) return null;

        var download = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return (download.Value.Content, download.Value.Details.ContentType);
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct = default) =>
        await _container.GetBlobClient(storageKey).DeleteIfExistsAsync(cancellationToken: ct);

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg",
    };
}
