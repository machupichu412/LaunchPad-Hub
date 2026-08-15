using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Cohorts;

public record CohortSummary(Cohort Cohort, int CandidateCount, int ProjectCount);

public interface ICohortRepository
{
    Task<IReadOnlyList<CohortSummary>> GetAllWithCountsAsync(CancellationToken ct = default);

    Task<Cohort?> GetByIdAsync(int cohortId, CancellationToken ct = default);

    /// <summary>Cohorts with Status == Active — candidate self-onboarding auto-assigns
    /// to the single active cohort; more than one (or zero) is treated as ambiguous by
    /// the caller, not resolved here.</summary>
    Task<IReadOnlyList<Cohort>> GetActiveAsync(CancellationToken ct = default);

    // Demo-only helper — see CreateCohortRequest for why there's no ProgramId on the request.
    Task<int> GetDefaultProgramIdAsync(CancellationToken ct = default);

    Task<Cohort> AddAsync(Cohort cohort, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
