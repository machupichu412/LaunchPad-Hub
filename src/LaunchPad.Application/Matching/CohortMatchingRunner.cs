using LaunchPad.Application.Assignments;
using LaunchPad.Application.Common;
using LaunchPad.Application.Projects;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Matching;

public sealed class CohortMatchingRunner : ICohortMatchingRunner
{
    private readonly IProjectRepository _projects;
    private readonly IAssignmentRepository _assignments;
    private readonly IProjectInterestRepository _projectInterests;
    private readonly IMatchingEngine _matchingEngine;
    private readonly ITextSimilarityScorer _textSimilarityScorer;
    private readonly IAuditLog _auditLog;

    public CohortMatchingRunner(
        IProjectRepository projects,
        IAssignmentRepository assignments,
        IProjectInterestRepository projectInterests,
        IMatchingEngine matchingEngine,
        ITextSimilarityScorer textSimilarityScorer,
        IAuditLog auditLog)
    {
        _projects = projects;
        _assignments = assignments;
        _projectInterests = projectInterests;
        _matchingEngine = matchingEngine;
        _textSimilarityScorer = textSimilarityScorer;
        _auditLog = auditLog;
    }

    public async Task<int> RunAsync(int cohortId, Guid actorEntraObjectId, CancellationToken ct = default)
    {
        var openProjects = await _projects.GetOpenByCohortAsync(cohortId, ct);
        var eligibleCandidates = await _assignments.GetEligibleCandidatesForMatchingAsync(cohortId, ct);
        var reservedByProject = await _assignments.GetReservedOrCommittedCandidateIdsByProjectAsync(cohortId, ct);
        var performanceScores = await _assignments.GetAveragePerformanceScoresByCohortAsync(cohortId, ct);
        var interestsByCandidate = (await _projectInterests.GetByCohortAsync(cohortId, ct))
            .GroupBy(i => i.CandidateId)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<int, byte>)g.ToDictionary(i => i.ProjectId, i => i.Rating));

        // One TF-IDF corpus for the whole run — cheap to rebuild at cohort scale (30-200
        // documents) and avoids recomputing document frequencies per project x candidate pair.
        var corpus = eligibleCandidates
            .Select(c => c.Bio)
            .Concat(openProjects.Select(p => p.Description))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!)
            .ToArray();
        var textIndex = _textSimilarityScorer.Prepare(corpus);

        var proposedCount = 0;
        foreach (var project in openProjects)
        {
            var alreadyReserved = reservedByProject.TryGetValue(project.ProjectId, out var reserved)
                ? reserved
                : (IReadOnlySet<int>)new HashSet<int>();

            // Purely additive: never touches existing pending/committed assignments, and
            // never proposes a candidate already reserved on this exact project.
            var spotsRemaining = project.MaxCandidates - alreadyReserved.Count;
            if (spotsRemaining <= 0) continue;

            var matchProject = new MatchProject(
                project.ProjectId,
                project.AvailabilityNeeded,
                project.Skills.Select(s => (s.SkillId, s.IsRequired)).ToArray(),
                project.EndDate);

            var matchCandidates = eligibleCandidates
                .Where(c => !alreadyReserved.Contains(c.CandidateId))
                .Select(c => new MatchCandidate(
                    c.CandidateId,
                    c.Availability,
                    c.Skills.Select(cs => cs.SkillId).ToArray(),
                    c.GraduationDate,
                    performanceScores.TryGetValue(c.CandidateId, out var performance) ? performance : null,
                    interestsByCandidate.TryGetValue(c.CandidateId, out var interests) ? interests : null,
                    textIndex.CosineSimilarity(c.Bio, project.Description)))
                .ToArray();

            foreach (var result in _matchingEngine.RankTopMatches(matchProject, matchCandidates, topN: spotsRemaining))
            {
                var assignment = new Assignment
                {
                    ProjectId = project.ProjectId,
                    CandidateId = result.CandidateId,
                    MatchScore = result.Score,
                    MatchRationale = result.Rationale,
                    Status = AssignmentStatus.Proposed,
                };
                await _assignments.AddAsync(assignment, ct);
                proposedCount++;
            }
        }

        await _assignments.SaveChangesAsync(ct);
        await _auditLog.RecordAsync(
            actorEntraObjectId, "Cohort", cohortId.ToString(), "MatchingRun", data: new { proposedCount }, ct: ct);

        return proposedCount;
    }
}
