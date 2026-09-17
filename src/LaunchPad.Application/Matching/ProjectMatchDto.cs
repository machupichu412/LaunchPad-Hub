namespace LaunchPad.Application.Matching;

/// <summary>
/// The sponsor's own view of a proposed match on their project — PendingAssignmentDto
/// (Ops's queue item) minus the sponsor-identifying fields, and minus the score, which
/// sponsors don't receive. Matches arrive best-first, and the rationale says why.
/// </summary>
public class ProjectMatchDto
{
    public int AssignmentId { get; set; }
    public int CandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string? MatchRationale { get; set; }
}
