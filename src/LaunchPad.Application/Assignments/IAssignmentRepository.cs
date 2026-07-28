using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Assignments;

public interface IAssignmentRepository
{
    Task<Assignment?> GetAsync(int assignmentId, CancellationToken ct = default);
    Task<Assignment> AddAsync(Assignment assignment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
