using LaunchPad.Application.SharePoint;
using Microsoft.Extensions.Hosting;

namespace LaunchPad.Infrastructure.SharePoint;

/// <summary>
/// Local-dev fallback, registered when SharePoint:SiteId isn't configured (this sandbox has
/// no real SharePoint site) — same "gracefully degrade for local dev" shape as
/// LocalDiskProfilePictureStorage. Creates real directories under App_Data/sharepoint/...;
/// the relative path itself is the opaque "folder ID." WebUrl always stays null here — the
/// frontend's "View in SharePoint" link naturally disappears locally with no extra gating.
/// </summary>
public sealed class LocalDiskFolderProvisioner : IFolderProvisioner
{
    private readonly string _rootPath;

    public LocalDiskFolderProvisioner(IHostEnvironment environment)
    {
        _rootPath = Path.Combine(environment.ContentRootPath, "App_Data", "sharepoint");
        Directory.CreateDirectory(_rootPath);
    }

    public Task<(string FolderId, string? WebUrl)> EnsureCohortFolderAsync(string cohortName, CancellationToken ct = default) =>
        Task.FromResult(EnsureFolder(FolderNameSanitizer.Sanitize(cohortName)));

    public Task<(string FolderId, string? WebUrl)> EnsureCandidateFolderAsync(
        string cohortName, string candidateDisplayName, CancellationToken ct = default) =>
        Task.FromResult(EnsureFolder(Path.Combine(
            FolderNameSanitizer.Sanitize(cohortName), "Candidates", FolderNameSanitizer.Sanitize(candidateDisplayName))));

    public Task<(string FolderId, string? WebUrl)> EnsureProjectFolderAsync(
        string cohortName, string projectName, CancellationToken ct = default) =>
        Task.FromResult(EnsureFolder(Path.Combine(
            FolderNameSanitizer.Sanitize(cohortName), "Projects", FolderNameSanitizer.Sanitize(projectName))));

    private (string FolderId, string? WebUrl) EnsureFolder(string relativePath)
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, relativePath));
        return (relativePath, null);
    }
}
