using FluentAssertions;
using LaunchPad.Application.Community;
using Xunit;

namespace LaunchPad.Application.Tests.Community;

public class CommunityFeedCursorTests
{
    [Fact]
    public void EncodeThenDecode_RoundTripsExactly()
    {
        var createdUtc = new DateTime(2026, 8, 15, 12, 30, 0, DateTimeKind.Utc);
        var cursor = CommunityFeedCursor.Encode(createdUtc, postId: 4821);

        var decoded = CommunityFeedCursor.TryDecode(cursor, out var decodedCreatedUtc, out var decodedPostId);

        decoded.Should().BeTrue();
        decodedCreatedUtc.Should().Be(createdUtc);
        decodedPostId.Should().Be(4821);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64!!!")]
    [InlineData("dGhpcyBoYXMgbm8gc2VwYXJhdG9y")] // base64 of "this has no separator"
    public void TryDecode_ReturnsFalse_ForMalformedInput(string? garbage)
    {
        var decoded = CommunityFeedCursor.TryDecode(garbage, out _, out _);

        decoded.Should().BeFalse();
    }

    [Fact]
    public void TryDecode_ReturnsFalse_WhenPostIdIsNotNumeric()
    {
        var raw = System.Text.Encoding.UTF8.GetBytes("12345_notanumber");
        var cursor = Convert.ToBase64String(raw);

        CommunityFeedCursor.TryDecode(cursor, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Encode_ProducesDifferentCursors_ForDifferentPosts()
    {
        var createdUtc = DateTime.UtcNow;
        var cursorA = CommunityFeedCursor.Encode(createdUtc, 1);
        var cursorB = CommunityFeedCursor.Encode(createdUtc, 2);

        cursorA.Should().NotBe(cursorB);
    }
}
