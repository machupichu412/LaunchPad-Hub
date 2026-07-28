namespace LaunchPad.Application.Matching;

public sealed class MatchingEngine : IMatchingEngine
{
    public IReadOnlyList<MatchResult> RankTopMatches(MatchProject project, IReadOnlyCollection<MatchCandidate> candidates, int topN = 3)
    {
        var requiredSkillIds = project.Skills.Where(s => s.IsRequired).Select(s => s.SkillId).ToHashSet();
        var preferredSkillIds = project.Skills.Where(s => !s.IsRequired).Select(s => s.SkillId).ToHashSet();

        var eligible = candidates.Where(c => c.Availability == project.AvailabilityNeeded);

        var scored = eligible.Select(c =>
        {
            var candidateSkillIds = c.SkillIds.ToHashSet();
            var matchedRequired = requiredSkillIds.Count == 0 ? 1m : (decimal)requiredSkillIds.Count(candidateSkillIds.Contains) / requiredSkillIds.Count;
            var matchedPreferred = preferredSkillIds.Count == 0 ? 0m : (decimal)preferredSkillIds.Count(candidateSkillIds.Contains) / preferredSkillIds.Count;

            // Eligibility gate: a required skill floor must be met to be considered at all.
            if (requiredSkillIds.Count > 0 && matchedRequired == 0m)
            {
                return null;
            }

            var score = Math.Round((matchedRequired * 0.7m + matchedPreferred * 0.3m) * 100m, 2);
            var rationale = $"Matched {requiredSkillIds.Count(candidateSkillIds.Contains)}/{requiredSkillIds.Count} required and " +
                             $"{preferredSkillIds.Count(candidateSkillIds.Contains)}/{preferredSkillIds.Count} preferred skills; " +
                             $"availability aligned with project need.";

            return new MatchResult(c.CandidateId, score, rationale);
        })
        .Where(r => r is not null)
        .Cast<MatchResult>()
        .OrderByDescending(r => r.Score)
        .Take(topN)
        .ToList();

        return scored;
    }
}
