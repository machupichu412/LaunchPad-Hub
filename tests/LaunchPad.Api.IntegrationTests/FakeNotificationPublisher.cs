using System.Collections.Concurrent;
using LaunchPad.Application.Notifications;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Replaces ServiceBusNotificationPublisher in tests — records what was published
/// instead of touching a real Service Bus namespace, so submit/approve/reject tests can
/// assert the right recipients/content without any Azure dependency.
/// </summary>
public sealed class FakeNotificationPublisher : INotificationPublisher
{
    public ConcurrentBag<NotificationMessage> Sent { get; } = new();

    public Task PublishAsync(NotificationMessage message, CancellationToken ct = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
