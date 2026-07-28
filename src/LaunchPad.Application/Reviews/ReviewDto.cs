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
}
