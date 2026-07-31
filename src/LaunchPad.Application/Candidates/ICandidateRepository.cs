using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Candidates;

public interface ICandidateRepository
{
    Task<Candidate?> GetWithSkillsAsync(int candidateId, CancellationToken ct = default);
    Task<Candidate?> GetByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default);
    Task<CandidateRisk?> GetRiskAsync(int candidateId, CancellationToken ct = default);
    Task<IReadOnlyList<Candidate>> GetByCohortAsync(int cohortId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
