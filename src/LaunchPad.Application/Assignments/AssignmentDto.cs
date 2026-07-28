using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Assignments;

public class AssignmentDto
{
    public int AssignmentId { get; set; }
    public int ProjectId { get; set; }
    public int CandidateId { get; set; }
    public AssignmentStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    // MatchScore/MatchRationale are ops/exec-only — apply the same redaction
    // discipline as CandidateDtoMapper if this DTO is ever exposed to sponsors.
    public decimal? MatchScore { get; set; }
    public string? MatchRationale { get; set; }
}
