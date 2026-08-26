using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Candidates;

public interface ICandidateRepository
{
    Task<Candidate?> GetWithSkillsAsync(int candidateId, CancellationToken ct = default);
    Task<Candidate?> GetByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default);
    Task<CandidateRisk?> GetRiskAsync(int candidateId, CancellationToken ct = default);
    Task<IReadOnlyList<Candidate>> GetByCohortAsync(int cohortId, CancellationToken ct = default);

    /// <summary>An empty/null cohortIds list means every cohort — the Talent Pipeline's
    /// "All cohorts" filter option.</summary>
    Task<IReadOnlyList<Candidate>> GetByCohortsAsync(IReadOnlyList<int> cohortIds, CancellationToken ct = default);
    Task<Candidate> AddAsync(Candidate candidate, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
