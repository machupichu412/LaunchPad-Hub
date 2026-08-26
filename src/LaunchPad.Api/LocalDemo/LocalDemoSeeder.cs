using LaunchPad.Application.Community;
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

        var categoryEngineering = new SkillCategory { Name = "Engineering" };
        var categoryData = new SkillCategory { Name = "Data" };
        var categoryDesign = new SkillCategory { Name = "Design" };
        var categoryCloud = new SkillCategory { Name = "Cloud" };

        var skillCSharp = new Skill { Name = "C#", SkillCategory = categoryEngineering };
        var skillReact = new Skill { Name = "React", SkillCategory = categoryEngineering };
        var skillPowerBi = new Skill { Name = "Power BI", SkillCategory = categoryData };
        var skillPython = new Skill { Name = "Python", SkillCategory = categoryData };
        var skillFigma = new Skill { Name = "Figma", SkillCategory = categoryDesign };
        var skillKubernetes = new Skill { Name = "Kubernetes", SkillCategory = categoryCloud };

        var sponsorUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "sponsor.demo@example.com", DisplayName = "Sam Sponsor" };
        var sponsor = new Sponsor { AppUser = sponsorUser, Organization = "Contoso Retail", Title = "Engineering Manager", IsActive = true };

        var sponsor2User = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "priya.shah@example.com", DisplayName = "Priya Shah" };
        var sponsor2 = new Sponsor { AppUser = sponsor2User, Organization = "Contoso Cloud", Title = "Director of Data & AI", IsActive = true };

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

        // Two more Open/Approved projects in the main cohort so the Ops "Run matching"
        // action and Projects catalog (category filter chips) have real material —
        // these deliberately stay unstaffed, unlike the one above. MaxCandidates=3
        // exercises the multi-spot gallery/request flow in a local demo.
        var project2 = new Project
        {
            Cohort = cohort,
            Sponsor = sponsor2,
            Name = "Customer Insights AI Copilot",
            Description = "Build a copilot that surfaces actionable customer insights from support tickets.",
            AvailabilityNeeded = Availability.FullTime,
            StartDate = cohort.StartDate,
            EndDate = cohort.EndDate,
            MaxCandidates = 3,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = ProjectStatus.Open,
            Skills = new List<ProjectSkill>
            {
                new() { Skill = skillPython, IsRequired = true },
                new() { Skill = skillPowerBi, IsRequired = false }
            }
        };
        var project3 = new Project
        {
            Cohort = cohort,
            Sponsor = sponsor,
            Name = "Design System Refresh",
            Description = "Modernize the shared component library and design tokens.",
            AvailabilityNeeded = Availability.PartTime,
            StartDate = cohort.StartDate,
            EndDate = cohort.EndDate,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = ProjectStatus.Open,
            Skills = new List<ProjectSkill> { new() { Skill = skillFigma, IsRequired = true } }
        };

        // One project still in Draft and one already PendingOps, so the Sponsor's new
        // "Submit for approval" button and the Ops "Project Approvals" queue both have
        // something to act on immediately.
        var project5 = new Project
        {
            Cohort = cohort,
            Sponsor = sponsor,
            Name = "Mentorship Program Refresh",
            Description = "Redesign the cross-cohort mentorship matching process.",
            AvailabilityNeeded = Availability.PartTime,
            StartDate = cohort.StartDate,
            EndDate = cohort.EndDate,
            ApprovalStatus = ProjectApprovalStatus.Draft,
            Status = ProjectStatus.Open,
            Skills = new List<ProjectSkill> { new() { Skill = skillFigma, IsRequired = true } }
        };
        var project6 = new Project
        {
            Cohort = cohort,
            Sponsor = sponsor2,
            Name = "API Gateway Migration",
            Description = "Migrate legacy REST endpoints behind a unified API gateway.",
            AvailabilityNeeded = Availability.FullTime,
            StartDate = cohort.StartDate,
            EndDate = cohort.EndDate,
            ApprovalStatus = ProjectApprovalStatus.PendingOps,
            Status = ProjectStatus.Open,
            Skills = new List<ProjectSkill> { new() { Skill = skillPython, IsRequired = true } }
        };

        // A second, lightly-populated cohort so Cohorts/Dashboard have more than one
        // card/cross-cohort number to show.
        var cohort2 = new Cohort
        {
            Program = program,
            Name = "LP-2026-Fall",
            StartDate = new DateOnly(2026, 9, 8),
            EndDate = new DateOnly(2026, 12, 12),
            Status = CohortStatus.Active
        };
        var project4 = new Project
        {
            Cohort = cohort2,
            Sponsor = sponsor2,
            Name = "Cloud Cost Optimization",
            Description = "Reduce cross-region infra spend by profiling workloads and rightsizing services.",
            AvailabilityNeeded = Availability.PartTime,
            StartDate = cohort2.StartDate,
            EndDate = cohort2.EndDate,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = ProjectStatus.Open,
            Skills = new List<ProjectSkill> { new() { Skill = skillKubernetes, IsRequired = true } }
        };
        var cohort2CandidateUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "alex.torres@example.com", DisplayName = "Alex Torres" };
        var cohort2Candidate = new Candidate
        {
            AppUser = cohort2CandidateUser,
            Cohort = cohort2,
            Location = "Austin, TX",
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
            Skills = new List<CandidateSkill> { new() { Skill = skillKubernetes, Proficiency = 3, Source = SkillSource.SelfReported } }
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
                Skills = new List<CandidateSkill>
                {
                    new() { Skill = skillReact, Proficiency = 4, Source = SkillSource.SelfReported },
                    new() { Skill = skillFigma, Proficiency = 3, Source = SkillSource.SelfReported }
                }
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
                // Shares vocabulary with project2's description ("actionable", "insights",
                // "support tickets") on purpose — makes the TF-IDF text-similarity term
                // visibly move this candidate's score in a local demo.
                Bio = "Built actionable insights dashboards from support ticket data using Python and Power BI.",
                Skills = new List<CandidateSkill>
                {
                    new() { Skill = skillPowerBi, Proficiency = 4, Source = SkillSource.OpsVerified },
                    new() { Skill = skillPython, Proficiency = 3, Source = SkillSource.SelfReported }
                }
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

        // A Proposed match sitting on the sponsor's own project so the Sponsor's
        // "Review matches" screen has something to recommend/reject immediately,
        // without first needing an Ops "Run matching" click.
        var proposedMatch = new Assignment
        {
            Project = project3,
            Candidate = candidates[0],
            MatchScore = 92m,
            MatchRationale = "Matched 1/1 required skills; availability aligned.",
            Status = AssignmentStatus.Proposed,
        };

        // A few marketplace interest ratings so the matching engine's interest nudge and
        // the "Highly rated only" marketplace filter both have real data to show.
        var interest1 = new ProjectInterest { Candidate = candidates[0], Project = project2, Rating = 4 };
        var interest2 = new ProjectInterest { Candidate = candidates[0], Project = project3, Rating = 5 };
        var interest3 = new ProjectInterest { Candidate = candidates[2], Project = project2, Rating = 5 };

        db.AddRange(categoryEngineering, categoryData, categoryDesign, categoryCloud);
        db.AddRange(program, cohort, skillCSharp, skillReact, skillPowerBi, sponsor, project, assignment);
        db.AddRange(candidates);
        db.AddRange(skillPython, skillFigma, skillKubernetes, sponsor2, project2, project3);
        db.AddRange(project5, project6);
        db.AddRange(cohort2, project4, cohort2Candidate);
        db.Add(proposedMatch);
        db.AddRange(interest1, interest2, interest3);
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

        // Attached to the "Draft dashboard wireframes" todo above — demos the optional
        // deliverable-to-todo link a candidate can set when submitting.
        var deliverable = new Deliverable
        {
            Assignment = assignment,
            ProjectTodo = todos[1],
            Title = "Dashboard Wireframes v1",
            FileName = "dashboard-wireframes-v1.pdf",
            Status = DeliverableStatus.Submitted,
            SubmittedUtc = DateTime.UtcNow.AddDays(-18),
        };

        // A Final review alongside the Midpoint one so HireOutcomeRule has something to
        // evaluate — the local in-memory demo has no real vCandidateRisk view backing
        // CandidateRisk though, so the suggestion badge itself only renders against a real
        // SQL Server target (see docker-compose.homelab.yml's LocalFull profile).
        var finalReview = new Review
        {
            Assignment = assignment,
            ReviewType = ReviewType.SponsorOnCandidate,
            Checkpoint = Checkpoint.Final,
            SubmittedBy = sponsorUser.AppUserId,
            Commitment = 5,
            Availability = 4,
            Guidance = 5,
            OutputQuality = 5,
            Comments = "Finished strong — the metrics integration shipped ahead of schedule.",
            Strengths = "Took full ownership of the API integration and mentored a newer candidate along the way.",
            GrowthAreas = "Keep building confidence presenting to stakeholders.",
            RecommendConversion = true,
        };

        var kudosPost = new CommunityPost
        {
            Author = candidateUsers[1],
            AuthorRoleLabel = "Candidate",
            PostType = CommunityPostType.Win,
            Body = "Just shipped the first version of the real-time metrics widget — really happy with how the chart animations turned out! #shipit",
            CreatedUtc = DateTime.UtcNow.AddDays(-2),
            // Matches what's actually seeded below (one comment, one reaction) — the
            // seeder writes through plain property assignment like this, not the
            // migration's raw-SQL backfill, which only ever needs to handle pre-existing
            // data on a real SQL Server target.
            CommentCount = 1,
            LikeCount = 1,
        };
        var questionPost = new CommunityPost
        {
            Author = candidateUsers[0],
            AuthorRoleLabel = "Candidate",
            PostType = CommunityPostType.Question,
            Body = "Has anyone worked with Power BI embedded tokens before? Trying to figure out the refresh flow. #help",
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
        };
        var announcementPost = new CommunityPost
        {
            Author = sponsorUser,
            AuthorRoleLabel = "Sponsor",
            PostType = CommunityPostType.Announcement,
            Body = "Midpoint reviews open next week — please have your deliverables up to date before then. Thanks for all the hard work so far! #midpointreview",
            CreatedUtc = DateTime.UtcNow.AddHours(-6),
        };

        // Wires up real Hashtag/CommunityPostHashtag rows for each #tag above, the same
        // way CommunityController.CreatePost does — so the demo has real, clickable,
        // filterable tags to click through rather than plain text that only looks tagged.
        var hashtagsByTag = new Dictionary<string, Hashtag>();
        Hashtag GetOrAddHashtag(string tag) =>
            hashtagsByTag.TryGetValue(tag, out var existing) ? existing : hashtagsByTag[tag] = new Hashtag { Tag = tag };

        foreach (var (post, body) in new[] { (kudosPost, kudosPost.Body), (questionPost, questionPost.Body), (announcementPost, announcementPost.Body) })
        {
            foreach (var tag in HashtagExtractor.Extract(body))
            {
                post.PostHashtags.Add(new CommunityPostHashtag { Post = post, Hashtag = GetOrAddHashtag(tag) });
            }
        }

        var kudosComment = new CommunityComment { Post = kudosPost, Author = sponsorUser, Body = "This looks great, nice work!", CreatedUtc = DateTime.UtcNow.AddHours(-20) };
        var kudosReaction = new CommunityPostReaction { Post = kudosPost, AppUser = sponsorUser };

        // A few notifications so the header bell has real content without needing the
        // full publish pipeline (project submit/approve/review) to fire first.
        var notifications = new[]
        {
            new Notification
            {
                RecipientAppUserId = candidateUsers[1].AppUserId,
                Subject = "Midpoint review received",
                Body = "Sam Sponsor submitted your midpoint review for Internal Dashboard Revamp.",
                CreatedUtc = DateTime.UtcNow.AddDays(-18),
                IsRead = true,
            },
            new Notification
            {
                RecipientAppUserId = candidateUsers[0].AppUserId,
                Subject = "You've been proposed for a project",
                Body = "You're a proposed match for Design System Refresh — the sponsor is reviewing candidates now.",
                CreatedUtc = DateTime.UtcNow.AddDays(-1),
                IsRead = false,
            },
            new Notification
            {
                RecipientAppUserId = sponsorUser.AppUserId,
                Subject = "New project pending approval",
                Body = "\"Mentorship Program Refresh\" is saved as a draft — submit it when you're ready for Program Ops to review.",
                CreatedUtc = DateTime.UtcNow.AddHours(-3),
                IsRead = false,
            },
        };

        db.AddRange(todos);
        db.Add(review);
        db.Add(finalReview);
        db.Add(deliverable);
        db.AddRange(kudosPost, questionPost, announcementPost);
        db.Add(kudosComment);
        db.Add(kudosReaction);
        db.AddRange(notifications);
        db.SaveChanges();
    }
}
