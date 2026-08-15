namespace LaunchPad.Application.Matching;

public sealed class MatchingEngine : IMatchingEngine
{
    public IReadOnlyList<MatchResult> RankTopMatches(MatchProject project, IReadOnlyCollection<MatchCandidate> candidates, int topN = 3)
    {
        var requiredSkillIds = project.Skills.Where(s => s.IsRequired).Select(s => s.SkillId).ToHashSet();
        var preferredSkillIds = project.Skills.Where(s => !s.IsRequired).Select(s => s.SkillId).ToHashSet();

        var scored = candidates
            .Where(c => c.Availability == project.AvailabilityNeeded)
            .Select(c => Score(project, c, requiredSkillIds, preferredSkillIds))
            .OrderByDescending(s => s.Result.Score)
            .ThenByDescending(s => s.PerformanceForTieBreak)
            .ThenByDescending(s => s.GraduationForTieBreak)
            .Take(topN)
            .Select(s => s.Result)
            .ToList();

        return scored;
    }

    private static ScoredCandidate Score(
        MatchProject project, MatchCandidate c, IReadOnlySet<int> requiredSkillIds, IReadOnlySet<int> preferredSkillIds)
    {
        var candidateSkillIds = c.SkillIds.ToHashSet();
        var requiredMatchedCount = requiredSkillIds.Count(candidateSkillIds.Contains);
        var preferredMatchedCount = preferredSkillIds.Count(candidateSkillIds.Contains);

        // Required skills are no longer a hard eligibility gate — a candidate missing some
        // (or all) required skills still gets ranked, just scored heavily against it, since
        // required-skill coverage carries roughly 3x the weight of preferred below. Same
        // squared-ratio shape as preferred skills: rewards near-complete coverage more than
        // a linear scale would. No required skills requested = full credit.
        var requiredSkillScore = requiredSkillIds.Count == 0
            ? 1m
            : (decimal)Math.Pow((double)requiredMatchedCount / requiredSkillIds.Count, 2);

        // Squared ratio per the doc — rewards near-complete preferred-skill coverage more
        // than a linear scale would. No preferred skills requested = full credit, never a
        // penalty for a project that simply didn't ask for any.
        var preferredSkillScore = preferredSkillIds.Count == 0
            ? 1m
            : (decimal)Math.Pow((double)preferredMatchedCount / preferredSkillIds.Count, 2);

        // Y/N: does the project wrap up on or before the candidate's graduation, i.e. are
        // they still eligible for hire when it ends? Unknown on either side (no project
        // end date, or no graduation date on file) is neutral — full credit — rather than
        // penalizing missing data.
        bool? graduationAligned = project.EndDate is null || c.GraduationDate is null
            ? null
            : project.EndDate.Value <= c.GraduationDate.Value;
        var graduationScore = graduationAligned == false ? 0m : 1m;

        // Text similarity (e.g. TF-IDF cosine between project description and candidate
        // bio) is a small bonus signal, never a gate — missing/unset contributes 0.
        var textSimilarityScore = c.TextSimilarityScore ?? 0m;

        decimal compositeFraction;
        if (c.PastPerformanceScore is decimal performance)
        {
            compositeFraction = requiredSkillScore * 0.45m + preferredSkillScore * 0.15m
                + performance * 0.25m + graduationScore * 0.10m + textSimilarityScore * 0.05m;
        }
        else
        {
            // First project — no review history to weigh, so performance's weight is
            // redistributed onto required/preferred skills, graduation, and text similarity.
            compositeFraction = requiredSkillScore * 0.65m + preferredSkillScore * 0.20m
                + graduationScore * 0.05m + textSimilarityScore * 0.10m;
        }

        // Interest "feeds in slightly": a small additive nudge, never a gate. Unrated
        // (candidate never browsed/rated this project) applies no nudge either way.
        byte? ratedInterest = c.InterestRatingsByProjectId is { } ratings && ratings.TryGetValue(project.ProjectId, out var rating)
            ? rating
            : null;
        var interestNudge = ratedInterest is byte r ? r - 3 : 0;

        var score = Math.Clamp(Math.Round(compositeFraction * 100m + interestNudge, 2), 0m, 100m);
        var rationale = BuildRationale(
            requiredMatchedCount, requiredSkillIds.Count,
            preferredMatchedCount, preferredSkillIds.Count,
            c.PastPerformanceScore, graduationAligned, ratedInterest, textSimilarityScore);

        return new ScoredCandidate(
            new MatchResult(c.CandidateId, score, rationale),
            c.PastPerformanceScore ?? -1m,
            graduationScore);
    }

    private static string BuildRationale(
        int requiredMatchedCount, int requiredCount,
        int preferredMatchedCount, int preferredCount,
        decimal? pastPerformanceScore, bool? graduationAligned, byte? interestRating, decimal textSimilarityScore)
    {
        var parts = new List<string>
        {
            $"Matched {requiredMatchedCount}/{requiredCount} required and {preferredMatchedCount}/{preferredCount} preferred skills",
            "availability aligned with project need",
            pastPerformanceScore is decimal performance
                ? $"past performance score {Math.Round(performance * 100m)}%"
                : "no review history yet (first project — weighted toward skills)",
            graduationAligned switch
            {
                true => "on track to graduate on or after the project ends",
                false => "may graduate before the project ends",
                null => "graduation timing unknown",
            },
        };

        if (interestRating is byte rating)
        {
            parts.Add($"candidate rated their interest {rating}/5");
        }

        if (textSimilarityScore > 0m)
        {
            parts.Add($"resume/description text similarity {Math.Round(textSimilarityScore * 100m)}%");
        }

        return string.Join("; ", parts) + ".";
    }

    private sealed record ScoredCandidate(MatchResult Result, decimal PerformanceForTieBreak, decimal GraduationForTieBreak);
}
