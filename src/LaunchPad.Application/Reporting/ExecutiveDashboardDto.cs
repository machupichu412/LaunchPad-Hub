namespace LaunchPad.Application.Reporting;

/// <summary>
/// Funnel: recommended (SponsorApproved) -> approved (OpsApproved) -> hired.
/// Backed by dbo.vCandidateRisk and Assignment status counts — read-only, exec/ops only.
///
/// The KPI fields below back the "LaunchPad Core Objectives & Exec KPIs" dashboard tiles.
/// Delivery-stage counts are backed by dbo.vProjectDeliveryKpi (see ProjectDeliveryKpi);
/// hire-ready rate, sponsor rating, and the university breakdown are plain aggregate
/// queries over already cohort-indexed columns, same style as the funnel counts above —
/// see ReportingRepository.GetExecutiveDashboardAsync.
/// </summary>
public class ExecutiveDashboardDto
{
    public int CohortId { get; set; }
    public int RecommendedCount { get; set; }
    public int ApprovedCount { get; set; }
    public int HiredCount { get; set; }
    public int PerformanceRiskCount { get; set; }
    public int EngagementRiskCount { get; set; }

    /// <summary>Non-cancelled projects in the cohort — denominator for the delivery-stage
    /// rates below.</summary>
    public int ProjectCount { get; set; }

    /// <summary>Projects at the terminal BusinessValueDocumented stage — "AI solutions
    /// delivered" (tile: "20+ AI solutions per cohort").</summary>
    public int SolutionsDeliveredCount { get; set; }

    /// <summary>Projects with DeliveryStage >= MvpBuilt — "Prototype maturity: 100% MVP."</summary>
    public int MvpCompleteCount { get; set; }

    /// <summary>Projects with DeliveryStage >= PilotReady — backs both "Prototype maturity:
    /// 80% pilot-ready" and "Pilot &amp; adoption readiness: >=50% advance beyond showcase."</summary>
    public int PilotReadyCount { get; set; }

    /// <summary>Same as SolutionsDeliveredCount, named for its other KPI role — "Business
    /// value generated: 100% documented with sponsor sign-off."</summary>
    public int BusinessValueDocumentedCount { get; set; }

    /// <summary>Candidates with a final Status of Hire or TalentPlus.</summary>
    public int HireReadyCandidateCount { get; set; }

    /// <summary>Candidates with any decided Status (excludes InProgress) — denominator
    /// for "Hire-ready talent rate: >=80% deemed hire-ready."</summary>
    public int DecidedCandidateCount { get; set; }

    /// <summary>Average SponsorOnCandidate Review.OverallScore across the cohort, or null if
    /// no sponsor review has been submitted yet. Backs "Sponsor endorsement: average rating
    /// >=4.0/5." Safe to expose here — this endpoint is already ViewHiddenScores-gated.</summary>
    public decimal? AverageSponsorRating { get; set; }

    /// <summary>How many ratings AverageSponsorRating is computed from — shown alongside the
    /// average so a tiny sample doesn't read as a confident number.</summary>
    public int SponsorRatingCount { get; set; }

    /// <summary>Candidate count grouped by School — "Future Workforce Diversity" / the
    /// "University Breakdown" tile. Ordered by count descending.</summary>
    public IReadOnlyList<UniversityBreakdownDto> UniversityBreakdown { get; set; } = Array.Empty<UniversityBreakdownDto>();
}

public class UniversityBreakdownDto
{
    public string School { get; set; } = "Unspecified";
    public int CandidateCount { get; set; }
}

public interface IReportingRepository
{
    Task<ExecutiveDashboardDto> GetExecutiveDashboardAsync(int cohortId, CancellationToken ct = default);
}
