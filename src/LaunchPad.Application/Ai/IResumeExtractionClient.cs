namespace LaunchPad.Application.Ai;

/// <summary>
/// Turns already-extracted resume plain text into structured facts.
///
/// Deliberately narrow rather than one general-purpose IAiClient: each AI capability in this
/// app has exactly one caller, one test fake, and its own degradation contract, and a single
/// fat interface would force every consumer to depend on capabilities it never uses.
///
/// This does no text extraction of its own — PDF/DOCX bytes are turned into text upstream by
/// IResumeTextExtractor, so this stays a pure text-in/facts-out contract with nothing to mock
/// but a string.
/// </summary>
public interface IResumeExtractionClient
{
    /// <param name="resumeText">Plain text already extracted from the uploaded file.</param>
    /// <param name="knownSkillNames">
    /// The current taxonomy. Passed so the model can prefer an existing name when the resume
    /// means the same thing — reconciling at generation time is far more reliable than
    /// reconciling afterwards, and it keeps near-duplicates out of the Ops queue.
    /// </param>
    /// <returns>
    /// Null when extraction is unavailable or fails. Callers must treat null as "no result",
    /// never as "no skills found" — the two are materially different to a candidate staring
    /// at an empty confirmation list.
    /// </returns>
    Task<ResumeExtraction?> ExtractAsync(
        string resumeText,
        IReadOnlyCollection<string> knownSkillNames,
        CancellationToken ct = default);
}

/// <summary>
/// Structured facts from one resume. Deliberately excludes name, email, and phone: the app
/// already holds those from Entra, and not extracting them keeps the PII footprint smaller.
/// </summary>
public sealed record ResumeExtraction(
    IReadOnlyList<ExtractedSkill> Skills,
    string? School,
    string? Degree,
    DateOnly? EstimatedGraduationDate);

/// <param name="Name">The skill as the resume expresses it, before taxonomy reconciliation.</param>
/// <param name="Proficiency">
/// 1-5, matching CandidateSkill.Proficiency. Recorded but deliberately kept out of match
/// scoring for now — folding it into the score would smuggle a scoring change into a
/// resume-parsing feature.
/// </param>
/// <param name="Evidence">
/// A short quote from the resume supporting the skill. Shown to the candidate during
/// confirmation so an inferred skill can be judged against what they actually wrote.
/// </param>
public sealed record ExtractedSkill(string Name, byte Proficiency, string? Evidence);
