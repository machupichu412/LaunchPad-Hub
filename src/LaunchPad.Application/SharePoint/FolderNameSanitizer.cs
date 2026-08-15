using System.Text;

namespace LaunchPad.Application.SharePoint;

/// <summary>
/// Turns a cohort/candidate/project name into a safe path segment — shared by the real
/// Graph implementation (SharePoint forbids `" * : &lt; &gt; ? / \ |`, plus leading/trailing
/// spaces/periods) and the local-disk fallback (the same characters are also illegal in
/// Windows file names, so one sanitizer covers both). The one genuinely pure, unit-testable
/// piece of this feature — no Graph/EF/IO involved.
/// </summary>
public static class FolderNameSanitizer
{
    private static readonly char[] IllegalCharacters = { '"', '*', ':', '<', '>', '?', '/', '\\', '|' };
    private const int MaxSegmentLength = 255;

    public static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(IllegalCharacters.Contains(c) ? '-' : c);
        }

        var sanitized = builder.ToString().Trim().Trim('.');
        if (sanitized.Length == 0)
        {
            sanitized = "Untitled";
        }

        return sanitized.Length > MaxSegmentLength ? sanitized[..MaxSegmentLength].TrimEnd() : sanitized;
    }
}
