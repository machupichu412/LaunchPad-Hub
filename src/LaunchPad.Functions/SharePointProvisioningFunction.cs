using System.Text.Json;
using LaunchPad.Application.SharePoint;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LaunchPad.Functions;

/// <summary>
/// Triggered when a cohort/candidate/project is created (CohortsController/CandidatesController/
/// ProjectsController publishes a FolderProvisioningJob to this queue). The actual Graph work stays
/// in LaunchPad.Application/Infrastructure (IFolderProvisioningRunner) — this function is a thin
/// relay, same shape as CohortMatchingFunction.
/// </summary>
public sealed class SharePointProvisioningFunction
{
    private readonly IFolderProvisioningRunner _runner;
    private readonly ILogger<SharePointProvisioningFunction> _logger;

    public SharePointProvisioningFunction(IFolderProvisioningRunner runner, ILogger<SharePointProvisioningFunction> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    [Function(nameof(SharePointProvisioningFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger("sharepoint-provisioning", Connection = "ServiceBusConnection")] string message,
        CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<FolderProvisioningJob>(message);
        if (job is null)
        {
            _logger.LogWarning("Received an unparseable folder-provisioning job message — skipping.");
            return;
        }

        await _runner.RunAsync(job, ct);
        _logger.LogInformation(
            "Folder provisioning completed for {TargetType} {TargetId}.", job.TargetType, job.TargetId);
    }
}
