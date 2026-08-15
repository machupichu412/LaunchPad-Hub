namespace LaunchPad.Application.Matching;

public record MatchResult(int CandidateId, decimal Score, string Rationale);

/// <summary>
/// PastPerformanceScore is 0-1 normalized ((avg OverallScore - 1) / 4), null when the
/// candidate has no submitted reviews yet (their first project — see MatchingEngine's
/// reweighting for this case). InterestRatingsByProjectId holds only the projects this
/// candidate has actually rated on the marketplace; a project absent from it is simply
/// unrated, not zero. TextSimilarityScore (0-1) is precomputed by the caller — e.g. TF-IDF
/// cosine similarity between the project description and this candidate's bio — so the
/// engine itself never touches text/corpus building; null contributes 0, not full credit,
/// since it's a bonus signal rather than an eligibility gate.
/// </summary>
public record MatchCandidate(
    int CandidateId,
    Domain.Enums.Availability Availability,
    IReadOnlyCollection<int> SkillIds,
    DateOnly? GraduationDate = null,
    decimal? PastPerformanceScore = null,
    IReadOnlyDictionary<int, byte>? InterestRatingsByProjectId = null,
    decimal? TextSimilarityScore = null);

public record MatchProject(
    int ProjectId,
    Domain.Enums.Availability AvailabilityNeeded,
    IReadOnlyCollection<(int SkillId, bool IsRequired)> Skills,
    DateOnly? EndDate = null);
