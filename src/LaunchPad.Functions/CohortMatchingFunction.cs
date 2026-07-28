using LaunchPad.Application.Matching;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LaunchPad.Functions;

/// <summary>
/// Triggered when Ops clicks "Run matching" for a cohort. Matching itself stays in
/// LaunchPad.Application (pure, unit-tested) — this function only orchestrates
/// loading candidates/projects and persisting the top-3 results.
/// </summary>
public sealed class CohortMatchingFunction
{
    private readonly IMatchingEngine _matchingEngine;
    private readonly ILogger<CohortMatchingFunction> _logger;

    public CohortMatchingFunction(IMatchingEngine matchingEngine, ILogger<CohortMatchingFunction> logger)
    {
        _matchingEngine = matchingEngine;
        _logger = logger;
    }

    [Function(nameof(CohortMatchingFunction))]
    public Task RunAsync(
        [ServiceBusTrigger("matching-jobs", Connection = "ServiceBusConnection")] string message)
    {
        _logger.LogInformation("Received matching job: {Message}", message);
        // TODO (Phase 3): deserialize CohortMatchingJob, load candidates/projects via
        // repositories, call _matchingEngine.RankTopMatches per project, persist Assignments.
        return Task.CompletedTask;
    }
}
