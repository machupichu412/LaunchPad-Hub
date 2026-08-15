namespace LaunchPad.Application.Matching;

/// <summary>
/// The actual cohort-wide matching algorithm, extracted out of any one caller so the real
/// Service-Bus-triggered Function, the local-dev inline fallback, and (indirectly) the API
/// all execute identical logic. Owns persistence (unlike the pure IMatchingEngine) — this is
/// the orchestration layer, not the scoring layer.
/// </summary>
public interface ICohortMatchingRunner
{
    /// <summary>Proposes candidates for every open project's remaining spots in the cohort.
    /// actorEntraObjectId attributes the audit event — carried through explicitly since a
    /// Service-Bus-triggered Function has no ICurrentUser/ambient request to read it from.</summary>
    Task<int> RunAsync(int cohortId, Guid actorEntraObjectId, CancellationToken ct = default);
}
