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

/// <summary>Matching now runs async (Service Bus + CohortMatchingFunction, or the
/// local-dev inline fallback) — Run publishes the job and returns immediately, so there's
/// no synchronous proposed count to report anymore. Queued is always true on a 202; kept
/// as a field (rather than an empty body) so the frontend has something to key a "job
/// queued" toast off of.</summary>
public class RunMatchingResult
{
    public bool Queued { get; set; }
}
