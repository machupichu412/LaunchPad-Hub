using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Matching;

public interface IProjectInterestRepository
{
    /// <summary>Every interest rating from candidates in the cohort — the matching
    /// engine's bulk input, grouped per-candidate by the caller (MatchingController.Run).</summary>
    Task<IReadOnlyList<ProjectInterest>> GetByCohortAsync(int cohortId, CancellationToken ct = default);

    /// <summary>One candidate's own ratings, keyed by ProjectId — the marketplace's "my
    /// rating" lookup for both the browse list and the detail view.</summary>
    Task<IReadOnlyDictionary<int, byte>> GetRatingsForCandidateAsync(int candidateId, CancellationToken ct = default);

    /// <summary>Rating again updates the existing row (unique per Candidate+Project) —
    /// never a second row.</summary>
    Task<ProjectInterest> UpsertAsync(int candidateId, int projectId, byte rating, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
