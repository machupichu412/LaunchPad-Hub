using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Api.LocalDemo;

/// <summary>
/// Populates the in-memory DbContext with enough data to make a local demo worth
/// looking at. Only ever runs when Database:UseInMemoryForLocalDemo is set — never
/// touches a real SQL Server, so it's safe to leave the call in Program.cs.
/// </summary>
public static class LocalDemoSeeder
{
    public static void Seed(LaunchPadDbContext db)
    {
        if (db.Programs.Any())
        {
            return;
        }

        var program = new LaunchPad.Domain.Entities.Program { Name = "LaunchPad Demo Program", IsActive = true };
        var cohort = new Cohort
        {
            Program = program,
            Name = "LP-2026-Demo",
            StartDate = new DateOnly(2026, 1, 5),
            EndDate = new DateOnly(2026, 4, 30),
            Status = CohortStatus.Active
        };

        var skillCSharp = new Skill { Name = "C#", Category = "Engineering" };
        var skillReact = new Skill { Name = "React", Category = "Engineering" };
        var skillPowerBi = new Skill { Name = "Power BI", Category = "Data" };

        var sponsorUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "sponsor.demo@example.com", DisplayName = "Sam Sponsor" };
        var sponsor = new Sponsor { AppUser = sponsorUser, Organization = "Contoso Retail", Title = "Engineering Manager", IsActive = true };

        var project = new Project
        {
            Cohort = cohort,
            Sponsor = sponsor,
            Name = "Internal Dashboard Revamp",
            Description = "Rebuild the internal ops dashboard with real-time metrics.",
            AvailabilityNeeded = Availability.PartTime,
            StartDate = cohort.StartDate,
            EndDate = cohort.EndDate,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = ProjectStatus.InProgress,
            Skills = new List<ProjectSkill>
            {
                new() { Skill = skillReact, IsRequired = true },
                new() { Skill = skillPowerBi, IsRequired = false }
            }
        };

