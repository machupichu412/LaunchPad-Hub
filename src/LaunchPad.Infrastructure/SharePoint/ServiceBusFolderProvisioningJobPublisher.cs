using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using LaunchPad.Application.SharePoint;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LaunchPad.Infrastructure.SharePoint;

/// <summary>
/// Publishes to the "sharepoint-provisioning" Service Bus queue via managed identity —
/// mirrors ServiceBusMatchingJobPublisher exactly, including the no-op-when-unconfigured
/// behavior (creating a cohort/candidate/project must still succeed even if its folder
/// provisioning doesn't get queued in an environment without a Service Bus namespace yet).
/// </summary>
public sealed class ServiceBusFolderProvisioningJobPublisher : IFolderProvisioningJobPublisher
{
    private readonly string? _namespace;
    private readonly string _queueName;
    private readonly ILogger<ServiceBusFolderProvisioningJobPublisher> _logger;

    public ServiceBusFolderProvisioningJobPublisher(IConfiguration configuration, ILogger<ServiceBusFolderProvisioningJobPublisher> logger)
    {
        _namespace = configuration["ServiceBus:Namespace"];
        _queueName = configuration["ServiceBus:SharePointProvisioningQueueName"] ?? "sharepoint-provisioning";
        _logger = logger;
    }

    public async Task PublishAsync(FolderProvisioningJob job, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_namespace) || _namespace.Contains("<env>"))
        {
            _logger.LogWarning(
                "ServiceBus:Namespace not configured — folder provisioning job for {TargetType} {TargetId} was not queued.",
                job.TargetType, job.TargetId);
            return;
        }

        await using var client = new ServiceBusClient(_namespace, new DefaultAzureCredential());
        var sender = client.CreateSender(_queueName);
        var body = JsonSerializer.Serialize(job);
        await sender.SendMessageAsync(new ServiceBusMessage(body), ct);
    }
}
