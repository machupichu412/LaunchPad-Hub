namespace LaunchPad.Domain.Entities;

/// <summary>
/// Maps to the dbo.vCandidateRisk database view — the single source of truth for
/// risk signals, shared by the API and Power BI. Never recompute this logic in
/// application code.
/// </summary>
public class CandidateRisk
{
    public int CandidateId { get; set; }
    public decimal? AvgScore { get; set; }
    public decimal? MidScore { get; set; }
    public decimal? FinalScore { get; set; }
    public bool HasPerformanceRisk { get; set; }
    public bool HasEngagementRisk { get; set; }
    public int StaleTodoCount { get; set; }
}
