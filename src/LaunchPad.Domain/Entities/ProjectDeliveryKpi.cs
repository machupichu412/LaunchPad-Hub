namespace LaunchPad.Domain.Entities;

/// <summary>
/// Maps to the dbo.vProjectDeliveryKpi database view — one row per cohort, aggregating
/// Project.DeliveryStage into the counts the Executive KPI dashboard needs. Shared by the
/// API and Power BI, same "computed view is the single source of truth" rule vCandidateRisk
/// follows — never recompute this logic in application code.
/// </summary>
public class ProjectDeliveryKpi
{
    public int CohortId { get; set; }

    /// <summary>Non-cancelled projects in the cohort — the denominator for every rate below.</summary>
    public int ProjectCount { get; set; }

    /// <summary>DeliveryStage >= MvpBuilt. Backs "Prototype maturity: 100% MVP."</summary>
    public int MvpCount { get; set; }

    /// <summary>DeliveryStage >= PilotReady. Backs both "Prototype maturity: 80% pilot-ready"
    /// and "Pilot & adoption readiness: >=50% advance beyond showcase" — same underlying
    /// signal, two KPI rows on the slide with different targets.</summary>
    public int PilotReadyCount { get; set; }

    /// <summary>DeliveryStage == BusinessValueDocumented (the terminal stage). Backs "Business
    /// value generated: 100% documented with sponsor sign-off" and, as a raw count,
    /// "AI solutions delivered."</summary>
    public int BusinessValueDocumentedCount { get; set; }
}
