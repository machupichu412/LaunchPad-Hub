using FluentAssertions;
using LaunchPad.Application.Matching;
using LaunchPad.Domain.Enums;
using Xunit;

namespace LaunchPad.Application.Tests.Matching;

public class MatchingEngineTests
{
    private readonly MatchingEngine _sut = new();

    [Fact]
    public void RankTopMatches_ExcludesCandidatesWithMismatchedAvailability()
    {
        var project = new MatchProject(1, Availability.FullTime, Array.Empty<(int, bool)>());
        var candidates = new[] { new MatchCandidate(1, Availability.PartTime, Array.Empty<int>()) };

        var results = _sut.RankTopMatches(project, candidates);

        results.Should().BeEmpty();
    }

    [Fact]
    public void RankTopMatches_ExcludesCandidatesMissingAllRequiredSkills()
    {
        var project = new MatchProject(1, Availability.FullTime, new[] { (SkillId: 1, IsRequired: true) });
        var candidates = new[] { new MatchCandidate(1, Availability.FullTime, new[] { 99 }) };

        var results = _sut.RankTopMatches(project, candidates);

        results.Should().BeEmpty();
    }

    [Fact]
    public void RankTopMatches_RanksFullSkillMatchAboveGap()
    {
        var project = new MatchProject(1, Availability.FullTime, new[] { (1, true), (2, true) });
        var candidates = new[]
        {
            new MatchCandidate(1, Availability.FullTime, new[] { 1 }),
            new MatchCandidate(2, Availability.FullTime, new[] { 1, 2 })
        };

        var results = _sut.RankTopMatches(project, candidates);

        results.Should().HaveCount(2);
        results[0].CandidateId.Should().Be(2);
        results[0].Score.Should().BeGreaterThan(results[1].Score);
    }

    [Fact]
    public void RankTopMatches_RespectsTopNLimit()
    {
        var project = new MatchProject(1, Availability.FullTime, Array.Empty<(int, bool)>());
        var candidates = Enumerable.Range(1, 5)
            .Select(id => new MatchCandidate(id, Availability.FullTime, Array.Empty<int>()))
            .ToArray();

        var results = _sut.RankTopMatches(project, candidates, topN: 3);

        results.Should().HaveCount(3);
    }
}
