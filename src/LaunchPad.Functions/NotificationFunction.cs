using System.Text.Json;
using LaunchPad.Application.Notifications;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LaunchPad.Functions;

/// <summary>
/// Consumes the "notifications" queue (project submission / approval decision emails —
/// see ProjectsController.Submit/Approve/Reject) and relays each message to Graph. Stays
/// a thin relay on purpose: subject/body are already fully formed by the API, so this
/// function owns no template or recipient-resolution logic of its own.
/// </summary>
public sealed class NotificationFunction
{
    private readonly IEmailNotifier _emailNotifier;
    private readonly ILogger<NotificationFunction> _logger;

    public NotificationFunction(IEmailNotifier emailNotifier, ILogger<NotificationFunction> logger)
    {
        _emailNotifier = emailNotifier;
        _logger = logger;
    }

    [Function(nameof(NotificationFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger("notifications", Connection = "ServiceBusConnection")] string message,
        CancellationToken ct)
    {
        var notification = JsonSerializer.Deserialize<NotificationMessage>(message);
        if (notification is null)
        {
            _logger.LogWarning("Received an unparseable notification message — skipping.");
            return;
        }

        await _emailNotifier.SendAsync(notification.ToUpn, notification.Subject, notification.Body, ct);
    }
}
