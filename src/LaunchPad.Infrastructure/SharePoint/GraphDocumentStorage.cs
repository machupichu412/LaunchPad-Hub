using LaunchPad.Application.SharePoint;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace LaunchPad.Infrastructure.SharePoint;

/// <summary>
/// Real Graph SDK implementation of deliverable file storage. Branches on contentLength:
/// Graph's simple PUT-to-content upload only supports files up to 4 MiB; anything larger
/// needs an upload session + chunked LargeFileUploadTask. The large-file path is the one
/// piece of this whole feature that genuinely cannot be exercised without a real SharePoint
/// site — flagged again in the manual smoke-test checklist in the plan.
///
/// Graph SDK v5 dropped ItemWithPath(); a file is addressed relative to its parent folder's
/// item ID via the same "{parentId}:/{fileName}:" colon syntax used for folders (see
/// GraphFolderProvisioner). The file name is run through FolderNameSanitizer first so it can
/// never itself contain a colon or slash that would break that addressing syntax.
/// </summary>
public sealed class GraphDocumentStorage : IDocumentStorage
{
    private const long SmallFileThresholdBytes = 4 * 1024 * 1024;

    // Must be a multiple of 320 KiB per Graph's upload-session chunking requirement.
    private const int UploadChunkSize = 5 * 320 * 1024;

    private readonly GraphServiceClient _graphClient;
    private readonly GraphDriveResolver _driveResolver;

    public GraphDocumentStorage(GraphServiceClient graphClient, GraphDriveResolver driveResolver)
    {
        _graphClient = graphClient;
        _driveResolver = driveResolver;
    }

    public async Task<string> SaveAsync(
        string folderItemId, string fileName, Stream content, string contentType, long contentLength, CancellationToken ct = default)
    {
        var driveId = await _driveResolver.GetDriveIdAsync(ct);
        var sanitizedFileName = FolderNameSanitizer.Sanitize(fileName);
        var pathAddressedFile = $"{folderItemId}:/{sanitizedFileName}:";

        if (contentLength <= SmallFileThresholdBytes)
        {
            var uploaded = await _graphClient.Drives[driveId].Items[pathAddressedFile]
                .Content.PutAsync(content, cancellationToken: ct);
            return uploaded?.Id ?? throw new InvalidOperationException("SharePoint upload did not return an item ID.");
        }

        var uploadSession = await _graphClient.Drives[driveId].Items[pathAddressedFile]
            .CreateUploadSession.PostAsync(
                new Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession.CreateUploadSessionPostRequestBody
                {
                    Item = new DriveItemUploadableProperties
                    {
                        AdditionalData = new Dictionary<string, object> { ["@microsoft.graph.conflictBehavior"] = "replace" },
                    },
                },
                cancellationToken: ct)
            ?? throw new InvalidOperationException("SharePoint did not return an upload session.");

        var uploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, content, UploadChunkSize, _graphClient.RequestAdapter);
        var uploadResult = await uploadTask.UploadAsync(cancellationToken: ct);
        if (!uploadResult.UploadSucceeded)
        {
            throw new InvalidOperationException($"SharePoint large-file upload of '{fileName}' failed.");
        }

        return uploadResult.ItemResponse?.Id ?? throw new InvalidOperationException("SharePoint upload did not return an item ID.");
    }

    public async Task<(Stream Content, string ContentType)?> GetAsync(string fileItemId, CancellationToken ct = default)
    {
        var driveId = await _driveResolver.GetDriveIdAsync(ct);

        DriveItem? item;
        try
        {
            item = await _graphClient.Drives[driveId].Items[fileItemId].GetAsync(cancellationToken: ct);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            return null;
        }

        if (item is null) return null;

        var content = await _graphClient.Drives[driveId].Items[fileItemId].Content.GetAsync(cancellationToken: ct);
        if (content is null) return null;

        return (content, item.File?.MimeType ?? "application/octet-stream");
    }

    public async Task DeleteAsync(string fileItemId, CancellationToken ct = default)
    {
        var driveId = await _driveResolver.GetDriveIdAsync(ct);
        try
        {
            await _graphClient.Drives[driveId].Items[fileItemId].DeleteAsync(cancellationToken: ct);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            // Already gone.
        }
    }
}
