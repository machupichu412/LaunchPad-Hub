using FluentAssertions;
using LaunchPad.Application.Risk;
using Xunit;

namespace LaunchPad.Application.Tests.Risk;

public class HireOutcomeRuleTests
{
    [Fact]
    public void Evaluate_NoFinalScore_ReturnsNull()
    {
        var signal = new HireOutcomeSignal(1, FinalScore: null, LatestRecommendConversion: true, HasPerformanceRisk: false, HasEngagementRisk: false);

        HireOutcomeRule.Evaluate(signal).Should().BeNull();
    }

    [Fact]
    public void Evaluate_FinalScoreButNoRecommendConversion_ReturnsNull()
    {
        // A Final review exists but the sponsor left the checkbox unspecified — nothing to
        // suggest from, same as no review at all.
        var signal = new HireOutcomeSignal(1, FinalScore: 4.0m, LatestRecommendConversion: null, HasPerformanceRisk: false, HasEngagementRisk: false);

        HireOutcomeRule.Evaluate(signal).Should().BeNull();
    }

    [Fact]
    public void Evaluate_NotRecommended_SuggestsNoHire_EvenWithAHighScore()
    {
        var signal = new HireOutcomeSignal(1, FinalScore: 5.0m, LatestRecommendConversion: false, HasPerformanceRisk: false, HasEngagementRisk: false);

        HireOutcomeRule.Evaluate(signal).Should().Be(SuggestedHireOutcome.NoHire);
    }

    [Fact]
    public void Evaluate_RecommendedButLowScore_SuggestsNoHire()
    {
        var signal = new HireOutcomeSignal(1, FinalScore: 2.0m, LatestRecommendConversion: true, HasPerformanceRisk: false, HasEngagementRisk: false);

        HireOutcomeRule.Evaluate(signal).Should().Be(SuggestedHireOutcome.NoHire);
    }

    [Fact]
    public void Evaluate_RecommendedWithGoodScoreButPerformanceRisk_SuggestsNoHire()
    {
        // A recovering-but-still-flagged trajectory shouldn't get waved through on the
        // strength of the sponsor's checkbox alone.
        var signal = new HireOutcomeSignal(1, FinalScore: 4.0m, LatestRecommendConversion: true, HasPerformanceRisk: true, HasEngagementRisk: false);

        HireOutcomeRule.Evaluate(signal).Should().Be(SuggestedHireOutcome.NoHire);
    }

    [Fact]
    public void Evaluate_RecommendedWithSolidScore_SuggestsHire()
    {
        var signal = new HireOutcomeSignal(1, FinalScore: 3.5m, LatestRecommendConversion: true, HasPerformanceRisk: false, HasEngagementRisk: false);

        HireOutcomeRule.Evaluate(signal).Should().Be(SuggestedHireOutcome.Hire);
    }

    [Fact]
    public void Evaluate_RecommendedWithExceptionalScoreAndNoEngagementRisk_SuggestsTalentPlus()
    {
        var signal = new HireOutcomeSignal(1, FinalScore: 4.8m, LatestRecommendConversion: true, HasPerformanceRisk: false, HasEngagementRisk: false);

        HireOutcomeRule.Evaluate(signal).Should().Be(SuggestedHireOutcome.TalentPlus);
    }

    [Fact]
    public void Evaluate_ExceptionalScoreButEngagementRisk_SuggestsHire_NotTalentPlus()
    {
        // Great sponsor rating, but flaky engagement — still a Hire, just not the top tier.
        var signal = new HireOutcomeSignal(1, FinalScore: 4.8m, LatestRecommendConversion: true, HasPerformanceRisk: false, HasEngagementRisk: true);

        HireOutcomeRule.Evaluate(signal).Should().Be(SuggestedHireOutcome.Hire);
    }
}
