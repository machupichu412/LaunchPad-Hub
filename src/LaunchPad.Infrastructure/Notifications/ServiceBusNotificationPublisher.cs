using System.Text.Json;
using Azure.Messaging.ServiceBus;
using LaunchPad.Infrastructure.Messaging;
using LaunchPad.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LaunchPad.Infrastructure.Notifications;

/// <summary>
/// Publishes to the "notifications" Service Bus queue via managed identity — no
/// connection string, matching the "no secrets in appsettings" convention. If
/// ServiceBus:Namespace isn't configured (this local sandbox, or any environment before
/// its Service Bus namespace is provisioned), logs and returns instead of throwing: a
/// project submission/approval must still succeed even when the notification backbone
/// isn't reachable.
/// </summary>
public sealed class ServiceBusNotificationPublisher : INotificationPublisher
{
    private readonly ServiceBusSenderProvider _senders;
    private readonly string _queueName;
    private readonly ILogger<ServiceBusNotificationPublisher> _logger;

    public ServiceBusNotificationPublisher(ServiceBusSenderProvider senders, IConfiguration configuration, ILogger<ServiceBusNotificationPublisher> logger)
    {
        _senders = senders;
        _queueName = configuration["ServiceBus:NotificationsQueueName"] ?? "notifications";
        _logger = logger;
    }

    public async Task PublishAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (!_senders.IsConfigured)
        {
            _logger.LogWarning(
                "ServiceBus:Namespace not configured — notification to {ToUpn} ({Subject}) was not queued.",
                message.ToUpn, message.Subject);
            return;
        }

        var body = JsonSerializer.Serialize(message);
        await _senders.GetSender(_queueName).SendMessageAsync(new ServiceBusMessage(body), ct);
    }
}
