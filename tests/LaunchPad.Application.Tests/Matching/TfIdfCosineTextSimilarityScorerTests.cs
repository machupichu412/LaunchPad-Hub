using FluentAssertions;
using LaunchPad.Application.Matching;
using Xunit;

namespace LaunchPad.Application.Tests.Matching;

public class TfIdfCosineTextSimilarityScorerTests
{
    private readonly TfIdfCosineTextSimilarityScorer _sut = new();

    [Fact]
    public void CosineSimilarity_IdenticalText_IsHigh()
    {
        var corpus = new[]
        {
            "Build a real-time analytics dashboard using React and Power BI",
            "Design a mobile-first checkout experience for the storefront",
        };
        var index = _sut.Prepare(corpus);

        var similarity = index.CosineSimilarity(corpus[0], corpus[0]);

        similarity.Should().BeGreaterThan(0.9m);
    }

    [Fact]
    public void CosineSimilarity_DisjointVocabulary_IsZero()
    {
        var corpus = new[]
        {
            "Build a real-time analytics dashboard using React and Power BI",
            "Design a mobile-first checkout experience for the storefront",
        };
        var index = _sut.Prepare(corpus);

        var similarity = index.CosineSimilarity(corpus[0], corpus[1]);

        similarity.Should().Be(0m);
    }

    [Fact]
    public void CosineSimilarity_SharedRareTermOutweighsSharedCommonTerm()
    {
        // "the"/"and" appear in nearly every document (low IDF weight); "kubernetes" is
        // rare (high IDF weight) — a pair sharing only the rare term should score higher
        // than a pair sharing only common terms, proving IDF weighting is actually doing
        // something rather than falling back to plain term-frequency overlap.
        var corpus = new[]
        {
            "the project and the sponsor and the candidate",
            "the project and the timeline and the budget",
            "migrate legacy services onto kubernetes clusters",
            "operate and scale kubernetes clusters reliably",
        };
        var index = _sut.Prepare(corpus);

        var commonTermPairSimilarity = index.CosineSimilarity(corpus[0], corpus[1]);
        var rareTermPairSimilarity = index.CosineSimilarity(corpus[2], corpus[3]);

        rareTermPairSimilarity.Should().BeGreaterThan(commonTermPairSimilarity);
    }

    [Theory]
    [InlineData(null, "some project description")]
    [InlineData("some candidate bio", null)]
    [InlineData("", "some project description")]
    [InlineData(null, null)]
    public void CosineSimilarity_MissingText_IsZero(string? textA, string? textB)
    {
        var index = _sut.Prepare(new[] { "some project description", "some candidate bio" });

        var similarity = index.CosineSimilarity(textA, textB);

        similarity.Should().Be(0m);
    }

    [Fact]
    public void Prepare_EmptyCorpus_NeverThrows_AndAlwaysScoresZero()
    {
        var index = _sut.Prepare(Array.Empty<string>());

        var similarity = index.CosineSimilarity("some text", "some text");

        similarity.Should().Be(0m);
    }
}
