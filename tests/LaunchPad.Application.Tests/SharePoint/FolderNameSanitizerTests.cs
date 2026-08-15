using FluentAssertions;
using LaunchPad.Application.SharePoint;
using Xunit;

namespace LaunchPad.Application.Tests.SharePoint;

public class FolderNameSanitizerTests
{
    [Theory]
    [InlineData("Acme Corp", "Acme Corp")]
    [InlineData("LP-2027-Spring", "LP-2027-Spring")]
    [InlineData("Wireframes: v1/v2", "Wireframes- v1-v2")]
    [InlineData("Path\\To*Thing?", "Path-To-Thing-")]
    [InlineData("\"Quoted\" <Name>", "-Quoted- -Name-")]
    [InlineData("Pipe|Value", "Pipe-Value")]
    public void Sanitize_ReplacesEveryIllegalCharacterWithADash(string input, string expected)
    {
        FolderNameSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("  Padded Name  ", "Padded Name")]
    [InlineData("Trailing.Period.", "Trailing.Period")]
    [InlineData("...", "Untitled")]
    public void Sanitize_TrimsWhitespaceAndTrailingPeriods(string input, string expected)
    {
        FolderNameSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_FallsBackToUntitled_WhenNothingSurvives(string input)
    {
        FolderNameSanitizer.Sanitize(input).Should().Be("Untitled");
    }

    [Fact]
    public void Sanitize_CapsLengthAt255Characters()
    {
        var longName = new string('a', 300);

        var result = FolderNameSanitizer.Sanitize(longName);

        result.Length.Should().Be(255);
    }

    [Fact]
    public void Sanitize_TrimsTrailingWhitespaceIntroducedByTruncation()
    {
        var longName = new string('a', 254) + " " + new string('b', 50);

        var result = FolderNameSanitizer.Sanitize(longName);

        result.Should().NotEndWith(" ");
        result.Length.Should().BeLessThanOrEqualTo(255);
    }
}
