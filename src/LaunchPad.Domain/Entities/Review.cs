using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

/// <summary>
/// All ratings live in one flexible table so scoring criteria can change without
/// schema churn. OverallScore is a persisted computed column owned by the database —
/// never set it from application code.
/// </summary>
public class Review
{
    public int ReviewId { get; set; }
    public int AssignmentId { get; set; }
    public ReviewType ReviewType { get; set; }
    public Checkpoint Checkpoint { get; set; }
    public int SubmittedBy { get; set; }
    public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;
    public byte? Commitment { get; set; }
    public byte? Availability { get; set; }
    public byte? Guidance { get; set; }
    public byte? OutputQuality { get; set; }
    public string? Comments { get; set; }
    public decimal? OverallScore { get; private set; }

    // Qualitative counterparts to the hidden numeric score — these ARE candidate-visible
    // (see CandidateEvaluationDto). OverallScore itself never is. Extending this table
    // rather than adding a parallel one — see the class summary above.
    public string? Strengths { get; set; }
    public string? GrowthAreas { get; set; }
    public bool? RecommendConversion { get; set; }

    public Assignment Assignment { get; set; } = null!;
}
