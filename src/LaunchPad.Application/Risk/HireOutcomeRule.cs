namespace LaunchPad.Application.Risk;

public enum SuggestedHireOutcome
{
    NoHire,
    Hire,
    TalentPlus,
}

/// <summary>Input to HireOutcomeRule.Evaluate. FinalScore/HasPerformanceRisk/HasEngagementRisk
/// come from dbo.vCandidateRisk (via CandidateRisk — never re-derived here, same "computed
/// view is the source of truth" rule SponsorAutoFlagRule follows). LatestRecommendConversion
/// is the most recent Final-checkpoint SponsorOnCandidate review's RecommendConversion flag
/// (see IReviewRepository.GetLatestFinalRecommendConversionAsync) — null means either no
/// Final review exists yet, or the sponsor left the checkbox unspecified; both are treated
/// the same way (nothing to suggest from).</summary>
public readonly record struct HireOutcomeSignal(
    int CandidateId,
    decimal? FinalScore,
    bool? LatestRecommendConversion,
    bool HasPerformanceRisk,
    bool HasEngagementRisk);

/// <summary>
/// Pure evaluation of "does a Final review's data support a hire-outcome suggestion" — same
/// shape as SponsorAutoFlagRule (a record-in-enum-out function, no DB access, testable
/// without a database per CLAUDE.md's layering rule). This is a suggestion only: Program Ops
/// applies or overrides it via CandidatesController.UpdateStatus. No CandidateStatus is ever
/// written from this rule directly — see the two-stage SponsorRecommend/OpsApprove pattern
/// this mirrors, keeping a human gate on hire-adjacent HR data.
/// </summary>
public static class HireOutcomeRule
{
    private const decimal NoHireScoreThreshold = 2.5m;
    private const decimal TalentPlusScoreThreshold = 4.5m;

    /// <summary>Null means "nothing to suggest yet" — no Final review, or an unrated one.</summary>
    public static SuggestedHireOutcome? Evaluate(HireOutcomeSignal signal)
    {
        if (signal.FinalScore is not decimal finalScore || signal.LatestRecommendConversion is not bool recommend)
        {
            return null;
        }

        if (!recommend || finalScore < NoHireScoreThreshold || signal.HasPerformanceRisk)
        {
            return SuggestedHireOutcome.NoHire;
        }

        if (finalScore >= TalentPlusScoreThreshold && !signal.HasEngagementRisk)
        {
            return SuggestedHireOutcome.TalentPlus;
        }

        return SuggestedHireOutcome.Hire;
    }
}
