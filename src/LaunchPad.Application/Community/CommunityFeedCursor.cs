using System.Globalization;
using System.Text;

namespace LaunchPad.Application.Community;

/// <summary>
/// Opaque keyset-pagination cursor for the community feed — encodes the last item's
/// (CreatedUtc, CommunityPostId) so the next page can resume with a stable, tie-break-safe
/// WHERE clause instead of an OFFSET that gets slower as the feed grows. Pure, DB-free, per
/// CLAUDE.md's layering rule — same shape as FolderNameSanitizer.
/// </summary>
public static class CommunityFeedCursor
{
    private const string Separator = "_";

    public static string Encode(DateTime createdUtc, int postId)
    {
        var raw = $"{createdUtc.Ticks}{Separator}{postId}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>False on any malformed/tampered input — callers should treat that as
    /// "start from the top of the feed" rather than a hard error, since a client-side
    /// bug shouldn't be able to break the feed for a user.</summary>
    public static bool TryDecode(string? cursor, out DateTime createdUtc, out int postId)
    {
        createdUtc = default;
        postId = default;

        if (string.IsNullOrWhiteSpace(cursor)) return false;

        string raw;
        try
        {
            raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        }
        catch (FormatException)
        {
            return false;
        }

        var parts = raw.Split(Separator);
        if (parts.Length != 2) return false;

        if (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)) return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id)) return false;
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) return false;

        createdUtc = new DateTime(ticks, DateTimeKind.Utc);
        postId = id;
        return true;
    }
}
