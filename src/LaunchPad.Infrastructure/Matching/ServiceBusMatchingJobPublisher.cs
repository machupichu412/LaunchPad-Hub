using System.Text.Json;
using Azure.Messaging.ServiceBus;
using LaunchPad.Infrastructure.Messaging;
using LaunchPad.Application.Matching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LaunchPad.Infrastructure.Matching;

/// <summary>
/// Publishes to the "matching-jobs" Service Bus queue via managed identity — mirrors
/// ServiceBusNotificationPublisher exactly, including the no-op-when-unconfigured behavior
/// (a cohort matching run must still be requestable, even if it just logs a warning, in any
/// environment before its Service Bus namespace is provisioned).
/// </summary>
public sealed class ServiceBusMatchingJobPublisher : IMatchingJobPublisher
{
    private readonly ServiceBusSenderProvider _senders;
    private readonly string _queueName;
    private readonly ILogger<ServiceBusMatchingJobPublisher> _logger;

    public ServiceBusMatchingJobPublisher(ServiceBusSenderProvider senders, IConfiguration configuration, ILogger<ServiceBusMatchingJobPublisher> logger)
    {
        _senders = senders;
        _queueName = configuration["ServiceBus:MatchingJobsQueueName"] ?? "matching-jobs";
        _logger = logger;
    }

    public async Task PublishAsync(CohortMatchingJob job, CancellationToken ct = default)
    {
        if (!_senders.IsConfigured)
        {
            _logger.LogWarning(
                "ServiceBus:Namespace not configured — matching job for cohort {CohortId} was not queued.",
                job.CohortId);
            return;
        }

        var body = JsonSerializer.Serialize(job);
        await _senders.GetSender(_queueName).SendMessageAsync(new ServiceBusMessage(body), ct);
    }
}
