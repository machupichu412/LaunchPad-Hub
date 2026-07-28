namespace LaunchPad.Application.Reporting;

/// <summary>
/// Funnel: recommended (SponsorApproved) -> approved (OpsApproved) -> hired.
/// Backed by dbo.vCandidateRisk and Assignment status counts — read-only, exec/ops only.
/// </summary>
public class ExecutiveDashboardDto
{
    public int CohortId { get; set; }
    public int RecommendedCount { get; set; }
    public int ApprovedCount { get; set; }
    public int HiredCount { get; set; }
    public int PerformanceRiskCount { get; set; }
    public int EngagementRiskCount { get; set; }
}

public interface IReportingRepository
{
    Task<ExecutiveDashboardDto> GetExecutiveDashboardAsync(int cohortId, CancellationToken ct = default);
}
