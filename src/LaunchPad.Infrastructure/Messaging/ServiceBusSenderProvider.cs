using System.Collections.Concurrent;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace LaunchPad.Infrastructure.Messaging;

/// <summary>
/// One ServiceBusClient for the process, and one cached sender per queue.
///
/// Each publisher used to construct its own client per message and dispose it, which
/// paid for an AMQP connection and a token acquisition on every publish — on the request
/// thread, since notifications are published inline from controller actions. Both types
/// are thread-safe and designed to be long-lived, so sharing them is the intended usage.
///
/// Also the single place that answers "is Service Bus configured at all?", so the three
/// publishers' graceful degradation (log and return rather than throw, so a project
/// submission still succeeds when the notification backbone is unreachable) stays
/// identical across all of them instead of being re-implemented three times.
/// </summary>
public sealed class ServiceBusSenderProvider : IAsyncDisposable
{
    private readonly string? _namespace;
    private readonly Lazy<ServiceBusClient>? _client;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public ServiceBusSenderProvider(IConfiguration configuration)
    {
        _namespace = configuration["ServiceBus:Namespace"];

        // Lazy so an unconfigured or never-used host never constructs a credential chain.
        if (IsConfigured)
        {
            _client = new Lazy<ServiceBusClient>(
                () => new ServiceBusClient(_namespace, new DefaultAzureCredential()));
        }
    }

    /// <summary>"&lt;env&gt;" is the placeholder left in appsettings.json for an environment
    /// whose namespace hasn't been provisioned — treated the same as absent.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_namespace) && !_namespace.Contains("<env>");

    public ServiceBusSender GetSender(string queueName) =>
        _senders.GetOrAdd(queueName, queue => _client!.Value.CreateSender(queue));

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }

        if (_client is { IsValueCreated: true })
        {
            await _client.Value.DisposeAsync();
        }
    }
}
