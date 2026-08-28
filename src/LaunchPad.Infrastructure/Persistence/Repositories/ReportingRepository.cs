using LaunchPad.Application.Reporting;
using LaunchPad.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class ReportingRepository : IReportingRepository
{
    private readonly LaunchPadDbContext _db;
    public ReportingRepository(LaunchPadDbContext db) => _db = db;

    public async Task<ExecutiveDashboardDto> GetExecutiveDashboardAsync(int cohortId, CancellationToken ct = default)
    {
        var assignments = _db.Assignments.Where(a => a.Project.CohortId == cohortId);

        var recommended = await assignments.CountAsync(a => a.Status >= AssignmentStatus.SponsorApproved, ct);
        var approved = await assignments.CountAsync(a => a.Status >= AssignmentStatus.OpsApproved, ct);
        var hired = await _db.Candidates.CountAsync(c => c.CohortId == cohortId && c.Status == CandidateStatus.Hire, ct);

        var risks = await (from c in _db.Candidates
                           join r in _db.CandidateRisks on c.CandidateId equals r.CandidateId
                           where c.CohortId == cohortId
                           select r).ToListAsync(ct);

        // Delivery-stage KPIs — one indexed row per cohort, shared with Power BI. See
        // ProjectDeliveryKpi/vProjectDeliveryKpi for what each count means.
        var delivery = await _db.ProjectDeliveryKpis.FirstOrDefaultAsync(k => k.CohortId == cohortId, ct);

        // Hire-ready rate — two indexed COUNTs over Candidate.CohortId, same shape as
        // `hired` above rather than a view: simple enough not to warrant one.
        var decidedCandidates = await _db.Candidates
            .CountAsync(c => c.CohortId == cohortId && c.Status != CandidateStatus.InProgress, ct);
        var hireReadyCandidates = await _db.Candidates
            .CountAsync(c => c.CohortId == cohortId
                && (c.Status == CandidateStatus.Hire || c.Status == CandidateStatus.TalentPlus), ct);

        // Sponsor endorsement — AVG/COUNT pushed to SQL rather than materialized client-side.
        var sponsorRatings = _db.Reviews.Where(r =>
            r.ReviewType == ReviewType.SponsorOnCandidate
            && r.Assignment.Project.CohortId == cohortId
            && r.OverallScore != null);
        var sponsorRatingCount = await sponsorRatings.CountAsync(ct);
        var averageSponsorRating = sponsorRatingCount > 0
            ? await sponsorRatings.AverageAsync(r => r.OverallScore!.Value, ct)
            : (decimal?)null;

        // University breakdown — one GROUP BY over an already cohort-indexed column.
        var universityBreakdown = await _db.Candidates
            .Where(c => c.CohortId == cohortId)
            .GroupBy(c => c.School)
            .Select(g => new UniversityBreakdownDto { School = g.Key ?? "Unspecified", CandidateCount = g.Count() })
            .OrderByDescending(u => u.CandidateCount)
            .ToListAsync(ct);

        return new ExecutiveDashboardDto
        {
            CohortId = cohortId,
            RecommendedCount = recommended,
            ApprovedCount = approved,
            HiredCount = hired,
            PerformanceRiskCount = risks.Count(r => r.HasPerformanceRisk),
            EngagementRiskCount = risks.Count(r => r.HasEngagementRisk),
            ProjectCount = delivery?.ProjectCount ?? 0,
            SolutionsDeliveredCount = delivery?.BusinessValueDocumentedCount ?? 0,
            MvpCompleteCount = delivery?.MvpCount ?? 0,
            PilotReadyCount = delivery?.PilotReadyCount ?? 0,
            BusinessValueDocumentedCount = delivery?.BusinessValueDocumentedCount ?? 0,
            HireReadyCandidateCount = hireReadyCandidates,
            DecidedCandidateCount = decidedCandidates,
            AverageSponsorRating = averageSponsorRating.HasValue ? Math.Round(averageSponsorRating.Value, 2) : null,
            SponsorRatingCount = sponsorRatingCount,
            UniversityBreakdown = universityBreakdown,
        };
    }
}
