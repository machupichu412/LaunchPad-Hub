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

    /// <summary>Status == SponsorApproved, scoped to a cohort — the Ops approval queue
    /// (what's actually actionable by Ops under the two-stage flow).</summary>
    Task<IReadOnlyList<Assignment>> GetPendingByCohortAsync(int cohortId, CancellationToken ct = default);

    /// <summary>Candidates in the cohort with no *live* (OpsApproved/Active) assignment — the matching
    /// pool. A candidate merely Proposed/SponsorApproved elsewhere is still matchable: nothing is
    /// decided yet, so they can legitimately be a top-N candidate for more than one open project.</summary>
    Task<IReadOnlyList<Candidate>> GetEligibleCandidatesForMatchingAsync(int cohortId, CancellationToken ct = default);

    /// <summary>Status IN (OpsApproved, Active) — mirrors the DB's unique filtered index exactly,
    /// so an approval can check for a conflict before hitting that constraint.</summary>
    Task<Assignment?> GetLiveAssignmentAsync(int candidateId, CancellationToken ct = default);

    /// <summary>Status == Proposed, scoped to a project — the sponsor's match-review queue for one
    /// project, and the set to auto-withdraw the losers from when the sponsor recommends a winner.</summary>
    Task<IReadOnlyList<Assignment>> GetProposedByProjectAsync(int projectId, CancellationToken ct = default);

    /// <summary>Project IDs in the cohort that already have a Proposed/SponsorApproved assignment —
    /// so a repeated "Run matching" click never piles more proposals onto a project already in review.</summary>
    Task<IReadOnlyList<int>> GetProjectIdsWithPendingMatchesAsync(int cohortId, CancellationToken ct = default);

    /// <summary>Status IN (Proposed, SponsorApproved) for a candidate, across every project — used to
    /// cascade-withdraw a candidate's other pending offers once Ops approves them somewhere.</summary>
    Task<IReadOnlyList<Assignment>> GetPendingAssignmentsForCandidateAsync(int candidateId, CancellationToken ct = default);

    /// <summary>Status IN (OpsApproved, Active, Completed) for a sponsor's own projects — the sponsor's
    /// "My Candidates" roster.</summary>
    Task<IReadOnlyList<Assignment>> GetBySponsorAsync(int sponsorId, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectTodo>> GetTodosAsync(int assignmentId, CancellationToken ct = default);
    Task<ProjectTodo?> GetTodoAsync(int assignmentId, int todoId, CancellationToken ct = default);

    Task<IReadOnlyList<Deliverable>> GetDeliverablesAsync(int assignmentId, CancellationToken ct = default);
    Task<Deliverable> AddDeliverableAsync(Deliverable deliverable, CancellationToken ct = default);

    /// <summary>Sponsor-on-candidate reviews only — never returns the reverse ReviewType.</summary>
    Task<IReadOnlyList<Review>> GetCandidateEvaluationsAsync(int assignmentId, CancellationToken ct = default);

    /// <summary>Each candidate's average OverallScore across their own SponsorOnCandidate
    /// reviews, normalized to 0-1 — the matching engine's "past performance" input. A
    /// candidate with no submitted reviews yet is simply absent from the result (their
    /// first project — the engine reweights around this, see MatchingEngine).</summary>
    Task<IReadOnlyDictionary<int, decimal>> GetAveragePerformanceScoresByCohortAsync(int cohortId, CancellationToken ct = default);
}
