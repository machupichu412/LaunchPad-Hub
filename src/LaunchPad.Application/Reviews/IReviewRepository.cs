using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Reviews;

public interface IReviewRepository
{
    Task<Review> AddAsync(Review review, CancellationToken ct = default);
    Task<IReadOnlyList<Review>> GetByAssignmentAsync(int assignmentId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>The most recent Final-checkpoint SponsorOnCandidate review's
    /// RecommendConversion for this candidate, across any of their assignments — null if no
    /// Final review exists yet. Feeds HireOutcomeRule; never itself a source of the hidden
    /// OverallScore.</summary>
    Task<bool?> GetLatestFinalRecommendConversionAsync(int candidateId, CancellationToken ct = default);
}
