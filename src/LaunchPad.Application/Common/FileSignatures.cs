namespace LaunchPad.Application.Common;

/// <summary>
/// What a file actually is, as opposed to what its name and Content-Type claim. Both of those
/// come from whoever is uploading, so neither is evidence: an .exe renamed to .pdf and sent as
/// application/pdf passed every check the app had. Everything stored here is served back to
/// other people, so the bytes have to agree with the label.
/// </summary>
public static class FileSignatures
{
    /// <summary>Enough bytes for every signature below (the longest check reads 12).</summary>
    public const int HeaderBytes = 16;

    /// <summary>What a candidate may attach to a deliverable: documents, plain text, images,
    /// and zip archives. Anything executable or scriptable is absent on purpose.</summary>
    public static readonly IReadOnlySet<string> AllowedDeliverableExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".md", ".csv", ".rtf",
            ".png", ".jpg", ".jpeg", ".gif", ".webp",
            ".zip",
        };

    private static readonly byte[] Pdf = "%PDF"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif87 = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89 = "GIF89a"u8.ToArray();
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();
    private static readonly byte[] Zip = [0x50, 0x4B];                                             // also docx/xlsx/pptx
    private static readonly byte[] OleCompound = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]; // legacy .doc/.xls/.ppt
    private static readonly byte[] Rtf = "{\\rtf"u8.ToArray();

    /// <summary>True when the header looks like the type the extension claims. Formats with no
    /// signature at all (plain text, CSV, Markdown) can only be checked for not being one of the
    /// binary types above, which is what rejects a renamed executable.</summary>
    public static bool MatchesExtension(ReadOnlySpan<byte> header, string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => Starts(header, Pdf),
        ".png" => Starts(header, Png),
        ".jpg" or ".jpeg" => Starts(header, Jpeg),
        ".gif" => Starts(header, Gif87) || Starts(header, Gif89),
        ".webp" => Starts(header, Riff) && header.Length >= 12 && header[8..12].SequenceEqual(Webp),
        ".docx" or ".xlsx" or ".pptx" or ".zip" => Starts(header, Zip),
        ".doc" or ".xls" or ".ppt" => Starts(header, OleCompound) || Starts(header, Zip),
        ".rtf" => Starts(header, Rtf),
        ".txt" or ".md" or ".csv" => !LooksBinary(header),
        _ => false,
    };

    /// <summary>The same check for an image that arrives as a bare body with a Content-Type
    /// instead of a filename (avatars, community post images).</summary>
    public static bool MatchesImageContentType(ReadOnlySpan<byte> header, string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => MatchesExtension(header, ".png"),
        "image/jpeg" or "image/jpg" => MatchesExtension(header, ".jpg"),
        "image/gif" => MatchesExtension(header, ".gif"),
        "image/webp" => MatchesExtension(header, ".webp"),
        _ => false,
    };

    public static bool IsAllowedDeliverableFileName(string fileName) =>
        AllowedDeliverableExtensions.Contains(Path.GetExtension(fileName));

    private static bool Starts(ReadOnlySpan<byte> header, ReadOnlySpan<byte> signature) =>
        header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature);

    /// <summary>A NUL byte this early means the file isn't text — which is what PE, ELF,
    /// Mach-O and the legacy Office binaries all trip over.</summary>
    private static bool LooksBinary(ReadOnlySpan<byte> header) => header.Contains((byte)0);
}
