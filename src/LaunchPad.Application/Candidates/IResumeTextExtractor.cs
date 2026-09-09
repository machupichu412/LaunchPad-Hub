namespace LaunchPad.Application.Candidates;

/// <summary>
/// Turns an uploaded resume file into plain text.
///
/// A separate step from IResumeExtractionClient on purpose: a language model cannot read PDF
/// bytes, and this half is deterministic, offline, and cheap to test against fixture files.
/// Keeping it apart also means a parse that fails here never spends a model call, and the
/// failure it reports ("we couldn't read any text") is materially different from the one the
/// model reports ("no skills found").
/// </summary>
public interface IResumeTextExtractor
{
    /// <param name="content">The uploaded file. Read from the current position.</param>
    /// <param name="contentType">
    /// The declared MIME type. A hint only — implementations are expected to identify the
    /// format from the bytes, since this value ultimately comes from the client.
    /// </param>
    /// <returns>
    /// The extracted text, or null when the file yields nothing usable — an image-only
    /// (scanned) PDF, an unsupported format, or a corrupt file. Null must surface to the
    /// candidate as a real failure with an explanation, never as an empty skill list: telling
    /// someone their resume contained no skills when it was simply unreadable is the worst
    /// outcome this feature has.
    /// </returns>
    Task<string?> ExtractTextAsync(Stream content, string contentType, CancellationToken ct = default);
}
