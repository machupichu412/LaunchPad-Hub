namespace LaunchPad.Application.SharePoint;

/// <summary>
/// Ensures a folder exists in the SharePoint site Program Ops already administers (see
/// SharePoint:SiteId) — never creates the site itself, only organizes folders within it.
/// Every Ensure* call is idempotent (path-addressed "create if missing"), so re-provisioning
/// an already-existing folder is always safe — this is what makes the self-healing
/// candidate/project-folder-under-cohort-folder ordering possible without a stricter
/// dependency guarantee between provisioning jobs.
/// </summary>
public interface IFolderProvisioner
{
    Task<(string FolderId, string? WebUrl)> EnsureCohortFolderAsync(string cohortName, CancellationToken ct = default);

    Task<(string FolderId, string? WebUrl)> EnsureCandidateFolderAsync(
        string cohortName, string candidateDisplayName, CancellationToken ct = default);

    Task<(string FolderId, string? WebUrl)> EnsureProjectFolderAsync(
        string cohortName, string projectName, CancellationToken ct = default);
}