        var candidateUsers = new[]
        {
            new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "jordan.rivera@example.com", DisplayName = "Jordan Rivera" },
            new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "casey.kim@example.com", DisplayName = "Casey Kim" },
            new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "morgan.lee@example.com", DisplayName = "Morgan Lee" }
        };

        var candidates = new[]
        {
            new Candidate
            {
                AppUser = candidateUsers[0], Cohort = cohort, Location = "Remote",
                Availability = Availability.PartTime, Status = CandidateStatus.InProgress,
                Skills = new List<CandidateSkill> { new() { Skill = skillReact, Proficiency = 4, Source = SkillSource.SelfReported } }
            },
            new Candidate
            {
                AppUser = candidateUsers[1], Cohort = cohort, Location = "Chicago, IL",
                Availability = Availability.PartTime, Status = CandidateStatus.Hire,
                Skills = new List<CandidateSkill>
                {
                    new() { Skill = skillReact, Proficiency = 5, Source = SkillSource.ResumeParsed },
                    new() { Skill = skillCSharp, Proficiency = 3, Source = SkillSource.SelfReported }
                }
            },
            new Candidate
            {
                AppUser = candidateUsers[2], Cohort = cohort, Location = "Remote",
                Availability = Availability.FullTime, Status = CandidateStatus.TalentPlus,
                Skills = new List<CandidateSkill> { new() { Skill = skillPowerBi, Proficiency = 4, Source = SkillSource.OpsVerified } }
            }
        };

        var assignment = new Assignment
        {
            Project = project,
            Candidate = candidates[1],
            MatchScore = 88.5m,
            MatchRationale = "Matched 1/1 required and 0/1 preferred skills; availability aligned.",
            Status = AssignmentStatus.Active,
            StartDate = cohort.StartDate
        };

        db.AddRange(program, cohort, skillCSharp, skillReact, skillPowerBi, sponsor, project, assignment);
        db.AddRange(candidates);
        db.SaveChanges();

        // Second wave: attach richer downstream data to the one fully-wired assignment
        // (Casey Kim on Internal Dashboard Revamp) so the candidate experience pages
        // have something real to render. AssignmentId/AppUserId are only populated
        // after the first SaveChanges, hence the two phases.
        var todos = new[]
        {
            new ProjectTodo { Assignment = assignment, Title = "Set up local dev environment", Status = TodoStatus.Completed, Priority = TodoPriority.Medium, DueDate = cohort.StartDate.AddDays(3), CompletedUtc = DateTime.UtcNow.AddDays(-30) },
            new ProjectTodo { Assignment = assignment, Title = "Draft dashboard wireframes", Status = TodoStatus.Completed, Priority = TodoPriority.High, DueDate = cohort.StartDate.AddDays(10), CompletedUtc = DateTime.UtcNow.AddDays(-18) },
            new ProjectTodo { Assignment = assignment, Title = "Build real-time metrics API integration", Status = TodoStatus.InProgress, Priority = TodoPriority.High, DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)) },
            new ProjectTodo { Assignment = assignment, Title = "Write end-to-end tests for dashboard widgets", Status = TodoStatus.NotStarted, Priority = TodoPriority.Medium, DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)) },
            new ProjectTodo { Assignment = assignment, Title = "Present midpoint demo to sponsor", Status = TodoStatus.NotStarted, Priority = TodoPriority.Low, DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)) },
        };

        var review = new Review
        {
            Assignment = assignment,
            ReviewType = ReviewType.SponsorOnCandidate,
            Checkpoint = Checkpoint.Midpoint,
            SubmittedBy = sponsorUser.AppUserId,
            Commitment = 4,
            Availability = 4,
            Guidance = 5,
            OutputQuality = 4,
            Comments = "Strong start to the engagement.",
            Strengths = "Picks up new frameworks quickly, communicates blockers early, and the wireframes were well thought out.",
            GrowthAreas = "Could write more test coverage before moving to the next task.",
            RecommendConversion = true,
        };

        var deliverable = new Deliverable
        {
            Assignment = assignment,
            Title = "Dashboard Wireframes v1",
            FileName = "dashboard-wireframes-v1.pdf",
            Status = DeliverableStatus.Submitted,
            SubmittedUtc = DateTime.UtcNow.AddDays(-18),
        };

        var kudosPost = new CommunityPost
        {
            Author = candidateUsers[1],
            AuthorRoleLabel = "Candidate",
            PostType = CommunityPostType.Win,
            Body = "Just shipped the first version of the real-time metrics widget — really happy with how the chart animations turned out!",
            CreatedUtc = DateTime.UtcNow.AddDays(-2),
        };
        var questionPost = new CommunityPost
        {
            Author = candidateUsers[0],
            AuthorRoleLabel = "Candidate",
            PostType = CommunityPostType.Question,
            Body = "Has anyone worked with Power BI embedded tokens before? Trying to figure out the refresh flow.",
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
        };
        var announcementPost = new CommunityPost
        {
            Author = sponsorUser,
            AuthorRoleLabel = "Sponsor",
            PostType = CommunityPostType.Announcement,
            Body = "Midpoint reviews open next week — please have your deliverables up to date before then. Thanks for all the hard work so far!",
            CreatedUtc = DateTime.UtcNow.AddHours(-6),
        };

        var kudosComment = new CommunityComment { Post = kudosPost, Author = sponsorUser, Body = "This looks great, nice work!", CreatedUtc = DateTime.UtcNow.AddHours(-20) };
        var kudosReaction = new CommunityPostReaction { Post = kudosPost, AppUser = sponsorUser };

        db.AddRange(todos);
        db.Add(review);
        db.Add(deliverable);
        db.AddRange(kudosPost, questionPost, announcementPost);
        db.Add(kudosComment);
        db.Add(kudosReaction);
        db.SaveChanges();
    }
}
