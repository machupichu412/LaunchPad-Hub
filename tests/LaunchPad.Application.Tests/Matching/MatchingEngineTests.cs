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
    public void RankTopMatches_ExcludesCandidatesWithOnlyAPartialRequiredSkillMatch()
    {
        // Matching Logic Doc: required skills are pass/fail at 100%, not "at least one" —
        // this is the fix to the pre-existing engine, which only excluded a candidate
        // with zero required skills matched.
        var project = new MatchProject(1, Availability.FullTime, new[] { (1, true), (2, true) });
        var candidates = new[] { new MatchCandidate(1, Availability.FullTime, new[] { 1 }) }; // has 1 of 2

        var results = _sut.RankTopMatches(project, candidates);

        results.Should().BeEmpty();
    }

    [Fact]
    public void RankTopMatches_RanksFullPreferredSkillMatchAboveGap()
    {
        var project = new MatchProject(1, Availability.FullTime, new[] { (1, true), (2, false) });
        var candidates = new[]
        {
            new MatchCandidate(1, Availability.FullTime, new[] { 1 }),      // required only, no preferred
            new MatchCandidate(2, Availability.FullTime, new[] { 1, 2 })    // required + preferred
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

    [Fact]
    public void RankTopMatches_NoPreferredSkillsRequested_GivesFullCreditNotZero()
    {
        // A project that only lists required skills shouldn't cap every candidate's score
        // just because it never asked for anything preferred.
        var project = new MatchProject(1, Availability.FullTime, new[] { (1, true) });
        var candidates = new[] { new MatchCandidate(1, Availability.FullTime, new[] { 1 }) };

        var results = _sut.RankTopMatches(project, candidates);

        results.Should().ContainSingle();
        results[0].Score.Should().Be(100m);
    }

    [Fact]
    public void RankTopMatches_FirstProjectCandidate_ReweightsAwayFromPerformance()
    {
        // No review history (PastPerformanceScore null) -> 95% preferred skills / 5%
        // graduation proximity, per the doc's own override for a candidate's first project.
        var project = new MatchProject(1, Availability.FullTime, new[] { (1, true), (2, false) });
        var noPreferred = new MatchCandidate(1, Availability.FullTime, new[] { 1 }, PastPerformanceScore: null);
        var withPreferred = new MatchCandidate(2, Availability.FullTime, new[] { 1, 2 }, PastPerformanceScore: null);

        var results = _sut.RankTopMatches(project, new[] { noPreferred, withPreferred });

        results.First(r => r.CandidateId == 2).Score.Should().BeGreaterThan(results.First(r => r.CandidateId == 1).Score);
    }

    [Fact]
    public void RankTopMatches_WithReviewHistory_WeighsPastPerformance()
    {
        var project = new MatchProject(1, Availability.FullTime, new[] { (1, true) });
        var strongPerformer = new MatchCandidate(1, Availability.FullTime, new[] { 1 }, PastPerformanceScore: 1.0m);
        var weakPerformer = new MatchCandidate(2, Availability.FullTime, new[] { 1 }, PastPerformanceScore: 0.0m);

        var results = _sut.RankTopMatches(project, new[] { strongPerformer, weakPerformer });

        results.First(r => r.CandidateId == 1).Score.Should().BeGreaterThan(results.First(r => r.CandidateId == 2).Score);
    }

    [Fact]
    public void RankTopMatches_CandidateGraduatingBeforeProjectEnds_ScoresLowerThanOneWhoDoesnt()
    {
        var project = new MatchProject(1, Availability.FullTime, new[] { (1, true) }, EndDate: new DateOnly(2026, 6, 1));
        var graduatesTooSoon = new MatchCandidate(1, Availability.FullTime, new[] { 1 }, GraduationDate: new DateOnly(2026, 1, 1));
        var staysThrough = new MatchCandidate(2, Availability.FullTime, new[] { 1 }, GraduationDate: new DateOnly(2026, 12, 1));

        var results = _sut.RankTopMatches(project, new[] { graduatesTooSoon, staysThrough });

        results.First(r => r.CandidateId == 2).Score.Should().BeGreaterThan(results.First(r => r.CandidateId == 1).Score);
    }

    [Fact]
    public void RankTopMatches_InterestRating_NudgesScoreBySmallAmount_NotAGate()
    {
        // Deliberately weak base match (missed preferred skill) so the +/-2 nudge is
        // visible without hitting the 0-100 clamp.
        var project = new MatchProject(1, Availability.FullTime, new[] { (1, true), (2, false) });
        var highlyInterested = new MatchCandidate(1, Availability.FullTime, new[] { 1 }, InterestRatingsByProjectId: new Dictionary<int, byte> { [1] = 5 });
        var uninterested = new MatchCandidate(2, Availability.FullTime, new[] { 1 }, InterestRatingsByProjectId: new Dictionary<int, byte> { [1] = 1 });
        var neverRated = new MatchCandidate(3, Availability.FullTime, new[] { 1 });

        var results = _sut.RankTopMatches(project, new[] { highlyInterested, uninterested, neverRated });

        var highScore = results.First(r => r.CandidateId == 1).Score;
        var lowScore = results.First(r => r.CandidateId == 2).Score;
        var neutralScore = results.First(r => r.CandidateId == 3).Score;

        (highScore - lowScore).Should().Be(4m); // +2 vs -2
        neutralScore.Should().BeInRange(lowScore, highScore); // unrated sits between, unpenalized
    }
}
