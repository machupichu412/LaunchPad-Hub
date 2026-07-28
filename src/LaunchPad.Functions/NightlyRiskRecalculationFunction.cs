using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LaunchPad.Functions;

/// <summary>
/// Off-peak, full-table scan. Risk itself is computed by dbo.vCandidateRisk, so
/// this function's job is refreshing anything materialized from it (e.g. sponsor
/// auto-flags, digest queues) — never duplicating the risk logic itself.
/// </summary>
public sealed class NightlyRiskRecalculationFunction
{
    private readonly ILogger<NightlyRiskRecalculationFunction> _logger;

    public NightlyRiskRecalculationFunction(ILogger<NightlyRiskRecalculationFunction> logger) => _logger = logger;

    [Function(nameof(NightlyRiskRecalculationFunction))]
    public Task RunAsync([TimerTrigger("0 0 3 * * *")] TimerInfo timer)
    {
        _logger.LogInformation("Nightly risk recalculation run started at: {Time}", DateTime.UtcNow);
        // TODO (Phase 4): query dbo.vCandidateRisk for newly-flagged candidates,
        // evaluate sponsor auto-flag thresholds, enqueue notification digests.
        return Task.CompletedTask;
    }
}
