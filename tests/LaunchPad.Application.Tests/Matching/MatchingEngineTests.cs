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
    public void RankTopMatches_MissingAllRequiredSkills_StillRanked_ButScoresMuchLower()
    {
        // Required skills are a heavily-weighted signal now, not a hard gate — a candidate
        // missing all of them still appears, just scored far below one who has them.
        var project = new MatchProject(1, Availability.FullTime, new[] { (SkillId: 1, IsRequired: true) });
        var missingRequired = new MatchCandidate(1, Availability.FullTime, new[] { 99 });
        var hasRequired = new MatchCandidate(2, Availability.FullTime, new[] { 1 });

        var results = _sut.RankTopMatches(project, new[] { missingRequired, hasRequired });

        results.Should().HaveCount(2);
        results.First(r => r.CandidateId == 1).Score.Should().BeLessThan(results.First(r => r.CandidateId == 2).Score);
    }

    [Fact]
    public void RankTopMatches_PartialRequiredSkillMatch_ScoresBetweenZeroAndFullMatch()
    {
        // Squared-ratio scoring: 1 of 2 required matched sits strictly between 0 of 2 and
        // 2 of 2 — no more pass/fail cliff at 100% required coverage.
        var project = new MatchProject(1, Availability.FullTime, new[] { (1, true), (2, true) });
        var none = new MatchCandidate(1, Availability.FullTime, Array.Empty<int>());
        var partial = new MatchCandidate(2, Availability.FullTime, new[] { 1 });
        var full = new MatchCandidate(3, Availability.FullTime, new[] { 1, 2 });

        var results = _sut.RankTopMatches(project, new[] { none, partial, full });

        var noneScore = results.First(r => r.CandidateId == 1).Score;
        var partialScore = results.First(r => r.CandidateId == 2).Score;
        var fullScore = results.First(r => r.CandidateId == 3).Score;

        partialScore.Should().BeGreaterThan(noneScore);
        partialScore.Should().BeLessThan(fullScore);
    }

    [Fact]
    public void RankTopMatches_RequiredSkillsWeighDominantlyOverPreferred()
    {
        // All-required-no-preferred should still clearly outrank all-preferred-no-required —
        // required carries roughly 3x preferred's weight in the composite.
        var project = new MatchProject(1, Availability.FullTime, new[] { (1, true), (2, false) });
        var allRequired = new MatchCandidate(1, Availability.FullTime, new[] { 1 });
        var allPreferred = new MatchCandidate(2, Availability.FullTime, new[] { 2 });

        var results = _sut.RankTopMatches(project, new[] { allRequired, allPreferred });

        results.First(r => r.CandidateId == 1).Score.Should().BeGreaterThan(results.First(r => r.CandidateId == 2).Score);
    }

    [Fact]
    public void RankTopMatches_TextSimilarityScore_FoldsIntoCompositeAsSmallBonus()
    {
        // Identical candidates apart from a precomputed text-similarity score — the one
        // with a higher score should rank higher, but the effect stays bounded (small weight).
        var project = new MatchProject(1, Availability.FullTime, new[] { (1, true) });
        var noSimilarity = new MatchCandidate(1, Availability.FullTime, new[] { 1 }, TextSimilarityScore: null);
        var highSimilarity = new MatchCandidate(2, Availability.FullTime, new[] { 1 }, TextSimilarityScore: 1.0m);

        var results = _sut.RankTopMatches(project, new[] { noSimilarity, highSimilarity });

        var lowScore = results.First(r => r.CandidateId == 1).Score;
        var highScore = results.First(r => r.CandidateId == 2).Score;

        highScore.Should().BeGreaterThan(lowScore);
        (highScore - lowScore).Should().BeLessThan(15m);
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
        // A project that lists zero preferred skills shouldn't score its candidates as if
        // they're missing every preferred skill — same candidate, same required-skill
        // match, but one project never asked for anything preferred while the other did
        // and the candidate lacks it.
        var projectWithNoPreferred = new MatchProject(1, Availability.FullTime, new[] { (1, true) });
        var projectRequestingPreferred = new MatchProject(2, Availability.FullTime, new[] { (1, true), (2, false) });
        var candidate = new MatchCandidate(1, Availability.FullTime, new[] { 1 });

        var noPreferredResult = _sut.RankTopMatches(projectWithNoPreferred, new[] { candidate }).Single();
        var missingPreferredResult = _sut.RankTopMatches(projectRequestingPreferred, new[] { candidate }).Single();

        noPreferredResult.Score.Should().BeGreaterThan(missingPreferredResult.Score);
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
