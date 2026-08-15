using LaunchPad.Application.SharePoint;

namespace LaunchPad.Infrastructure.SharePoint;

/// <summary>
/// Local-dev/demo fallback: runs the folder-provisioning job immediately in-process instead
/// of publishing to Service Bus. Selected via Program.cs's SharePoint:ProvisionInlineForLocalDemo
/// config gate — the same pattern as InlineMatchingJobPublisher.
/// </summary>
public sealed class InlineFolderProvisioningJobPublisher : IFolderProvisioningJobPublisher
{
    private readonly IFolderProvisioningRunner _runner;

    public InlineFolderProvisioningJobPublisher(IFolderProvisioningRunner runner) => _runner = runner;

    public Task PublishAsync(FolderProvisioningJob job, CancellationToken ct = default) =>
        _runner.RunAsync(job, ct);
}
