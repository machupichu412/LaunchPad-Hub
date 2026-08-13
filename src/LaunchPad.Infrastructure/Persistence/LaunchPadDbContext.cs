using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence;

public class LaunchPadDbContext : DbContext
{
    public LaunchPadDbContext(DbContextOptions<LaunchPadDbContext> options) : base(options)
    {
    }

    public DbSet<Program> Programs => Set<Program>();
    public DbSet<Cohort> Cohorts => Set<Cohort>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<SkillCategory> SkillCategories => Set<SkillCategory>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<CandidateSkill> CandidateSkills => Set<CandidateSkill>();
    public DbSet<ProjectSkill> ProjectSkills => Set<ProjectSkill>();
    public DbSet<ProjectInterest> ProjectInterests => Set<ProjectInterest>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ProjectTodo> ProjectTodos => Set<ProjectTodo>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Deliverable> Deliverables => Set<Deliverable>();
    public DbSet<CommunityPost> CommunityPosts => Set<CommunityPost>();
    public DbSet<CommunityComment> CommunityComments => Set<CommunityComment>();
    public DbSet<CommunityPostReaction> CommunityPostReactions => Set<CommunityPostReaction>();

    // Keyless read model backed by the dbo.vCandidateRisk view — never write through this.
    public DbSet<CandidateRisk> CandidateRisks => Set<CandidateRisk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LaunchPadDbContext).Assembly);
    }
}
