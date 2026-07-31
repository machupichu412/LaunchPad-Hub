namespace LaunchPad.Application.Matching;

public class PendingAssignmentDto
{
    public int AssignmentId { get; set; }
    public int CandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string SponsorName { get; set; } = string.Empty;
    public string? SponsorOrganization { get; set; }
    public decimal? MatchScore { get; set; }
    public string? MatchRationale { get; set; }
}

public class RunMatchingResult
{
    public int ProposedCount { get; set; }
}
