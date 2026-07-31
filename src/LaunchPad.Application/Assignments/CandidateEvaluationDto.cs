using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Assignments;

/// <summary>
/// Deliberately separate from ReviewDto, which still carries raw OverallScore
/// unredacted — that DTO must stay internal/Ops-only. This one is safe for a
/// candidate: qualitative feedback and a categorical recommendation only, never a
/// numeric or star rating. See CLAUDE.md's "hidden ratings" control.
/// </summary>
public class CandidateEvaluationDto
{
    public int ReviewId { get; set; }
    public Checkpoint Checkpoint { get; set; }
    public DateTime SubmittedUtc { get; set; }
    public string? Strengths { get; set; }
    public string? GrowthAreas { get; set; }
    public bool? RecommendConversion { get; set; }
}
