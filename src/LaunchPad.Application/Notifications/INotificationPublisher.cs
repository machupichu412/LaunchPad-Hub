namespace LaunchPad.Application.Notifications;

/// <summary>
/// Publishes a notification for async delivery — the API's side of the Service Bus +
/// Function pipeline (see CLAUDE.md's async-work rule: external I/O like a Graph send
/// must not run inline on the request thread). Implementations must never throw when the
/// notification backbone isn't configured (e.g. this local sandbox) — log and no-op
/// instead, so submit/approve/reject still succeed without a reachable Service Bus.
/// </summary>
public interface INotificationPublisher
{
    Task PublishAsync(NotificationMessage message, CancellationToken ct = default);
}
