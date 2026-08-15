using System.Collections.Concurrent;
using LaunchPad.Application.Matching;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Replaces IMatchingJobPublisher in tests — runs the job synchronously via
/// ICohortMatchingRunner in the same request scope (mirrors InlineMatchingJobPublisher's
/// local-dev behavior) so a POST /api/matching/run has already produced its Assignment
/// rows by the time the HTTP response returns, no real Service Bus involved. Also records
/// what was published for tests that want to assert on the job itself.
/// </summary>
public sealed class FakeMatchingJobPublisher : IMatchingJobPublisher
{
    private readonly ICohortMatchingRunner _runner;

    public FakeMatchingJobPublisher(ICohortMatchingRunner runner) => _runner = runner;

    public ConcurrentBag<CohortMatchingJob> Published { get; } = new();

    public async Task PublishAsync(CohortMatchingJob job, CancellationToken ct = default)
    {
        Published.Add(job);
        await _runner.RunAsync(job.CohortId, job.TriggeredByEntraObjectId, ct);
    }
}
