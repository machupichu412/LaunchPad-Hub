using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
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
    private readonly string? _namespace;
    private readonly string _queueName;
    private readonly ILogger<ServiceBusMatchingJobPublisher> _logger;

    public ServiceBusMatchingJobPublisher(IConfiguration configuration, ILogger<ServiceBusMatchingJobPublisher> logger)
    {
        _namespace = configuration["ServiceBus:Namespace"];
        _queueName = configuration["ServiceBus:MatchingJobsQueueName"] ?? "matching-jobs";
        _logger = logger;
    }

    public async Task PublishAsync(CohortMatchingJob job, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_namespace) || _namespace.Contains("<env>"))
        {
            _logger.LogWarning(
                "ServiceBus:Namespace not configured — matching job for cohort {CohortId} was not queued.",
                job.CohortId);
            return;
        }

        await using var client = new ServiceBusClient(_namespace, new DefaultAzureCredential());
        var sender = client.CreateSender(_queueName);
        var body = JsonSerializer.Serialize(job);
        await sender.SendMessageAsync(new ServiceBusMessage(body), ct);
    }
}
