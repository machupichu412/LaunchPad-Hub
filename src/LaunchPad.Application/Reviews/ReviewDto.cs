using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Reviews;

public class ReviewDto
{
    public int ReviewId { get; set; }
    public int AssignmentId { get; set; }
    public ReviewType ReviewType { get; set; }
    public Checkpoint Checkpoint { get; set; }
    public DateTime SubmittedUtc { get; set; }
    public byte? Commitment { get; set; }
    public byte? Availability { get; set; }
    public byte? Guidance { get; set; }
    public byte? OutputQuality { get; set; }
    public string? Comments { get; set; }
    public decimal? OverallScore { get; set; }
}

public class SubmitReviewRequest
{
    public int AssignmentId { get; set; }
    public ReviewType ReviewType { get; set; }
    public Checkpoint Checkpoint { get; set; }
    public byte? Commitment { get; set; }
    public byte? Availability { get; set; }
    public byte? Guidance { get; set; }
    public byte? OutputQuality { get; set; }
    public string? Comments { get; set; }

    // Qualitative counterparts to the hidden numeric dimensions above — these ARE
    // candidate-visible (see CandidateEvaluationDto). Never echoed back with the
    // numeric fields in SponsorReviewDto's raw form; kept together here only
    // because they're submitted in the same form.
    public string? Strengths { get; set; }
    public string? GrowthAreas { get; set; }
    public bool? RecommendConversion { get; set; }
}

/// <summary>
/// What a Sponsor gets back after submitting/listing their own reviews. Deliberately
/// excludes OverallScore and all four numeric rating dimensions — CLAUDE.md's
/// redaction rule ("hidden numeric ratings must never reach a Sponsor or Candidate,
/// not in the JSON payload") has no exception for the person who typed them in.
/// </summary>
public class SponsorReviewDto
{
    public int ReviewId { get; set; }
    public int AssignmentId { get; set; }
    public Checkpoint Checkpoint { get; set; }
    public DateTime SubmittedUtc { get; set; }
    public string? Comments { get; set; }
    public string? Strengths { get; set; }
    public string? GrowthAreas { get; set; }
    public bool? RecommendConversion { get; set; }
}
