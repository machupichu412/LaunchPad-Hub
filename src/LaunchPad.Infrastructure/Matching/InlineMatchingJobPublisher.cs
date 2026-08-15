using LaunchPad.Application.Matching;

namespace LaunchPad.Infrastructure.Matching;

/// <summary>
/// Local-dev/demo fallback: runs the cohort matching job immediately in-process instead of
/// publishing to Service Bus, since no Service Bus emulator exists in this repo. Selected
/// via Program.cs's Matching:RunInlineForLocalDemo config gate — the same pattern as
/// Database:UseInMemoryForLocalDemo.
/// </summary>
public sealed class InlineMatchingJobPublisher : IMatchingJobPublisher
{
    private readonly ICohortMatchingRunner _runner;

    public InlineMatchingJobPublisher(ICohortMatchingRunner runner) => _runner = runner;

    public Task PublishAsync(CohortMatchingJob job, CancellationToken ct = default) =>
        _runner.RunAsync(job.CohortId, job.TriggeredByEntraObjectId, ct);
}
