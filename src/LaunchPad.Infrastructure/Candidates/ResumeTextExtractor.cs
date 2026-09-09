using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaunchPad.Application.Candidates;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace LaunchPad.Infrastructure.Candidates;

/// <summary>
/// Extracts plain text from PDF and DOCX resumes locally, using PdfPig and the Open XML SDK.
///
/// Chosen over Azure Document Intelligence: no extra Azure resource, no extra private endpoint,
/// no per-page cost, and — because it is deterministic and offline — the whole thing is testable
/// against fixture files with no cloud account. The tradeoff is no OCR, so scanned resumes are
/// detected and reported rather than read. That is the right trade here: a scanned resume is
/// rare, and telling someone their file was unreadable beats silently parsing nothing.
///
/// Every failure path returns null rather than throwing. Resumes are arbitrary uploaded files,
/// and a malformed one is an ordinary event, not an exceptional one — the parse run records it
/// as a failure with a message the candidate can act on.
/// </summary>
public sealed class ResumeTextExtractor : IResumeTextExtractor
{
    /// <summary>Below this, treat the file as unreadable rather than empty. A scanned PDF
    /// typically yields nothing or a few stray glyphs from a header logo; no genuine resume
    /// comes in under a couple of sentences.</summary>
    private const int MinimumMeaningfulLength = 50;

    /// <summary>Defence in depth behind the upload's own size cap — this class can also be
    /// called with a blob read back from storage, where nothing revalidates the size.</summary>
    private const long MaxBytes = 12 * 1024 * 1024;

    /// <summary>A resume is a handful of pages. Anything beyond this is either not a resume or
    /// an attempt to make parsing expensive; take the leading pages and move on.</summary>
    private const int MaxPages = 50;

    /// <summary>Keeps a pathological file from producing a string that dwarfs the model's
    /// context and the database column behind it. The extraction client trims further.</summary>
    private const int MaxCharacters = 200_000;

    private static readonly Regex HorizontalWhitespace = new(@"[^\S\n]+", RegexOptions.Compiled);
    private static readonly Regex ExcessBlankLines = new(@"\n{3,}", RegexOptions.Compiled);

    private readonly ILogger<ResumeTextExtractor> _logger;

    public ResumeTextExtractor(ILogger<ResumeTextExtractor> logger) => _logger = logger;

    public async Task<string?> ExtractTextAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        // Buffered up front for two reasons: the format is identified from the leading bytes and
        // then the whole stream is re-read, and neither PdfPig nor the Open XML SDK can work
        // with the non-seekable stream a blob download hands back.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        if (buffer.Length == 0)
        {
            _logger.LogWarning("Resume text extraction skipped: the file was empty.");
            return null;
        }

        if (buffer.Length > MaxBytes)
        {
            _logger.LogWarning(
                "Resume text extraction skipped: {ByteCount} bytes exceeds the {MaxBytes}-byte limit.",
                buffer.Length, MaxBytes);
            return null;
        }

        buffer.Position = 0;
        var format = IdentifyFormat(buffer);
        buffer.Position = 0;

        // The declared content type is a hint; the bytes decide. It reaches us from an HTTP
        // client, and this file is about to be parsed and then sent to a language model.
        if (format is ResumeFormat.Unknown)
        {
            _logger.LogWarning(
                "Resume text extraction skipped: unrecognized file signature (declared content type {ContentType}).",
                contentType);
            return null;
        }

        // PdfPig and the Open XML SDK are both synchronous. This runs inside a Function, off the
        // request thread, so it blocks its own worker rather than a request — no Task.Run.
        try
        {
            var raw = format switch
            {
                ResumeFormat.Pdf => ExtractFromPdf(buffer, ct),
                ResumeFormat.Docx => ExtractFromDocx(buffer),
                _ => null,
            };

            var normalized = Normalize(raw);

            if (normalized is null || normalized.Length < MinimumMeaningfulLength)
            {
                _logger.LogWarning(
                    "Resume text extraction produced too little text to use ({CharacterCount} characters from a {Format} file) — likely a scanned or image-only document.",
                    normalized?.Length ?? 0, format);
                return null;
            }

            _logger.LogInformation(
                "Extracted {CharacterCount} characters from a {Format} resume.", normalized.Length, format);
            return normalized;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Deliberately broad. Both libraries throw a wide range of types on malformed,
            // encrypted, or truncated input, and none of them should surface as a 500 — an
            // unreadable upload is an expected outcome of letting people upload files.
            _logger.LogWarning(ex, "Resume text extraction failed for a {Format} file.", format);
            return null;
        }
    }

    private static ResumeFormat IdentifyFormat(Stream stream)
    {
        Span<byte> signature = stackalloc byte[4];
        if (stream.Read(signature) < 4) return ResumeFormat.Unknown;

        // "%PDF"
        if (signature is [0x25, 0x50, 0x44, 0x46]) return ResumeFormat.Pdf;

        // "PK\x03\x04" — a ZIP container. DOCX is one; so are XLSX and PPTX, which the Open XML
        // SDK will reject when it looks for a main document part.
        if (signature is [0x50, 0x4B, 0x03, 0x04]) return ResumeFormat.Docx;

        return ResumeFormat.Unknown;
    }

    private static string ExtractFromPdf(Stream stream, CancellationToken ct)
    {
        using var document = PdfDocument.Open(stream);
        var builder = new StringBuilder();

        var pageNumber = 0;
        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            if (++pageNumber > MaxPages) break;

            string pageText;
            try
            {
                // Reading order matters: resumes are frequently two-column, and raw content-stream
                // order interleaves the columns into unreadable alternating fragments. This
                // reconstructs a sensible order; page.Text is the fallback when layout analysis
                // can't cope with a particular page.
                pageText = ContentOrderTextExtractor.GetText(page);
            }
            catch
            {
                pageText = page.Text;
            }

            builder.Append(pageText).Append('\n');
            if (builder.Length > MaxCharacters) break;
        }

        return builder.ToString();
    }

    private static string ExtractFromDocx(Stream stream)
    {
        using var document = WordprocessingDocument.Open(stream, isEditable: false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;

        // Per paragraph rather than Body.InnerText: InnerText concatenates every run with no
        // separator at all, so headings and bullet points run together into single words. Table
        // cells contain paragraphs too, so this picks up skills matrices laid out as tables.
        var builder = new StringBuilder();
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var text = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.Append(text).Append('\n');
            }

            if (builder.Length > MaxCharacters) break;
        }

        return builder.ToString();
    }

    /// <summary>Collapses the ragged whitespace both extractors produce while keeping paragraph
    /// breaks, which carry the section structure a resume depends on.</summary>
    private static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var text = raw.Replace("\r\n", "\n").Replace('\r', '\n');
        text = HorizontalWhitespace.Replace(text, " ");
        text = string.Join('\n', text.Split('\n').Select(line => line.Trim()));
        text = ExcessBlankLines.Replace(text, "\n\n");
        text = text.Trim();

        if (text.Length > MaxCharacters)
        {
            text = text[..MaxCharacters];
        }

        return text.Length == 0 ? null : text;
    }

    private enum ResumeFormat
    {
        Unknown,
        Pdf,
        Docx,
    }
}
