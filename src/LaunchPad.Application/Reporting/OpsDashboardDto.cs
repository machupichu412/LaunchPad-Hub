namespace LaunchPad.Application.Reporting;

/// <summary>
/// Cross-cohort, unlike ExecutiveDashboardDto — the mockup's "Active Candidates ...
/// Across all cohorts" framing makes this a program-wide admin view, not a
/// per-cohort one. Severity on RiskCandidateDto is derived display-only from the
/// two real boolean flags (never a new stored field) — see CLAUDE.md's redaction rule.
/// </summary>
public class OpsDashboardDto
{
    public int ActiveCandidateCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public int ActiveProjectCohortCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int ApprovedTotalCount { get; set; }
    public int HighRiskCount { get; set; }
    public MatchFunnelDto MatchFunnel { get; set; } = new();
    public IReadOnlyList<RiskCandidateDto> TopRisks { get; set; } = Array.Empty<RiskCandidateDto>();
}

public class MatchFunnelDto
{
    public int Proposed { get; set; }
    public int Approved { get; set; }
    public int Denied { get; set; }
    public int Active { get; set; }
}

public class RiskCandidateDto
{
    public int CandidateId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string CohortName { get; set; } = string.Empty;
    public decimal? AvgScore { get; set; }
    public bool HasPerformanceRisk { get; set; }
    public bool HasEngagementRisk { get; set; }
    public int StaleTodoCount { get; set; }
}

public interface IOpsDashboardRepository
{
    Task<OpsDashboardDto> GetDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RiskCandidateDto>> GetAtRiskCandidatesAsync(int? take = null, CancellationToken ct = default);
}
