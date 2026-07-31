using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Cohorts;

public record CohortSummary(Cohort Cohort, int CandidateCount, int ProjectCount);

public interface ICohortRepository
{
    Task<IReadOnlyList<CohortSummary>> GetAllWithCountsAsync(CancellationToken ct = default);

    // Demo-only helper — see CreateCohortRequest for why there's no ProgramId on the request.
    Task<int> GetDefaultProgramIdAsync(CancellationToken ct = default);

    Task<Cohort> AddAsync(Cohort cohort, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
