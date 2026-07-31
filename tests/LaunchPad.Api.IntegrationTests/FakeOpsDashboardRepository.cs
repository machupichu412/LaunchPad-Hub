using LaunchPad.Application.Reporting;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// CandidateRisk is keyless (backed by dbo.vCandidateRisk in prod, see
/// TestCandidateRepositoryWithFakeRisk) and OpsDashboardRepository is built almost
/// entirely around joining it, so — unlike that class — there's no real inner
/// implementation left to delegate to here. These tests only need to prove the
/// authorization boundary (ProgramOps reaches these endpoints, other roles don't),
/// not aggregation correctness, so canned data is enough.
/// </summary>
public sealed class FakeOpsDashboardRepository : IOpsDashboardRepository
{
    public Task<OpsDashboardDto> GetDashboardAsync(CancellationToken ct = default) =>
        Task.FromResult(new OpsDashboardDto
        {
            ActiveCandidateCount = 3,
            ActiveProjectCount = 2,
            ActiveProjectCohortCount = 1,
            PendingApprovalCount = 1,
            ApprovedTotalCount = 1,
            HighRiskCount = 1,
            MatchFunnel = new MatchFunnelDto { Proposed = 1, Approved = 1, Denied = 0, Active = 1 },
            TopRisks = new List<RiskCandidateDto>
            {
                new() { CandidateId = 1, DisplayName = "Fake Candidate", CohortName = "Fake Cohort", AvgScore = 2.1m, HasPerformanceRisk = true, HasEngagementRisk = false, StaleTodoCount = 2 },
            },
        });

    public Task<IReadOnlyList<RiskCandidateDto>> GetAtRiskCandidatesAsync(int? take = null, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RiskCandidateDto>>(new List<RiskCandidateDto>
        {
            new() { CandidateId = 1, DisplayName = "Fake Candidate", CohortName = "Fake Cohort", AvgScore = 2.1m, HasPerformanceRisk = true, HasEngagementRisk = false, StaleTodoCount = 2 },
        });
}
