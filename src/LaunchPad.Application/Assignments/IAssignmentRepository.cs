using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Assignments;

public interface IAssignmentRepository
{
    Task<Assignment?> GetAsync(int assignmentId, CancellationToken ct = default);

    /// <summary>Includes Candidate.AppUser — required by OwnsAssignmentHandler to check EntraObjectId.</summary>
    Task<Assignment?> GetWithOwnershipDetailsAsync(int assignmentId, CancellationToken ct = default);

    Task<Assignment?> GetActiveByCandidateIdAsync(int candidateId, CancellationToken ct = default);
    Task<Assignment> AddAsync(Assignment assignment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Status == Proposed, scoped to a cohort — the Ops approval queue.</summary>
    Task<IReadOnlyList<Assignment>> GetPendingByCohortAsync(int cohortId, CancellationToken ct = default);

    /// <summary>Candidates in the cohort with no assignment in a non-terminal status — the matching pool.</summary>
    Task<IReadOnlyList<Candidate>> GetEligibleCandidatesForMatchingAsync(int cohortId, CancellationToken ct = default);

    /// <summary>Status IN (OpsApproved, Active) — mirrors the DB's unique filtered index exactly,
    /// so an approval can check for a conflict before hitting that constraint.</summary>
    Task<Assignment?> GetLiveAssignmentAsync(int candidateId, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectTodo>> GetTodosAsync(int assignmentId, CancellationToken ct = default);
    Task<ProjectTodo?> GetTodoAsync(int assignmentId, int todoId, CancellationToken ct = default);

    Task<IReadOnlyList<Deliverable>> GetDeliverablesAsync(int assignmentId, CancellationToken ct = default);
    Task<Deliverable> AddDeliverableAsync(Deliverable deliverable, CancellationToken ct = default);

    /// <summary>Sponsor-on-candidate reviews only — never returns the reverse ReviewType.</summary>
    Task<IReadOnlyList<Review>> GetCandidateEvaluationsAsync(int assignmentId, CancellationToken ct = default);
}
