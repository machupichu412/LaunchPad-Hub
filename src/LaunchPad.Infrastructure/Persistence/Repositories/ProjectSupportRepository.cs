using LaunchPad.Application.Common;
using LaunchPad.Application.ProjectSupport;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace LaunchPad.Infrastructure.Persistence.Repositories;

public class ProjectSupportRepository(LaunchPadDbContext db, ICurrentUser user) : IProjectSupportRepository
{
    private IQueryable<ProjectExpert> MyExpertProjects => db.Set<ProjectExpert>().Where(x =>
        x.AppUser.EntraObjectId == user.EntraObjectId && x.AppUser.IsActive);
    private IQueryable<Assignment> MyAssignments => db.Assignments.Where(x =>
        x.Candidate.AppUser.EntraObjectId == user.EntraObjectId && x.Candidate.AppUser.IsActive &&
        (x.Status == AssignmentStatus.OpsApproved || x.Status == AssignmentStatus.Active || x.Status == AssignmentStatus.Completed));
    public async Task<bool> IsExpertAsync(int projectId, CancellationToken ct) =>
        user.IsInRole(Roles.SubjectMatterExpert) && await MyExpertProjects.AnyAsync(x => x.ProjectId == projectId, ct);
    public async Task<bool> IsCandidateAsync(int projectId, CancellationToken ct) =>
        user.IsInRole(Roles.Candidate) && await MyAssignments.AnyAsync(x => x.ProjectId == projectId, ct);
    public async Task<bool> CanAccessAsync(int projectId, CancellationToken ct) =>
        user.IsInRole(Roles.ProgramOps) || await IsExpertAsync(projectId, ct) || await IsCandidateAsync(projectId, ct);
    public async Task<IReadOnlyList<SupportProjectDto>> ProjectsAsync(CancellationToken ct)
    {
        var projects = db.Projects.AsNoTracking();
        if (!user.IsInRole(Roles.ProgramOps)) projects = projects.Where(p =>
            (user.IsInRole(Roles.SubjectMatterExpert) && MyExpertProjects.Any(x => x.ProjectId == p.ProjectId)) ||
            (user.IsInRole(Roles.Candidate) && MyAssignments.Any(x => x.ProjectId == p.ProjectId)));
        return await projects.OrderBy(x => x.Name).Select(x => new SupportProjectDto(x.ProjectId, x.Name, x.Description)).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<ExpertDto>> ExpertsAsync(int? projectId, CancellationToken ct)
    {
        var users = db.AppUsers.Where(x => x.IsActive && x.IsSubjectMatterExpert);
        if (projectId.HasValue) users = db.Set<ProjectExpert>().Where(x => x.ProjectId == projectId).Select(x => x.AppUser);
        return await users.OrderBy(x => x.DisplayName).Select(x => new ExpertDto(x.AppUserId, x.DisplayName)).ToListAsync(ct);
    }
    public async Task<bool> SetExpertAsync(int projectId, int userId, bool assigned, CancellationToken ct)
    {
        if (!await db.Projects.AnyAsync(x => x.ProjectId == projectId, ct)) return false;
        var row = await db.Set<ProjectExpert>().FindAsync([projectId, userId], ct);
        if (assigned)
        {
            if (!await db.AppUsers.AnyAsync(x => x.AppUserId == userId && x.IsActive && x.IsSubjectMatterExpert, ct)) return false;
            if (row is null) db.Add(new ProjectExpert { ProjectId = projectId, AppUserId = userId });
        }
        else if (row is not null) db.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }
    public async Task<IReadOnlyList<QuestionDto>> QuestionsAsync(int projectId, CancellationToken ct) =>
        await db.Set<ProjectQuestion>().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedUtc)
            .Select(x => new QuestionDto(x.ProjectQuestionId, x.Title, x.Body, x.Author.DisplayName, x.CreatedUtc, x.DeliverableId,
                x.Answers.OrderBy(a => a.CreatedUtc).Select(a => new AnswerDto(a.ProjectAnswerId, a.Author.DisplayName, a.Body, a.CreatedUtc, a.FileName)).ToList()))
            .ToListAsync(ct);
    public Task<ProjectQuestion?> QuestionAsync(int projectId, int questionId, CancellationToken ct) =>
        db.Set<ProjectQuestion>().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.ProjectQuestionId == questionId, ct);
    public async Task AddQuestionAsync(ProjectQuestion question, CancellationToken ct) { db.Add(question); await db.SaveChangesAsync(ct); }
    public async Task AddAnswerAsync(ProjectAnswer answer, CancellationToken ct) { db.Add(answer); await db.SaveChangesAsync(ct); }
    public Task<ProjectAnswer?> AnswerAsync(int projectId, int questionId, int answerId, CancellationToken ct) =>
        db.Set<ProjectAnswer>().SingleOrDefaultAsync(x => x.ProjectAnswerId == answerId && x.ProjectQuestionId == questionId && x.Question.ProjectId == projectId, ct);
    public async Task<IReadOnlyList<SupportDeliverableDto>> DeliverablesAsync(int projectId, CancellationToken ct) =>
        await db.Deliverables.Where(x => x.Assignment.ProjectId == projectId).OrderByDescending(x => x.SubmittedUtc)
            .Select(x => new SupportDeliverableDto(x.DeliverableId, x.Title, x.FileName, x.Assignment.Candidate.AppUser.DisplayName, x.SubmittedUtc)).ToListAsync(ct);
    public Task<Deliverable?> DeliverableAsync(int projectId, int deliverableId, CancellationToken ct) =>
        db.Deliverables.SingleOrDefaultAsync(x => x.DeliverableId == deliverableId && x.Assignment.ProjectId == projectId, ct);
    public Task<Project?> ProjectAsync(int projectId, CancellationToken ct) => db.Projects.Include(x => x.Cohort).SingleOrDefaultAsync(x => x.ProjectId == projectId, ct);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
