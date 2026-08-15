using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Assignments;

public interface IAssignmentRepository
{
    Task<Assignment?> GetAsync(int assignmentId, CancellationToken ct = default);

    /// <summary>Includes Candidate.AppUser, Candidate.Cohort, and Project.Sponsor.AppUser —
    /// the first two required by OwnsAssignmentHandler to check EntraObjectId, Candidate.Cohort
    /// also needed by SubmitDeliverable's synchronous self-heal path for the cohort name.</summary>
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

    /// <summary>Status IN (Proposed, SponsorApproved) for a candidate, across every project — used to
    /// cascade-withdraw a candidate's other pending offers once Ops approves them somewhere.</summary>
    Task<IReadOnlyList<Assignment>> GetPendingAssignmentsForCandidateAsync(int candidateId, CancellationToken ct = default);

    /// <summary>Status IN (OpsApproved, Active, Completed) for a sponsor's own projects — the sponsor's
    /// "My Candidates" roster.</summary>
    Task<IReadOnlyList<Assignment>> GetBySponsorAsync(int sponsorId, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectTodo>> GetTodosAsync(int assignmentId, CancellationToken ct = default);
    Task<ProjectTodo?> GetTodoAsync(int assignmentId, int todoId, CancellationToken ct = default);
    Task<ProjectTodo> AddTodoAsync(ProjectTodo todo, CancellationToken ct = default);

    Task<IReadOnlyList<Deliverable>> GetDeliverablesAsync(int assignmentId, CancellationToken ct = default);
    Task<Deliverable> AddDeliverableAsync(Deliverable deliverable, CancellationToken ct = default);

    /// <summary>One deliverable scoped to a specific assignment — mirrors GetTodoAsync's
    /// shape, used by the download-proxy endpoint.</summary>
    Task<Deliverable?> GetDeliverableAsync(int assignmentId, int deliverableId, CancellationToken ct = default);

    /// <summary>Sponsor-on-candidate reviews only — never returns the reverse ReviewType.</summary>
    Task<IReadOnlyList<Review>> GetCandidateEvaluationsAsync(int assignmentId, CancellationToken ct = default);

    /// <summary>Each candidate's average OverallScore across their own SponsorOnCandidate
    /// reviews, normalized to 0-1 — the matching engine's "past performance" input. A
    /// candidate with no submitted reviews yet is simply absent from the result (their
    /// first project — the engine reweights around this, see MatchingEngine).</summary>
    Task<IReadOnlyDictionary<int, decimal>> GetAveragePerformanceScoresByCohortAsync(int cohortId, CancellationToken ct = default);

    /// <summary>Per-project set of candidate IDs already Proposed/SponsorApproved/OpsApproved/
    /// Active on that project. Count() gives spotsRemaining for a cohort-wide matching run;
    /// the set itself excludes those candidates from being re-proposed to the same project on
    /// a repeat "Run matching" click. Unlocked — a stale read here produces at worst an extra
    /// Proposed row, never real overbooking (the locked chokepoints below enforce that).</summary>
    Task<IReadOnlyDictionary<int, IReadOnlySet<int>>> GetReservedOrCommittedCandidateIdsByProjectAsync(
        int cohortId, CancellationToken ct = default);

    /// <summary>Count of assignments IN (SponsorApproved, OpsApproved, Active) for one project —
    /// unlocked, used to block shrinking Project.MaxCandidates below what's already committed,
    /// and by the sponsor-recommend cascade (only withdraw remaining Proposed siblings once
    /// this recommend actually fills the last spot).</summary>
    Task<int> GetCommittedCountForProjectAsync(int projectId, CancellationToken ct = default);

    /// <summary>Candidate IDs in the cohort with a Proposed/SponsorApproved assignment on any
    /// project other than excludingProjectId — the "pending on another project" badge for the
    /// sponsor's eligible-candidates gallery.</summary>
    Task<IReadOnlySet<int>> GetCandidateIdsWithPendingAssignmentsElsewhereAsync(
        int cohortId, int excludingProjectId, CancellationToken ct = default);

    /// <summary>Status IN (SponsorApproved, OpsApproved, Active) for one project, with
    /// Candidate.AppUser included — the "Assigned candidates" section on a project's page.</summary>
    Task<IReadOnlyList<Assignment>> GetCommittedByProjectAsync(int projectId, CancellationToken ct = default);

    /// <summary>Atomically creates a sponsor's direct assignment request at SponsorApproved,
    /// rechecking the candidate's live-assignment status and the project's remaining spots
    /// under a transaction (serializable isolation on a relational provider) immediately before
    /// insert — closes the TOCTOU gap two concurrent sponsor requests would otherwise hit.
    /// newAssignment.ProjectId/CandidateId must be set; Status/SponsorApprovedUtc/By are
    /// overwritten by this method regardless of what the caller passed in.</summary>
    Task<SponsorDirectRequestResult> TryCreateSponsorDirectRequestAsync(
        Assignment newAssignment, CancellationToken ct = default);

    /// <summary>Atomically approves a SponsorApproved assignment to OpsApproved: rechecks the
    /// candidate-conflict (mirrors the DB's UX_Assignment_Active filtered index) and the
    /// project's committed-spot count under the same transaction as TryCreateSponsorDirectRequestAsync,
    /// sets StartDate, and cascades-withdraws the candidate's other pending offers elsewhere.
    /// Replaces the logic previously inline in MatchingController.Approve.</summary>
    Task<OpsApproveResult> TryOpsApproveAsync(int assignmentId, int opsAppUserId, CancellationToken ct = default);

    /// <summary>Bulk-transitions every non-terminal (Proposed/SponsorApproved/OpsApproved/Active)
    /// assignment on a project to Withdrawn — the "project cancellation frees its candidates"
    /// behavior. Mutates tracked entities only; caller SaveChangesAsync's alongside the
    /// project's own Status change in one transaction.</summary>
    Task CancelProjectAssignmentsAsync(int projectId, CancellationToken ct = default);
}
