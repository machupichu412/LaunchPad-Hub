using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using LaunchPad.Application.Reporting;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests.Sqlite;

/// <summary>
/// The scenarios docs/testing/user-test-findings.md listed as "not verifiable locally" because
/// the InMemory provider cannot represent them: X4 (one live assignment per candidate), the
/// risk half of X6 and O5, E1's delivery tiles, and X9 (concurrent approvals).
///
/// These run on SQLite (see SqliteWebApplicationFactory) through the real HTTP stack, the real
/// repositories, and real views — not on SQL Server. What that does and does not prove is
/// spelled out on SqliteWebApplicationFactory and SqliteSchema; SqlServerOnlyBehaviorTests
/// remains the check that the shipped T-SQL is right.
/// </summary>
[Collection(SqliteCollection.Name)]
public class RelationalBehaviorTests
{
    private readonly SqliteWebApplicationFactory _factory;

    public RelationalBehaviorTests(SqliteFixture fixture) => _factory = fixture.Factory;

    private sealed record World(
        int CohortId, int ProjectA, int ProjectB, int CandidateId, Guid SponsorOid, int SponsorAppUserId);

    private async Task<World> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var tag = Guid.NewGuid().ToString("N")[..8];

        var program = new Domain.Entities.Program { Name = $"Relational {tag}", IsActive = true };
        var cohort = new Cohort
        {
            Program = program,
            Name = $"Cohort {tag}",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 6, 1),
            Status = CohortStatus.Active,
        };
        var sponsorOid = Guid.NewGuid();
        var sponsorAppUser = new AppUser { EntraObjectId = sponsorOid, Upn = $"sponsor-{tag}@example.com", DisplayName = "Relational Sponsor" };
        var sponsor = new Sponsor { AppUser = sponsorAppUser };
        var candidate = new Candidate
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = $"cand-{tag}@example.com", DisplayName = "Relational Candidate" },
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
        };
        Project NewProject(string name) => new()
        {
            Cohort = cohort,
            Sponsor = sponsor,
            Name = name,
            AvailabilityNeeded = Availability.PartTime,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = ProjectStatus.Open,
            MaxCandidates = 2,
        };
        var projectA = NewProject($"A {tag}");
        var projectB = NewProject($"B {tag}");

        db.AddRange(program, cohort, sponsorAppUser, sponsor, candidate, projectA, projectB);
        await db.SaveChangesAsync();

        return new World(cohort.CohortId, projectA.ProjectId, projectB.ProjectId,
            candidate.CandidateId, sponsorOid, sponsorAppUser.AppUserId);
    }

    private HttpClient ClientAs(string roles, Guid? oid = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, (oid ?? Guid.NewGuid()).ToString());
        return client;
    }

    // X4 — the index itself, not the application check in front of it.
    [Fact]
    public async Task TheFilteredUniqueIndexRefusesASecondLiveAssignment()
    {
        var world = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        db.Assignments.Add(new Assignment
        {
            ProjectId = world.ProjectA,
            CandidateId = world.CandidateId,
            Status = AssignmentStatus.OpsApproved, // inside the filter: Status IN (2,3)
        });
        await db.SaveChangesAsync();

        db.Assignments.Add(new Assignment
        {
            ProjectId = world.ProjectB,
            CandidateId = world.CandidateId,
            Status = AssignmentStatus.Active, // also inside the filter — must collide
        });

        var act = () => db.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<SqliteException>()
            .Which.SqliteErrorCode.Should().Be(19); // SQLITE_CONSTRAINT
    }

    // X4 — and a Proposed sibling is outside the filter, so it must still be allowed.
    [Fact]
    public async Task TheFilteredUniqueIndexAllowsASecondAssignmentThatIsNotLiveYet()
    {
        var world = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        db.Assignments.Add(new Assignment { ProjectId = world.ProjectA, CandidateId = world.CandidateId, Status = AssignmentStatus.Active });
        db.Assignments.Add(new Assignment { ProjectId = world.ProjectB, CandidateId = world.CandidateId, Status = AssignmentStatus.Proposed });

        var act = () => db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    // X4 end to end: the application check refuses first, so the index is never reached.
    [Fact]
    public async Task OpsApproveRefusesASecondLiveAssignmentWithoutHittingTheIndex()
    {
        var world = await SeedAsync();
        var second = await SeedAssignmentsForDoubleApprovalAsync(world);

        var ops = ClientAs(Roles.ProgramOps);
        var response = await ops.PostAsync($"/api/matching/{second}/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // X9 — two Ops approving the same candidate onto two projects at the same moment.
    [Fact]
    public async Task ConcurrentOpsApprovalsLeaveExactlyOneLiveAssignment()
    {
        var world = await SeedAsync();
        var (first, second) = await SeedTwoSponsorApprovedAsync(world);

        var opsOne = ClientAs(Roles.ProgramOps);
        var opsTwo = ClientAs(Roles.ProgramOps);

        var responses = await Task.WhenAll(
            opsOne.PostAsync($"/api/matching/{first}/approve", null),
            opsTwo.PostAsync($"/api/matching/{second}/approve", null));

        // Whichever order they land in, the invariant is what matters: one candidate,
        // one live assignment. A loser may be a 409 (application check) or a 500 (the
        // index catching a race the check missed) — both are acceptable outcomes here;
        // two winners never is.
        responses.Count(r => r.IsSuccessStatusCode).Should().Be(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var live = await db.Assignments.CountAsync(a =>
            a.CandidateId == world.CandidateId &&
            (a.Status == AssignmentStatus.OpsApproved || a.Status == AssignmentStatus.Active));
        live.Should().Be(1);
    }

    // X6 / O5 — the risk view, read through the real CandidateRepository.
    [Fact]
    public async Task TheRiskViewFlagsAFallingScoreForOpsAndStaysHiddenFromTheSponsor()
    {
        var world = await SeedAsync();
        var assignmentId = await AddAssignmentAsync(world.ProjectA, world.CandidateId, AssignmentStatus.Active);
        // Midpoint 4.0, final 2.0: below the 3.0 average line and a drop of more than 0.5.
        await AddSponsorReviewAsync(assignmentId, world.SponsorAppUserId, Checkpoint.Midpoint, 4);
        await AddSponsorReviewAsync(assignmentId, world.SponsorAppUserId, Checkpoint.Final, 2);

        var ops = ClientAs(Roles.ProgramOps);
        var opsView = await ops.GetFromJsonAsync<CandidateDto>($"/api/candidates/{world.CandidateId}", TestJsonOptions.Default);

        opsView!.AverageScore.Should().Be(3.0m);
        opsView.HasPerformanceRisk.Should().BeTrue();

        // Same row, same view, through the same mapper — the sponsor gets nothing.
        var sponsor = ClientAs(Roles.Sponsor, world.SponsorOid);
        var sponsorResponse = await sponsor.GetAsync($"/api/candidates/{world.CandidateId}");
        sponsorResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sponsorBody = (await sponsorResponse.Content.ReadAsStringAsync()).ToLowerInvariant();
        sponsorBody.Should().NotContain("averagescore", "a sponsor must never receive a score");
        sponsorBody.Should().NotContain("hasperformancerisk");
        sponsorBody.Should().NotContain("hasengagementrisk");
    }

    // X6 / O5 — the other half of the view: engagement risk from overdue to-dos.
    [Fact]
    public async Task TheRiskViewFlagsStaleTodosAsEngagementRisk()
    {
        var world = await SeedAsync();
        var assignmentId = await AddAssignmentAsync(world.ProjectA, world.CandidateId, AssignmentStatus.Active);
        await AddOverdueTodosAsync(assignmentId, count: 3);

        var ops = ClientAs(Roles.ProgramOps);
        var opsView = await ops.GetFromJsonAsync<CandidateDto>($"/api/candidates/{world.CandidateId}", TestJsonOptions.Default);

        opsView!.HasEngagementRisk.Should().BeTrue();
        opsView.HasPerformanceRisk.Should().BeFalse("no review has been submitted, so there is no score to be at risk on");

        // The count behind the flag, straight from the view.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var risk = await db.CandidateRisks.SingleAsync(r => r.CandidateId == world.CandidateId);
        risk.StaleTodoCount.Should().Be(3);
    }

    // E1 — the delivery KPI view behind the Executive tiles.
    [Fact]
    public async Task TheDeliveryKpiViewFeedsTheExecutiveTilesAndSkipsCancelledProjects()
    {
        var world = await SeedAsync();
        await SetDeliveryStageAsync(world.ProjectA, ProjectDeliveryStage.PilotReady);
        await SetDeliveryStageAsync(world.ProjectB, ProjectDeliveryStage.MvpBuilt);
        var cancelled = await AddProjectAsync(world, ProjectDeliveryStage.BusinessValueDocumented, ProjectStatus.Cancelled);

        var exec = ClientAs(Roles.Executive);
        var dashboard = await exec.GetFromJsonAsync<ExecutiveDashboardDto>(
            $"/api/ops/executive-dashboard/{world.CohortId}", TestJsonOptions.Default);

        dashboard!.ProjectCount.Should().Be(2, "the cancelled project is excluded by the view");
        dashboard.MvpCompleteCount.Should().Be(2);
        dashboard.PilotReadyCount.Should().Be(1);
        dashboard.BusinessValueDocumentedCount.Should().Be(0, "only the cancelled project reached that stage");
        cancelled.Should().BeGreaterThan(0);
    }

    // --- seeding helpers -------------------------------------------------------------

    private async Task<int> AddAssignmentAsync(int projectId, int candidateId, AssignmentStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var assignment = new Assignment { ProjectId = projectId, CandidateId = candidateId, Status = status };
        db.Add(assignment);
        await db.SaveChangesAsync();
        return assignment.AssignmentId;
    }

    private async Task<int> SeedAssignmentsForDoubleApprovalAsync(World world)
    {
        await AddAssignmentAsync(world.ProjectA, world.CandidateId, AssignmentStatus.Active);
        return await AddAssignmentAsync(world.ProjectB, world.CandidateId, AssignmentStatus.SponsorApproved);
    }

    private async Task<(int First, int Second)> SeedTwoSponsorApprovedAsync(World world) =>
        (await AddAssignmentAsync(world.ProjectA, world.CandidateId, AssignmentStatus.SponsorApproved),
         await AddAssignmentAsync(world.ProjectB, world.CandidateId, AssignmentStatus.SponsorApproved));

    private async Task AddSponsorReviewAsync(int assignmentId, int submittedBy, Checkpoint checkpoint, byte score)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        db.Reviews.Add(new Review
        {
            AssignmentId = assignmentId,
            ReviewType = ReviewType.SponsorOnCandidate,
            Checkpoint = checkpoint,
            SubmittedBy = submittedBy,
            Commitment = score,
            Availability = score,
            Guidance = score,
            OutputQuality = score,
        });
        await db.SaveChangesAsync();
    }

    private async Task AddOverdueTodosAsync(int assignmentId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var overdue = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);
        for (var i = 0; i < count; i++)
        {
            db.ProjectTodos.Add(new ProjectTodo
            {
                AssignmentId = assignmentId,
                Title = $"Overdue {i}",
                Status = TodoStatus.NotStarted,
                DueDate = overdue,
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task SetDeliveryStageAsync(int projectId, ProjectDeliveryStage stage)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var project = await db.Projects.SingleAsync(p => p.ProjectId == projectId);
        project.DeliveryStage = stage;
        await db.SaveChangesAsync();
    }

    private async Task<int> AddProjectAsync(World world, ProjectDeliveryStage stage, ProjectStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var sponsor = await db.Sponsors.SingleAsync(s => s.AppUserId == world.SponsorAppUserId);
        var project = new Project
        {
            CohortId = world.CohortId,
            SponsorId = sponsor.SponsorId,
            Name = $"Extra {Guid.NewGuid():N}",
            AvailabilityNeeded = Availability.PartTime,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = status,
            DeliveryStage = stage,
        };
        db.Add(project);
        await db.SaveChangesAsync();
        return project.ProjectId;
    }
}
