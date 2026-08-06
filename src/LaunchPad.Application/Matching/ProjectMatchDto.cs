namespace LaunchPad.Application.Matching;

/// <summary>
/// The sponsor's own view of a proposed match on their project — same shape as
/// PendingAssignmentDto (Ops's queue item) minus the sponsor-identifying fields,
/// since the sponsor viewing this already knows it's their own project.
/// </summary>
public class ProjectMatchDto
{
    public int AssignmentId { get; set; }
    public int CandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public decimal? MatchScore { get; set; }
    public string? MatchRationale { get; set; }
}
