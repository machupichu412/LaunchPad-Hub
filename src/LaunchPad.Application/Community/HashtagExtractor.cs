using System.Text.RegularExpressions;

namespace LaunchPad.Application.Community;

/// <summary>
/// Extracts hashtags from a post body for normalized, indexed storage (see Hashtag/
/// CommunityPostHashtag) — pure, DB-free, per CLAUDE.md's layering rule, same shape as
/// FolderNameSanitizer. Display casing is intentionally discarded here: the frontend
/// re-highlights hashtags from the original Body text, so this only needs to produce the
/// canonical lowercase form used for lookups/filtering.
/// </summary>
public static class HashtagExtractor
{
    // A '#' not immediately preceded by a word character or another '#' (so "word#tag" and
    // "##tag" don't count), followed by 1-50 word characters (matches Hashtag.Tag's column
    // width). A tag must contain at least one letter — "#2026" or "#___" aren't meaningful.
    private static readonly Regex Pattern = new(@"(?<![\w#])#([A-Za-z0-9_]{1,50})", RegexOptions.Compiled);

    public static IReadOnlyList<string> Extract(string? body)
    {
        if (string.IsNullOrEmpty(body)) return Array.Empty<string>();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (Match match in Pattern.Matches(body))
        {
            var raw = match.Groups[1].Value;
            if (!raw.Any(char.IsLetter)) continue;

            var canonical = raw.ToLowerInvariant();
            if (seen.Add(canonical)) result.Add(canonical);
        }

        return result;
    }
}
