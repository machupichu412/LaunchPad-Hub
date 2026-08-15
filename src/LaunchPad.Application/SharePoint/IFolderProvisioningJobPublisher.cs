namespace LaunchPad.Application.SharePoint;

/// <summary>
/// Publishes a folder-provisioning job for async execution — the API's side of the Service
/// Bus + Function pipeline (see CLAUDE.md's async-work rule: multi-step external Graph calls
/// must not run inline on the request thread). Same shape as IMatchingJobPublisher.
/// </summary>
public interface IFolderProvisioningJobPublisher
{
    Task PublishAsync(FolderProvisioningJob job, CancellationToken ct = default);
}
