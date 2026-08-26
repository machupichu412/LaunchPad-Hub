using System.Data;
using LaunchPad.Application.Assignments;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class AssignmentRepository : IAssignmentRepository
{
    private static readonly AssignmentStatus[] ReservedOrCommittedStatuses =
    {
        AssignmentStatus.Proposed, AssignmentStatus.SponsorApproved, AssignmentStatus.OpsApproved, AssignmentStatus.Active,
    };

    private static readonly AssignmentStatus[] CommittedStatuses =
    {
        AssignmentStatus.SponsorApproved, AssignmentStatus.OpsApproved, AssignmentStatus.Active,
    };

    private readonly LaunchPadDbContext _db;
    public AssignmentRepository(LaunchPadDbContext db) => _db = db;

    public Task<Assignment?> GetAsync(int assignmentId, CancellationToken ct = default) =>
        _db.Assignments
            .Include(a => a.Project).ThenInclude(p => p.Sponsor).ThenInclude(s => s.AppUser)
            .Include(a => a.Candidate).ThenInclude(c => c.AppUser)
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, ct);

    public Task<Assignment?> GetWithOwnershipDetailsAsync(int assignmentId, CancellationToken ct = default) =>
        _db.Assignments
            .Include(a => a.Candidate).ThenInclude(c => c.AppUser)
            .Include(a => a.Candidate).ThenInclude(c => c.Cohort)
            .Include(a => a.Project).ThenInclude(p => p.Sponsor).ThenInclude(s => s.AppUser)
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, ct);

    public Task<Assignment?> GetActiveByCandidateIdAsync(int candidateId, CancellationToken ct = default) =>
        _db.Assignments
            .Include(a => a.Project).ThenInclude(p => p.Sponsor).ThenInclude(s => s.AppUser)
            .Include(a => a.Project).ThenInclude(p => p.Skills).ThenInclude(ps => ps.Skill)
            .Where(a => a.CandidateId == candidateId && a.Status != AssignmentStatus.Withdrawn)
            .OrderByDescending(a => a.AssignmentId)
            .FirstOrDefaultAsync(ct);

    public async Task<Assignment> AddAsync(Assignment assignment, CancellationToken ct = default)
    {
        await _db.Assignments.AddAsync(assignment, ct);
        return assignment;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<ProjectTodo>> GetTodosAsync(int assignmentId, CancellationToken ct = default) =>
        await _db.ProjectTodos
            .Where(t => t.AssignmentId == assignmentId)
            .OrderBy(t => t.DueDate)
            .ToListAsync(ct);

    public Task<ProjectTodo?> GetTodoAsync(int assignmentId, int todoId, CancellationToken ct = default) =>
        _db.ProjectTodos.FirstOrDefaultAsync(t => t.AssignmentId == assignmentId && t.ProjectTodoId == todoId, ct);

    public async Task<ProjectTodo> AddTodoAsync(ProjectTodo todo, CancellationToken ct = default)
    {
        await _db.ProjectTodos.AddAsync(todo, ct);
        return todo;
    }

    public Task<ProjectTodo?> GetLinkedReviewTodoAsync(
        int assignmentId, ReviewType reviewType, Checkpoint checkpoint, CancellationToken ct = default) =>
        _db.ProjectTodos.FirstOrDefaultAsync(t =>
            t.AssignmentId == assignmentId && t.LinkedReviewType == reviewType && t.LinkedReviewCheckpoint == checkpoint, ct);

    public async Task<IReadOnlyList<Assignment>> GetActiveByCohortAsync(int cohortId, CancellationToken ct = default) =>
        await _db.Assignments
            .Include(a => a.Candidate).ThenInclude(c => c.AppUser)
            .Include(a => a.Project).ThenInclude(p => p.Sponsor).ThenInclude(s => s.AppUser)
            .Where(a => a.Status == AssignmentStatus.Active && a.Project.CohortId == cohortId)
            .ToListAsync(ct);

    public async Task<IReadOnlySet<(int AssignmentId, ReviewType ReviewType)>> GetLinkedReviewTodoKeysAsync(
        IReadOnlyList<int> assignmentIds, Checkpoint checkpoint, CancellationToken ct = default)
    {
        var rows = await _db.ProjectTodos
            .Where(t => assignmentIds.Contains(t.AssignmentId) && t.LinkedReviewType != null && t.LinkedReviewCheckpoint == checkpoint)
            .Select(t => new { t.AssignmentId, ReviewType = t.LinkedReviewType!.Value })
            .ToListAsync(ct);

        return rows.Select(r => (r.AssignmentId, r.ReviewType)).ToHashSet();
    }

    public async Task<IReadOnlyList<Deliverable>> GetDeliverablesAsync(int assignmentId, CancellationToken ct = default) =>
        await _db.Deliverables
            .Include(d => d.ProjectTodo)
            .Where(d => d.AssignmentId == assignmentId)
            .OrderByDescending(d => d.SubmittedUtc)
            .ToListAsync(ct);

    public async Task<Deliverable> AddDeliverableAsync(Deliverable deliverable, CancellationToken ct = default)
    {
        await _db.Deliverables.AddAsync(deliverable, ct);
        return deliverable;
    }

    public Task<Deliverable?> GetDeliverableAsync(int assignmentId, int deliverableId, CancellationToken ct = default) =>
        _db.Deliverables.FirstOrDefaultAsync(d => d.AssignmentId == assignmentId && d.DeliverableId == deliverableId, ct);

    public async Task<IReadOnlyList<Review>> GetCandidateEvaluationsAsync(int assignmentId, CancellationToken ct = default) =>
        await _db.Reviews
            .Where(r => r.AssignmentId == assignmentId && r.ReviewType == ReviewType.SponsorOnCandidate)
            .OrderByDescending(r => r.SubmittedUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Assignment>> GetPendingByCohortAsync(int cohortId, CancellationToken ct = default) =>
        await _db.Assignments
            .Include(a => a.Candidate).ThenInclude(c => c.AppUser)
            .Include(a => a.Project).ThenInclude(p => p.Sponsor).ThenInclude(s => s.AppUser)
            .Where(a => a.Project.CohortId == cohortId && a.Status == AssignmentStatus.SponsorApproved)
            .ToListAsync(ct);

    public Task<Assignment?> GetLiveAssignmentAsync(int candidateId, CancellationToken ct = default) =>
        _db.Assignments.FirstOrDefaultAsync(a =>
            a.CandidateId == candidateId
            && (a.Status == AssignmentStatus.OpsApproved || a.Status == AssignmentStatus.Active), ct);

    public async Task<IReadOnlyList<Candidate>> GetEligibleCandidatesForMatchingAsync(int cohortId, CancellationToken ct = default) =>
        await _db.Candidates
            .Include(c => c.AppUser)
            .Include(c => c.Skills).ThenInclude(cs => cs.Skill)
            .Where(c => c.CohortId == cohortId && !_db.Assignments.Any(a =>
                a.CandidateId == c.CandidateId
                && (a.Status == AssignmentStatus.OpsApproved || a.Status == AssignmentStatus.Active)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Assignment>> GetProposedByProjectAsync(int projectId, CancellationToken ct = default) =>
        await _db.Assignments
            .Include(a => a.Candidate).ThenInclude(c => c.AppUser)
            .Include(a => a.Project).ThenInclude(p => p.Sponsor).ThenInclude(s => s.AppUser)
            .Where(a => a.ProjectId == projectId && a.Status == AssignmentStatus.Proposed)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Assignment>> GetPendingAssignmentsForCandidateAsync(int candidateId, CancellationToken ct = default) =>
        await _db.Assignments
            .Where(a => a.CandidateId == candidateId
                && (a.Status == AssignmentStatus.Proposed || a.Status == AssignmentStatus.SponsorApproved))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Assignment>> GetBySponsorAsync(int sponsorId, CancellationToken ct = default) =>
        await _db.Assignments
            .Include(a => a.Candidate).ThenInclude(c => c.AppUser)
            .Include(a => a.Project)
            .Where(a => a.Project.SponsorId == sponsorId
                && (a.Status == AssignmentStatus.OpsApproved || a.Status == AssignmentStatus.Active || a.Status == AssignmentStatus.Completed))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, decimal>> GetAveragePerformanceScoresByCohortAsync(int cohortId, CancellationToken ct = default)
    {
        var averages = await _db.Reviews
            .Where(r => r.ReviewType == ReviewType.SponsorOnCandidate
                && r.Assignment.Candidate.CohortId == cohortId
                && r.OverallScore != null)
            .GroupBy(r => r.Assignment.CandidateId)
            .Select(g => new { CandidateId = g.Key, AverageScore = g.Average(r => r.OverallScore!.Value) })
            .ToListAsync(ct);

        // OverallScore is on a 1-5 scale; normalize to 0-1 for the matching engine.
        return averages.ToDictionary(x => x.CandidateId, x => Math.Clamp((x.AverageScore - 1m) / 4m, 0m, 1m));
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlySet<int>>> GetReservedOrCommittedCandidateIdsByProjectAsync(
        int cohortId, CancellationToken ct = default)
    {
        var rows = await _db.Assignments
            .Where(a => a.Project.CohortId == cohortId && ReservedOrCommittedStatuses.Contains(a.Status))
            .Select(a => new { a.ProjectId, a.CandidateId })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.ProjectId)
            .ToDictionary(g => g.Key, g => (IReadOnlySet<int>)g.Select(r => r.CandidateId).ToHashSet());
    }

    public Task<int> GetCommittedCountForProjectAsync(int projectId, CancellationToken ct = default) =>
        _db.Assignments.CountAsync(a => a.ProjectId == projectId && CommittedStatuses.Contains(a.Status), ct);

    public async Task<IReadOnlySet<int>> GetCandidateIdsWithPendingAssignmentsElsewhereAsync(
        int cohortId, int excludingProjectId, CancellationToken ct = default)
    {
        var ids = await _db.Assignments
            .Where(a => a.Project.CohortId == cohortId
                && a.ProjectId != excludingProjectId
                && (a.Status == AssignmentStatus.Proposed || a.Status == AssignmentStatus.SponsorApproved))
            .Select(a => a.CandidateId)
            .Distinct()
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    public async Task<IReadOnlyList<Assignment>> GetCommittedByProjectAsync(int projectId, CancellationToken ct = default) =>
        await _db.Assignments
            .Include(a => a.Candidate).ThenInclude(c => c.AppUser)
            .Where(a => a.ProjectId == projectId && CommittedStatuses.Contains(a.Status))
            .ToListAsync(ct);

    // Serializable isolation on a relational provider takes range locks on the read set,
    // blocking a concurrent transaction's insert into that same range until this one commits
    // or rolls back — the standard SQL Server count-then-insert concurrency guard. The
    // in-memory test provider doesn't support transactions/isolation levels at all, so this
    // falls back to an unlocked check-then-act there (fine: no real concurrency in a single
    // in-process test run).
    public async Task<SponsorDirectRequestResult> TryCreateSponsorDirectRequestAsync(
        Assignment newAssignment, CancellationToken ct = default)
    {
        var useTransaction = _db.Database.IsRelational();
        await using var transaction = useTransaction
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;

        var existingLive = await GetLiveAssignmentAsync(newAssignment.CandidateId, ct);
        if (existingLive is not null)
        {
            return new SponsorDirectRequestResult(SponsorDirectRequestOutcome.CandidateNoLongerEligible);
        }

        var maxCandidates = await _db.Projects
            .Where(p => p.ProjectId == newAssignment.ProjectId)
            .Select(p => p.MaxCandidates)
            .FirstAsync(ct);

        var reservedOrCommitted = await _db.Assignments
            .CountAsync(a => a.ProjectId == newAssignment.ProjectId && ReservedOrCommittedStatuses.Contains(a.Status), ct);

        if (reservedOrCommitted >= maxCandidates)
        {
            return new SponsorDirectRequestResult(SponsorDirectRequestOutcome.ProjectFull);
        }

        newAssignment.Status = AssignmentStatus.SponsorApproved;
        newAssignment.SponsorApprovedUtc = DateTime.UtcNow;

        await _db.Assignments.AddAsync(newAssignment, ct);
        await _db.SaveChangesAsync(ct);

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        return new SponsorDirectRequestResult(SponsorDirectRequestOutcome.Created, newAssignment);
    }

    public async Task<OpsApproveResult> TryOpsApproveAsync(int assignmentId, int opsAppUserId, CancellationToken ct = default)
    {
        var useTransaction = _db.Database.IsRelational();
        await using var transaction = useTransaction
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;

        var assignment = await _db.Assignments
            .Include(a => a.Project).ThenInclude(p => p.Sponsor).ThenInclude(s => s.AppUser)
            .Include(a => a.Candidate).ThenInclude(c => c.AppUser)
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, ct);
        if (assignment is null)
        {
            return new OpsApproveResult(OpsApproveOutcome.NotFound);
        }

        if (assignment.Status != AssignmentStatus.SponsorApproved)
        {
            return new OpsApproveResult(OpsApproveOutcome.WrongStatus);
        }

        var existingLive = await GetLiveAssignmentAsync(assignment.CandidateId, ct);
        if (existingLive is not null && existingLive.AssignmentId != assignment.AssignmentId)
        {
            return new OpsApproveResult(OpsApproveOutcome.CandidateConflict);
        }

        // Only OpsApproved/Active siblings actually consume a spot — a sibling that's merely
        // SponsorApproved (pending Ops's own review) hasn't taken anything yet, so it must
        // not block this approval; RecommendMatch's conditional cascade is what keeps the
        // SponsorApproved pile from exceeding MaxCandidates in the first place.
        var liveElsewhereOnProject = await _db.Assignments
            .CountAsync(a => a.ProjectId == assignment.ProjectId && a.AssignmentId != assignment.AssignmentId
                && (a.Status == AssignmentStatus.OpsApproved || a.Status == AssignmentStatus.Active), ct);
        if (liveElsewhereOnProject >= assignment.Project.MaxCandidates)
        {
            return new OpsApproveResult(OpsApproveOutcome.ProjectFull);
        }

        assignment.Status = AssignmentStatus.OpsApproved;
        assignment.OpsApprovedUtc = DateTime.UtcNow;
        assignment.OpsApprovedBy = opsAppUserId;
        assignment.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // The candidate is committed now — withdraw any other pending offer they hold on
        // other projects (they were still Proposed/SponsorApproved there because "eligible
        // for matching" only excludes a live commitment).
        var otherPending = await _db.Assignments
            .Where(a => a.CandidateId == assignment.CandidateId && a.AssignmentId != assignment.AssignmentId
                && (a.Status == AssignmentStatus.Proposed || a.Status == AssignmentStatus.SponsorApproved))
            .ToListAsync(ct);
        foreach (var other in otherPending)
        {
            other.Status = AssignmentStatus.Withdrawn;
        }

        await _db.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        return new OpsApproveResult(OpsApproveOutcome.Approved, assignment);
    }

    public async Task CancelProjectAssignmentsAsync(int projectId, CancellationToken ct = default)
    {
        var nonTerminal = await _db.Assignments
            .Where(a => a.ProjectId == projectId && ReservedOrCommittedStatuses.Contains(a.Status))
            .ToListAsync(ct);

        foreach (var assignment in nonTerminal)
        {
            assignment.Status = AssignmentStatus.Withdrawn;
        }
    }
}
