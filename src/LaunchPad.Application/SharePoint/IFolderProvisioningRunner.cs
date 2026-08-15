namespace LaunchPad.Application.SharePoint;

/// <summary>The orchestration layer a Function (or the API's synchronous self-heal path)
/// actually calls — owns persistence, unlike the pure IFolderProvisioner.</summary>
public interface IFolderProvisioningRunner
{
    Task RunAsync(FolderProvisioningJob job, CancellationToken ct = default);
}
