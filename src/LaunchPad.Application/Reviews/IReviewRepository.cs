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

    /// <summary>Batch form of <see cref="GetLatestFinalRecommendConversionAsync"/> for list
    /// endpoints. Candidates with no Final review are absent from the dictionary, which the
    /// caller reads the same as the single form's null.</summary>
    Task<IReadOnlyDictionary<int, bool?>> GetLatestFinalRecommendConversionsAsync(IReadOnlyList<int> candidateIds, CancellationToken ct = default);
}
