namespace LaunchPad.Application.Matching;

/// <summary>
/// Pure, unit-testable matching logic — no EF Core, no I/O. Eligibility gates
/// (e.g. availability) filter candidates out entirely; weighted scoring then ranks
/// the remainder. Callers (Functions, orchestrating a cohort-wide run) own persistence.
/// </summary>
public interface IMatchingEngine
{
    IReadOnlyList<MatchResult> RankTopMatches(MatchProject project, IReadOnlyCollection<MatchCandidate> candidates, int topN = 3);
}
