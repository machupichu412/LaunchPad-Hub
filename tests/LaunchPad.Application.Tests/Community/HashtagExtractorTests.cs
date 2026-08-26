using FluentAssertions;
using LaunchPad.Application.Community;
using Xunit;

namespace LaunchPad.Application.Tests.Community;

public class HashtagExtractorTests
{
    [Fact]
    public void Extract_ReturnsSingleTag_Lowercased()
    {
        HashtagExtractor.Extract("Shipped the widget! #ShipIt").Should().Equal("shipit");
    }

    [Fact]
    public void Extract_ReturnsMultipleTagsInOrder()
    {
        HashtagExtractor.Extract("#win and also #teamwork today").Should().Equal("win", "teamwork");
    }

    [Fact]
    public void Extract_DeduplicatesCaseInsensitively_KeepingFirstOccurrence()
    {
        HashtagExtractor.Extract("#Win great #win #WIN").Should().Equal("win");
    }

    [Fact]
    public void Extract_RejectsDigitOnlyTokens()
    {
        HashtagExtractor.Extract("Cohort #2026 kicks off").Should().BeEmpty();
    }

    [Fact]
    public void Extract_RejectsUnderscoreOnlyTokens()
    {
        HashtagExtractor.Extract("weird tag #___ here").Should().BeEmpty();
    }

    [Fact]
    public void Extract_StopsAtPunctuationAdjacentToTag()
    {
        HashtagExtractor.Extract("great work! #win.").Should().Equal("win");
    }

    [Fact]
    public void Extract_RejectsDoubleHashPrefix()
    {
        HashtagExtractor.Extract("##doubled should not count").Should().BeEmpty();
    }

    [Fact]
    public void Extract_RejectsHashImmediatelyAfterAWordCharacter()
    {
        // "domain#tag" — the '#' is glued to a word, not a standalone hashtag.
        HashtagExtractor.Extract("visit example.com/user#tag").Should().BeEmpty();
    }

    [Fact]
    public void Extract_TruncatesAt50Characters()
    {
        var longTag = new string('a', 60);
        var result = HashtagExtractor.Extract($"#{longTag}");

        result.Should().ContainSingle();
        result[0].Length.Should().Be(50);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no hashtags in this body at all")]
    public void Extract_ReturnsEmpty_WhenNoTagsPresent(string body)
    {
        HashtagExtractor.Extract(body).Should().BeEmpty();
    }

    [Fact]
    public void Extract_ReturnsEmpty_ForNullBody()
    {
        HashtagExtractor.Extract(null).Should().BeEmpty();
    }
}
