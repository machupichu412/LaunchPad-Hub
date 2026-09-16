using System.Text.Json;
using Azure.Messaging.ServiceBus;
using LaunchPad.Infrastructure.Messaging;
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
    private readonly ServiceBusSenderProvider _senders;
    private readonly string _queueName;
    private readonly ILogger<ServiceBusFolderProvisioningJobPublisher> _logger;

    public ServiceBusFolderProvisioningJobPublisher(ServiceBusSenderProvider senders, IConfiguration configuration, ILogger<ServiceBusFolderProvisioningJobPublisher> logger)
    {
        _senders = senders;
        _queueName = configuration["ServiceBus:SharePointProvisioningQueueName"] ?? "sharepoint-provisioning";
        _logger = logger;
    }

    public async Task PublishAsync(FolderProvisioningJob job, CancellationToken ct = default)
    {
        if (!_senders.IsConfigured)
        {
            _logger.LogWarning(
                "ServiceBus:Namespace not configured — folder provisioning job for {TargetType} {TargetId} was not queued.",
                job.TargetType, job.TargetId);
            return;
        }

        var body = JsonSerializer.Serialize(job);
        await _senders.GetSender(_queueName).SendMessageAsync(new ServiceBusMessage(body), ct);
    }
}
