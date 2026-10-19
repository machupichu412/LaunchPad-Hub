using LaunchPad.Application.Assignments;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LaunchPad.Functions;

/// <summary>
/// Nightly, just before the risk recalculation that reads the statuses it sets. Ops approving
/// an assignment says who works on what; this says when that work actually starts and ends,
/// which nothing else in the app did — approved assignments stayed approved forever, and
/// reviews (Active only) could never be scheduled for them. The decision itself lives in
/// AssignmentLifecycleRunner so it stays unit-testable without a database.
/// </summary>
public sealed class AssignmentLifecycleFunction
{
    private readonly IAssignmentLifecycleRunner _runner;
    private readonly ILogger<AssignmentLifecycleFunction> _logger;

    public AssignmentLifecycleFunction(IAssignmentLifecycleRunner runner, ILogger<AssignmentLifecycleFunction> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    [Function(nameof(AssignmentLifecycleFunction))]
    public async Task RunAsync([TimerTrigger("0 30 2 * * *")] TimerInfo timer, CancellationToken ct)
    {
        var result = await _runner.RunAsync(DateOnly.FromDateTime(DateTime.UtcNow), ct);
        _logger.LogInformation(
            "Assignment lifecycle sweep activated {Activated} and completed {Completed} assignment(s).",
            result.Activated, result.Completed);
    }
}
