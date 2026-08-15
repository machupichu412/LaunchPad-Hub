namespace LaunchPad.Application.SharePoint;

/// <summary>
/// Stores/retrieves a deliverable's actual file content in the candidate's SharePoint
/// folder. The item ID returned by SaveAsync is opaque (a Graph drive-item ID) — callers
/// never construct a SharePoint URL from it; every read is proxied through
/// AssignmentsController's download endpoint, same "opaque key, API-mediated read" contract
/// as IProfilePictureStorage. contentLength lets the real implementation pick a small-file
/// vs. large-file Graph upload path without buffering the stream twice to find out.
/// </summary>
public interface IDocumentStorage
{
    Task<string> SaveAsync(
        string folderItemId, string fileName, Stream content, string contentType, long contentLength, CancellationToken ct = default);

    Task<(Stream Content, string ContentType)?> GetAsync(string fileItemId, CancellationToken ct = default);

    Task DeleteAsync(string fileItemId, CancellationToken ct = default);
}
