using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Reviews;

public interface IReviewRepository
{
    Task<Review> AddAsync(Review review, CancellationToken ct = default);
    Task<IReadOnlyList<Review>> GetByAssignmentAsync(int assignmentId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
