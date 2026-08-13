using FluentAssertions;
using LaunchPad.Application.Risk;
using Xunit;

namespace LaunchPad.Application.Tests.Risk;

public class SponsorAutoFlagRuleTests
{
    [Fact]
    public void EvaluateNewlyFlagged_ReturnsAtRiskCandidatesNotAlreadyFlagged()
    {
        var currentRisk = new[]
        {
            new CandidateRiskSnapshot(1, HasPerformanceRisk: true, HasEngagementRisk: false),
            new CandidateRiskSnapshot(2, HasPerformanceRisk: false, HasEngagementRisk: true),
            new CandidateRiskSnapshot(3, HasPerformanceRisk: false, HasEngagementRisk: false),
        };
        var alreadyFlagged = new HashSet<int> { 1 };

        var result = SponsorAutoFlagRule.EvaluateNewlyFlagged(currentRisk, alreadyFlagged);

        result.Should().ContainSingle(r => r.CandidateId == 2);
    }

    [Fact]
    public void EvaluateNewlyFlagged_ExcludesCandidatesWithNoRiskFlags()
    {
        var currentRisk = new[] { new CandidateRiskSnapshot(1, HasPerformanceRisk: false, HasEngagementRisk: false) };

        var result = SponsorAutoFlagRule.EvaluateNewlyFlagged(currentRisk, new HashSet<int>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateNewlyFlagged_DoesNotReFlagACandidateAlreadyFlagged()
    {
        var currentRisk = new[] { new CandidateRiskSnapshot(1, HasPerformanceRisk: true, HasEngagementRisk: false) };
        var alreadyFlagged = new HashSet<int> { 1 };

        var result = SponsorAutoFlagRule.EvaluateNewlyFlagged(currentRisk, alreadyFlagged);

        result.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateRecovered_ReturnsPreviouslyFlaggedCandidatesNoLongerAtRisk()
    {
        var alreadyFlagged = new HashSet<int> { 1, 2, 3 };
        var currentlyAtRisk = new HashSet<int> { 2 };

        var result = SponsorAutoFlagRule.EvaluateRecovered(currentlyAtRisk, alreadyFlagged);

        result.Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public void EvaluateRecovered_ReturnsEmpty_WhenAllFlaggedCandidatesAreStillAtRisk()
    {
        var alreadyFlagged = new HashSet<int> { 1, 2 };
        var currentlyAtRisk = new HashSet<int> { 1, 2 };

        var result = SponsorAutoFlagRule.EvaluateRecovered(currentlyAtRisk, alreadyFlagged);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ARelapsedCandidate_IsTreatedAsNewlyFlaggedAgain_AfterRecoveryClearsTheirFlag()
    {
        // The scenario EvaluateRecovered exists for: without clearing the flag on
        // recovery, a candidate who dips into risk, recovers, then relapses would
        // never be re-flagged because "alreadyFlagged" would still contain them.
        var alreadyFlagged = new HashSet<int> { 1 };
        var stillAtRiskIds = new HashSet<int>();

        var recovered = SponsorAutoFlagRule.EvaluateRecovered(stillAtRiskIds, alreadyFlagged);
        var flagStateAfterRecovery = new HashSet<int>(alreadyFlagged.Except(recovered));

        var relapseRisk = new[] { new CandidateRiskSnapshot(1, HasPerformanceRisk: true, HasEngagementRisk: false) };
        var newlyFlaggedOnRelapse = SponsorAutoFlagRule.EvaluateNewlyFlagged(relapseRisk, flagStateAfterRecovery);

        newlyFlaggedOnRelapse.Should().ContainSingle(r => r.CandidateId == 1);
    }
}
