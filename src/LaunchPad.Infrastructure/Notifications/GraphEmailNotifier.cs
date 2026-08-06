using Azure.Identity;
using LaunchPad.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace LaunchPad.Infrastructure.Notifications;

/// <summary>
/// Sends mail via the real Microsoft Graph SDK, app-only (Mail.Send application
/// permission), as a configured shared mailbox — no per-user delegated auth. Uses
/// DefaultAzureCredential (managed identity in Azure; app registration env vars locally)
/// so no client secret ever lives in appsettings. If Graph:TenantId or
/// Graph:NotificationSenderUpn isn't configured (this local sandbox has neither), logs and
/// no-ops — that's expected here, not an error. If they ARE configured and the Graph call
/// itself fails, the exception propagates so Service Bus retries/dead-letters the message
/// instead of silently losing it.
/// </summary>
public sealed class GraphEmailNotifier : IEmailNotifier
{
    private readonly string? _tenantId;
    private readonly string? _senderUpn;
    private readonly ILogger<GraphEmailNotifier> _logger;

    public GraphEmailNotifier(IConfiguration configuration, ILogger<GraphEmailNotifier> logger)
    {
        _tenantId = configuration["Graph:TenantId"];
        _senderUpn = configuration["Graph:NotificationSenderUpn"];
        _logger = logger;
    }

    public async Task SendAsync(string toUpn, string subject, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_tenantId) || string.IsNullOrWhiteSpace(_senderUpn))
        {
            _logger.LogWarning(
                "Graph:TenantId/Graph:NotificationSenderUpn not configured — email to {ToUpn} ({Subject}) was not sent.",
                toUpn, subject);
            return;
        }

        var graphClient = new GraphServiceClient(new DefaultAzureCredential(), new[] { "https://graph.microsoft.com/.default" });

        var requestBody = new SendMailPostRequestBody
        {
            Message = new Message
            {
                Subject = subject,
                Body = new ItemBody { ContentType = BodyType.Text, Content = body },
                ToRecipients = new List<Recipient>
                {
                    new() { EmailAddress = new EmailAddress { Address = toUpn } },
                },
            },
            SaveToSentItems = false,
        };

        await graphClient.Users[_senderUpn].SendMail.PostAsync(requestBody, cancellationToken: ct);
    }
}
