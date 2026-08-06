using LaunchPad.Application.Projects;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly LaunchPadDbContext _db;
    public ProjectRepository(LaunchPadDbContext db) => _db = db;

    public Task<Project?> GetWithSponsorAsync(int projectId, CancellationToken ct = default) =>
        _db.Projects
            .Include(p => p.Sponsor).ThenInclude(s => s.AppUser)
            .Include(p => p.Skills).ThenInclude(ps => ps.Skill).ThenInclude(s => s.SkillCategory)
            .FirstOrDefaultAsync(p => p.ProjectId == projectId, ct);

    public async Task<IReadOnlyList<Project>> GetByCohortAsync(int cohortId, CancellationToken ct = default) =>
        await _db.Projects
            .Include(p => p.Sponsor).ThenInclude(s => s.AppUser)
            .Include(p => p.Skills).ThenInclude(ps => ps.Skill).ThenInclude(s => s.SkillCategory)
            .Where(p => p.CohortId == cohortId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Project>> GetOpenByCohortAsync(int cohortId, CancellationToken ct = default) =>
        await _db.Projects
            .Include(p => p.Sponsor).ThenInclude(s => s.AppUser)
            .Include(p => p.Skills).ThenInclude(ps => ps.Skill).ThenInclude(s => s.SkillCategory)
            .Where(p => p.CohortId == cohortId && p.ApprovalStatus == ProjectApprovalStatus.Approved && p.Status == ProjectStatus.Open)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Project>> GetBySponsorAsync(int sponsorId, CancellationToken ct = default) =>
        await _db.Projects
            .Include(p => p.Sponsor).ThenInclude(s => s.AppUser)
            .Include(p => p.Skills).ThenInclude(ps => ps.Skill).ThenInclude(s => s.SkillCategory)
            .Where(p => p.SponsorId == sponsorId)
            .ToListAsync(ct);

    public async Task<Project> AddAsync(Project project, CancellationToken ct = default)
    {
        await _db.Projects.AddAsync(project, ct);
        return project;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
