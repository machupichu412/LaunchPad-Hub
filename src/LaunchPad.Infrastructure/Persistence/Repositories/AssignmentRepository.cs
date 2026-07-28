using LaunchPad.Application.Assignments;
using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class AssignmentRepository : IAssignmentRepository
{
    private readonly LaunchPadDbContext _db;
    public AssignmentRepository(LaunchPadDbContext db) => _db = db;

    public Task<Assignment?> GetAsync(int assignmentId, CancellationToken ct = default) =>
        _db.Assignments
            .Include(a => a.Project)
            .Include(a => a.Candidate)
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, ct);

    public async Task<Assignment> AddAsync(Assignment assignment, CancellationToken ct = default)
    {
        await _db.Assignments.AddAsync(assignment, ct);
        return assignment;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
