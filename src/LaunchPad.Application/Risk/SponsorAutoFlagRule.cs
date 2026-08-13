namespace LaunchPad.Application.Risk;

/// <summary>Risk state for one candidate, as already computed by dbo.vCandidateRisk
/// (see CandidateRiskConfiguration) — never re-derived here.</summary>
public readonly record struct CandidateRiskSnapshot(int CandidateId, bool HasPerformanceRisk, bool HasEngagementRisk)
{
    public bool IsAtRisk => HasPerformanceRisk || HasEngagementRisk;
}

/// <summary>
/// Pure evaluation of launchpad-build-guide.md §5.4's "sponsor auto-flag" rule: a
/// candidate crossing into risk should produce one notification/audit entry per
/// flagged episode, not a fresh one every time a recurring job re-observes the same
/// still-true flag. Takes the risk state already computed by vCandidateRisk plus
/// which candidates were already flagged as of the last evaluation, and returns only
/// the state transitions (new flags, recoveries) — no DB access, so this is testable
/// without a database per the layering rule in CLAUDE.md.
/// </summary>
public static class SponsorAutoFlagRule
{
    /// <summary>Candidates now at risk who weren't already flagged — these should get
    /// a fresh AuditEvent + notification.</summary>
    public static IReadOnlyList<CandidateRiskSnapshot> EvaluateNewlyFlagged(
        IEnumerable<CandidateRiskSnapshot> currentRisk,
        IReadOnlySet<int> alreadyFlaggedCandidateIds) =>
        currentRisk
            .Where(r => r.IsAtRisk && !alreadyFlaggedCandidateIds.Contains(r.CandidateId))
            .ToArray();

    /// <summary>Previously-flagged candidates no longer at risk — clearing their flag
    /// lets a later relapse be treated as newly-flagged again instead of being
    /// silently suppressed forever.</summary>
    public static IReadOnlyList<int> EvaluateRecovered(
        IReadOnlySet<int> currentlyAtRiskCandidateIds,
        IReadOnlySet<int> alreadyFlaggedCandidateIds) =>
        alreadyFlaggedCandidateIds
            .Where(id => !currentlyAtRiskCandidateIds.Contains(id))
            .ToArray();
}
