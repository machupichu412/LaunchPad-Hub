using System.Text.Json;
using LaunchPad.Application.Matching;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LaunchPad.Functions;

/// <summary>
/// Triggered when Ops clicks "Run matching" for a cohort (ProjectsController/MatchingController
/// publishes a CohortMatchingJob to this queue instead of running inline). The actual algorithm
/// stays in LaunchPad.Application (ICohortMatchingRunner — pure scoring via IMatchingEngine, plus
/// its own persistence) — this function is a thin relay, same shape as NotificationFunction.
/// </summary>
public sealed class CohortMatchingFunction
{
    private readonly ICohortMatchingRunner _runner;
    private readonly ILogger<CohortMatchingFunction> _logger;

    public CohortMatchingFunction(ICohortMatchingRunner runner, ILogger<CohortMatchingFunction> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    [Function(nameof(CohortMatchingFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger("matching-jobs", Connection = "ServiceBusConnection")] string message,
        CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<CohortMatchingJob>(message);
        if (job is null)
        {
            _logger.LogWarning("Received an unparseable matching job message — skipping.");
            return;
        }

        var proposedCount = await _runner.RunAsync(job.CohortId, job.TriggeredByEntraObjectId, ct);
        _logger.LogInformation(
            "Matching run for cohort {CohortId} proposed {ProposedCount} new assignment(s).", job.CohortId, proposedCount);
    }
}
