using LaunchPad.Application.Assignments;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class AssignmentRepository : IAssignmentRepository
{
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

    public async Task<IReadOnlyList<Deliverable>> GetDeliverablesAsync(int assignmentId, CancellationToken ct = default) =>
        await _db.Deliverables
            .Where(d => d.AssignmentId == assignmentId)
            .OrderByDescending(d => d.SubmittedUtc)
            .ToListAsync(ct);

    public async Task<Deliverable> AddDeliverableAsync(Deliverable deliverable, CancellationToken ct = default)
    {
        await _db.Deliverables.AddAsync(deliverable, ct);
        return deliverable;
    }

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

    public async Task<IReadOnlyList<int>> GetProjectIdsWithPendingMatchesAsync(int cohortId, CancellationToken ct = default) =>
        await _db.Assignments
            .Where(a => a.Project.CohortId == cohortId
                && (a.Status == AssignmentStatus.Proposed || a.Status == AssignmentStatus.SponsorApproved))
            .Select(a => a.ProjectId)
            .Distinct()
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
}
