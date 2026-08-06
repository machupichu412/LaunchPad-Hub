using LaunchPad.Application.Reporting;
using LaunchPad.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class OpsDashboardRepository : IOpsDashboardRepository
{
    private readonly LaunchPadDbContext _db;
    public OpsDashboardRepository(LaunchPadDbContext db) => _db = db;

    public async Task<OpsDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var activeCandidateCount = await _db.Candidates.CountAsync(c => c.Status == CandidateStatus.InProgress, ct);

        var activeProjects = _db.Projects.Where(p => p.Status == ProjectStatus.Open || p.Status == ProjectStatus.InProgress);
        var activeProjectCount = await activeProjects.CountAsync(ct);
        var activeProjectCohortCount = await activeProjects.Select(p => p.CohortId).Distinct().CountAsync(ct);

        var proposedCount = await _db.Assignments.CountAsync(a => a.Status == AssignmentStatus.Proposed, ct);
        // What's actually actionable in Ops's queue under the two-stage flow —
        // Proposed is still awaiting sponsor review, not Ops's.
        var sponsorApprovedCount = await _db.Assignments.CountAsync(a => a.Status == AssignmentStatus.SponsorApproved, ct);
        var approvedTotalCount = await _db.Assignments.CountAsync(
            a => a.Status == AssignmentStatus.OpsApproved || a.Status == AssignmentStatus.Active, ct);
        var deniedCount = await _db.Assignments.CountAsync(a => a.Status == AssignmentStatus.Withdrawn, ct);
        var activeCount = await _db.Assignments.CountAsync(a => a.Status == AssignmentStatus.Active, ct);

        var highRiskCount = await _db.CandidateRisks.CountAsync(r => r.HasPerformanceRisk || r.HasEngagementRisk, ct);
        var topRisks = await GetAtRiskCandidatesAsync(take: 3, ct);

        return new OpsDashboardDto
        {
            ActiveCandidateCount = activeCandidateCount,
            ActiveProjectCount = activeProjectCount,
            ActiveProjectCohortCount = activeProjectCohortCount,
            PendingApprovalCount = sponsorApprovedCount,
            ApprovedTotalCount = approvedTotalCount,
            HighRiskCount = highRiskCount,
            MatchFunnel = new MatchFunnelDto
            {
                Proposed = proposedCount,
                Approved = approvedTotalCount,
                Denied = deniedCount,
                Active = activeCount,
            },
            TopRisks = topRisks,
        };
    }

    public async Task<IReadOnlyList<RiskCandidateDto>> GetAtRiskCandidatesAsync(int? take = null, CancellationToken ct = default)
    {
        var query =
            from c in _db.Candidates
            join r in _db.CandidateRisks on c.CandidateId equals r.CandidateId
            where r.HasPerformanceRisk || r.HasEngagementRisk
            orderby (r.HasPerformanceRisk ? 1 : 0) + (r.HasEngagementRisk ? 1 : 0) descending, r.AvgScore
            select new RiskCandidateDto
            {
                CandidateId = c.CandidateId,
                DisplayName = c.AppUser.DisplayName,
                CohortName = c.Cohort.Name,
                AvgScore = r.AvgScore,
                HasPerformanceRisk = r.HasPerformanceRisk,
                HasEngagementRisk = r.HasEngagementRisk,
                StaleTodoCount = r.StaleTodoCount,
            };

        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }

        return await query.ToListAsync(ct);
    }
}
