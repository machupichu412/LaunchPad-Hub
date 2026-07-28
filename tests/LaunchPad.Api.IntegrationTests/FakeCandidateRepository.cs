using LaunchPad.Application.Candidates;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Avoids standing up a real Azure SQL instance (with its vCandidateRisk view) just
/// to exercise the authorization/redaction boundary. The behavior under test is the
/// mapper + policy pipeline, not EF Core query translation.
/// </summary>
public sealed class FakeCandidateRepository : ICandidateRepository
{
    public static readonly Candidate Seeded = new()
    {
        CandidateId = 1,
        AppUser = new AppUser { DisplayName = "Jordan Rivera" },
        Skills = new List<CandidateSkill>(),
        Status = CandidateStatus.InProgress
    };

    public static readonly CandidateRisk SeededRisk = new()
    {
        CandidateId = 1,
        AvgScore = 2.1m,
        HasPerformanceRisk = true,
        HasEngagementRisk = false
    };

    public Task<Candidate?> GetWithSkillsAsync(int candidateId, CancellationToken ct = default) =>
        Task.FromResult<Candidate?>(candidateId == Seeded.CandidateId ? Seeded : null);

    public Task<CandidateRisk?> GetRiskAsync(int candidateId, CancellationToken ct = default) =>
        Task.FromResult<CandidateRisk?>(candidateId == Seeded.CandidateId ? SeededRisk : null);

    public Task<IReadOnlyList<Candidate>> GetByCohortAsync(int cohortId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Candidate>>(new List<Candidate> { Seeded });
}
