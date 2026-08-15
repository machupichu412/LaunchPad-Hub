using LaunchPad.Application.SharePoint;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace LaunchPad.Infrastructure.SharePoint;

/// <summary>
/// Real Graph SDK implementation — never provisions the SharePoint site itself (Program Ops
/// already administers it, see SharePoint:SiteId), only organizes folders within it. Every
/// Ensure* call walks the full path segment by segment, creating whichever segments are
/// missing (idempotent "create if missing" via a get-then-create-on-404 check) — this is
/// what makes candidate/project folder provisioning self-healing even when the cohort's own
/// folder hasn't been created yet, without needing a stricter ordering guarantee between
/// provisioning jobs.
///
/// Graph SDK v5 dropped the typed ItemWithPath() helper that existed in v4 — path-addressed
/// items are now reached via the Items[] indexer using Graph's "root:/{path}:" colon syntax
/// (e.g. Items["root:/LaunchPad/Fall 2026:"], Items["root"] for the drive root itself). Folder
/// segments are sanitized before being used this way so a segment can never itself contain a
/// colon or slash that would break the addressing syntax.
///
/// NOT independently testable without a real SharePoint site/tenant — the path-addressed
/// GetAsync/children-POST call shapes below can only be verified end-to-end there. See the
/// manual smoke-test checklist in the plan.
/// </summary>
public sealed class GraphFolderProvisioner : IFolderProvisioner
{
    private readonly GraphServiceClient _graphClient;
    private readonly GraphDriveResolver _driveResolver;
    private readonly string _rootFolderPath;

    public GraphFolderProvisioner(GraphServiceClient graphClient, GraphDriveResolver driveResolver, IConfiguration configuration)
    {
        _graphClient = graphClient;
        _driveResolver = driveResolver;
        _rootFolderPath = configuration["SharePoint:RootFolderPath"] ?? "LaunchPad";
    }

    public Task<(string FolderId, string? WebUrl)> EnsureCohortFolderAsync(string cohortName, CancellationToken ct = default) =>
        EnsureFolderPathAsync(new[] { _rootFolderPath, FolderNameSanitizer.Sanitize(cohortName) }, ct);

    public Task<(string FolderId, string? WebUrl)> EnsureCandidateFolderAsync(
        string cohortName, string candidateDisplayName, CancellationToken ct = default) =>
        EnsureFolderPathAsync(
            new[] { _rootFolderPath, FolderNameSanitizer.Sanitize(cohortName), "Candidates", FolderNameSanitizer.Sanitize(candidateDisplayName) },
            ct);

    public Task<(string FolderId, string? WebUrl)> EnsureProjectFolderAsync(
        string cohortName, string projectName, CancellationToken ct = default) =>
        EnsureFolderPathAsync(
            new[] { _rootFolderPath, FolderNameSanitizer.Sanitize(cohortName), "Projects", FolderNameSanitizer.Sanitize(projectName) },
            ct);

    private async Task<(string FolderId, string? WebUrl)> EnsureFolderPathAsync(IReadOnlyList<string> segments, CancellationToken ct)
    {
        var driveId = await _driveResolver.GetDriveIdAsync(ct);

        string? parentId = null;
        DriveItem? current = null;
        var pathSoFar = string.Empty;

        foreach (var segment in segments)
        {
            pathSoFar = pathSoFar.Length == 0 ? segment : $"{pathSoFar}/{segment}";
            current = await GetOrCreateFolderAsync(driveId, pathSoFar, parentId, segment, ct);
            parentId = current.Id;
        }

        return (current!.Id!, current.WebUrl);
    }

    private async Task<DriveItem> GetOrCreateFolderAsync(
        string driveId, string fullPath, string? parentId, string segmentName, CancellationToken ct)
    {
        try
        {
            var existing = await _graphClient.Drives[driveId].Items[$"root:/{fullPath}:"].GetAsync(cancellationToken: ct);
            if (existing is not null) return existing;
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            // Doesn't exist yet — fall through and create it.
        }

        var newFolder = new DriveItem
        {
            Name = segmentName,
            Folder = new Folder(),
            AdditionalData = new Dictionary<string, object> { ["@microsoft.graph.conflictBehavior"] = "replace" },
        };

        var created = parentId is null
            ? await _graphClient.Drives[driveId].Items["root"].Children.PostAsync(newFolder, cancellationToken: ct)
            : await _graphClient.Drives[driveId].Items[parentId].Children.PostAsync(newFolder, cancellationToken: ct);

        return created ?? throw new InvalidOperationException($"Failed to create SharePoint folder '{segmentName}' at 'root:/{fullPath}:'.");
    }
}
