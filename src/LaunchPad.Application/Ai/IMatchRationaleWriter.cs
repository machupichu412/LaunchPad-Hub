namespace LaunchPad.Application.Ai;

/// <summary>
/// Writes the human-readable explanation attached to a proposed assignment.
///
/// This never touches the score. MatchingEngine computes and explains matches deterministically;
/// this only replaces the wording of the explanation, so a cohort run remains reproducible and
/// ProjectsController's synchronous re-scoring still agrees with it.
/// </summary>
public interface IMatchRationaleWriter
{
    /// <summary>
    /// Returns null on any failure — unavailable, timed out, throttled, content-filtered.
    /// Callers fall back to the engine's deterministic rationale. A matching run must never
    /// fail because a rationale could not be written.
    /// </summary>
    Task<string?> WriteAsync(MatchRationaleContext context, CancellationToken ct = default);
}

/// <summary>
/// Everything the writer is allowed to see about one proposed match.
///
/// Carries no numeric rating and no candidate name, by construction. The rationale is persisted
/// to Assignment.MatchRationale and read by Sponsors and Candidates through DTOs that the role
/// gate on AverageScore does not cover, and generated prose cannot be redacted by a DTO mapper
/// the way a structured field can. Anything omitted here cannot leak downstream, so the shape of
/// this record is the control.
/// </summary>
public sealed record MatchRationaleContext(
    string ProjectName,
    string? ProjectDescription,
    IReadOnlyList<string> RequiredSkillsMatched,
    IReadOnlyList<string> RequiredSkillsMissing,
    IReadOnlyList<string> PreferredSkillsMatched,
    /// <summary>A coarse band — never a score. See MatchingEngine's performance banding.</summary>
    string PerformanceBand,
    string GraduationAlignment,
    byte? CandidateInterestRating);
