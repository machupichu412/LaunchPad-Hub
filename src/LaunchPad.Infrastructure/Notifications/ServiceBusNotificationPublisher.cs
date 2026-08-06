using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
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
    private readonly string? _namespace;
    private readonly string _queueName;
    private readonly ILogger<ServiceBusNotificationPublisher> _logger;

    public ServiceBusNotificationPublisher(IConfiguration configuration, ILogger<ServiceBusNotificationPublisher> logger)
    {
        _namespace = configuration["ServiceBus:Namespace"];
        _queueName = configuration["ServiceBus:NotificationsQueueName"] ?? "notifications";
        _logger = logger;
    }

    public async Task PublishAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_namespace) || _namespace.Contains("<env>"))
        {
            _logger.LogWarning(
                "ServiceBus:Namespace not configured — notification to {ToUpn} ({Subject}) was not queued.",
                message.ToUpn, message.Subject);
            return;
        }

        await using var client = new ServiceBusClient(_namespace, new DefaultAzureCredential());
        var sender = client.CreateSender(_queueName);
        var body = JsonSerializer.Serialize(message);
        await sender.SendMessageAsync(new ServiceBusMessage(body), ct);
    }
}
