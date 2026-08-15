using System.Collections.Concurrent;
using LaunchPad.Application.SharePoint;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Replaces IFolderProvisioningJobPublisher in tests — runs the job synchronously via
/// IFolderProvisioningRunner in the same request scope (mirrors InlineFolderProvisioningJobPublisher's
/// local-dev behavior), same shape as FakeMatchingJobPublisher.
/// </summary>
public sealed class FakeFolderProvisioningJobPublisher : IFolderProvisioningJobPublisher
{
    private readonly IFolderProvisioningRunner _runner;

    public FakeFolderProvisioningJobPublisher(IFolderProvisioningRunner runner) => _runner = runner;

    public ConcurrentBag<FolderProvisioningJob> Published { get; } = new();

    public async Task PublishAsync(FolderProvisioningJob job, CancellationToken ct = default)
    {
        Published.Add(job);
        await _runner.RunAsync(job, ct);
    }
}
