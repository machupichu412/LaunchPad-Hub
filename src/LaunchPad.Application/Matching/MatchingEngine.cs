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
            .Where(s => s is not null)
            .OrderByDescending(s => s!.Result.Score)
            .ThenByDescending(s => s!.PerformanceForTieBreak)
            .ThenByDescending(s => s!.GraduationForTieBreak)
            .Take(topN)
            .Select(s => s!.Result)
            .ToList();

        return scored;
    }

    private static ScoredCandidate? Score(
        MatchProject project, MatchCandidate c, IReadOnlySet<int> requiredSkillIds, IReadOnlySet<int> preferredSkillIds)
    {
        var candidateSkillIds = c.SkillIds.ToHashSet();
        var requiredMatchedCount = requiredSkillIds.Count(candidateSkillIds.Contains);
        var preferredMatchedCount = preferredSkillIds.Count(candidateSkillIds.Contains);

        // Eligibility gate: ALL required skills must be present, not just one — a partial
        // match on required skills isn't eligible at all (Matching Logic Doc: "Having
        // 100% of required skills allows eligibility... Pass or fail").
        if (requiredSkillIds.Count > 0 && requiredMatchedCount < requiredSkillIds.Count)
        {
            return null;
        }

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

        decimal compositeFraction;
        if (c.PastPerformanceScore is decimal performance)
        {
            compositeFraction = preferredSkillScore * 0.65m + performance * 0.25m + graduationScore * 0.10m;
        }
        else
        {
            // First project — no review history to weigh, so the doc's own override
            // reweights entirely onto preferred skills + graduation proximity.
            compositeFraction = preferredSkillScore * 0.95m + graduationScore * 0.05m;
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
            c.PastPerformanceScore, graduationAligned, ratedInterest);

        return new ScoredCandidate(
            new MatchResult(c.CandidateId, score, rationale),
            c.PastPerformanceScore ?? -1m,
            graduationScore);
    }

    private static string BuildRationale(
        int requiredMatchedCount, int requiredCount,
        int preferredMatchedCount, int preferredCount,
        decimal? pastPerformanceScore, bool? graduationAligned, byte? interestRating)
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

        return string.Join("; ", parts) + ".";
    }

    private sealed record ScoredCandidate(MatchResult Result, decimal PerformanceForTieBreak, decimal GraduationForTieBreak);
}
