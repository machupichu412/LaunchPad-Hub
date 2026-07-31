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

    Task<IReadOnlyList<ProjectTodo>> GetTodosAsync(int assignmentId, CancellationToken ct = default);
    Task<ProjectTodo?> GetTodoAsync(int assignmentId, int todoId, CancellationToken ct = default);

    Task<IReadOnlyList<Deliverable>> GetDeliverablesAsync(int assignmentId, CancellationToken ct = default);
    Task<Deliverable> AddDeliverableAsync(Deliverable deliverable, CancellationToken ct = default);

    /// <summary>Sponsor-on-candidate reviews only — never returns the reverse ReviewType.</summary>
    Task<IReadOnlyList<Review>> GetCandidateEvaluationsAsync(int assignmentId, CancellationToken ct = default);
}
