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

        return new ExecutiveDashboardDto
        {
            CohortId = cohortId,
            RecommendedCount = recommended,
            ApprovedCount = approved,
            HiredCount = hired,
            PerformanceRiskCount = risks.Count(r => r.HasPerformanceRisk),
            EngagementRiskCount = risks.Count(r => r.HasEngagementRisk)
        };
    }
}
