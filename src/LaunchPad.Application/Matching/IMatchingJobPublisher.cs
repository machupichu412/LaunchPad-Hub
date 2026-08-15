namespace LaunchPad.Application.Matching;

/// <summary>
/// Publishes a cohort-matching job for async execution — the API's side of the Service Bus
/// + Function pipeline (see CLAUDE.md's async-work rule: cohort-wide matching must not run
/// inline on the request thread). Same shape as INotificationPublisher.
/// </summary>
public interface IMatchingJobPublisher
{
    Task PublishAsync(CohortMatchingJob job, CancellationToken ct = default);
}
