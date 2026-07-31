using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Matching;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Program Ops's admin fast path: run matching -> approve/deny directly from
/// Proposed (no separate Sponsor-recommend stage in this pass — see the build-out
/// plan). All three actions are ProgramOps-only (Policies.ApproveMatch).
/// </summary>
public class MatchingControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public MatchingControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(int CohortId, int ProjectId, int CandidateId)> SeedMatchingScenarioAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var program = new Domain.Entities.Program { Name = "Test Program" };
        var cohort = new Cohort { Program = program, Name = "Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "sponsor@example.com", DisplayName = "Test Sponsor" }, Organization = "Test Org" };
        var skill = new Skill { Name = "React", Category = "Engineering" };

        var project = new Project
        {
            Cohort = cohort,
            Sponsor = sponsor,
            Name = "Test Project",
            AvailabilityNeeded = Availability.PartTime,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = ProjectStatus.Open,
            Skills = new List<ProjectSkill> { new() { Skill = skill, IsRequired = true } },
        };

        var candidate = new Candidate
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "candidate@example.com", DisplayName = "Test Candidate" },
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
            Skills = new List<CandidateSkill> { new() { Skill = skill, Proficiency = 4, Source = SkillSource.SelfReported } },
        };

        db.AddRange(program, cohort, sponsor, skill, project, candidate);
        await db.SaveChangesAsync();

        return (cohort.CohortId, project.ProjectId, candidate.CandidateId);
    }

    [Fact]
    public async Task Run_AsProgramOps_ProposesAMatch_ThenAppearsInQueue()
    {
        var (cohortId, projectId, candidateId) = await SeedMatchingScenarioAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var runResponse = await client.PostAsync($"/api/matching/run?cohortId={cohortId}", content: null);
        runResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var runResult = await runResponse.Content.ReadFromJsonAsync<RunMatchingResult>(TestJsonOptions.Default);
        runResult!.ProposedCount.Should().Be(1);

        var queueResponse = await client.GetAsync($"/api/matching/queue?cohortId={cohortId}");
        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await queueResponse.Content.ReadFromJsonAsync<List<PendingAssignmentDto>>(TestJsonOptions.Default);
        var pending = queue!.Single();
        pending.ProjectId.Should().Be(projectId);
        pending.CandidateId.Should().Be(candidateId);
        pending.MatchScore.Should().NotBeNull();
    }

    [Fact]
    public async Task Run_AsNonProgramOps_IsForbidden()
    {
        var (cohortId, _, _) = await SeedMatchingScenarioAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var response = await client.PostAsync($"/api/matching/run?cohortId={cohortId}", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approve_TransitionsToOpsApproved()
    {
        var (cohortId, _, _) = await SeedMatchingScenarioAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        await client.PostAsync($"/api/matching/run?cohortId={cohortId}", content: null);
        var queue = await (await client.GetAsync($"/api/matching/queue?cohortId={cohortId}")).Content.ReadFromJsonAsync<List<PendingAssignmentDto>>(TestJsonOptions.Default);
        var assignmentId = queue!.Single().AssignmentId;

        var approveResponse = await client.PostAsync($"/api/matching/{assignmentId}/approve", content: null);

        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var queueAfter = await (await client.GetAsync($"/api/matching/queue?cohortId={cohortId}")).Content.ReadFromJsonAsync<List<PendingAssignmentDto>>(TestJsonOptions.Default);
        queueAfter.Should().BeEmpty("an approved assignment is no longer Proposed, so it drops out of the queue");
    }

    [Fact]
    public async Task Deny_ReturnsCandidateToThePool()
    {
        var (cohortId, _, _) = await SeedMatchingScenarioAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        await client.PostAsync($"/api/matching/run?cohortId={cohortId}", content: null);
        var queue = await (await client.GetAsync($"/api/matching/queue?cohortId={cohortId}")).Content.ReadFromJsonAsync<List<PendingAssignmentDto>>(TestJsonOptions.Default);
        var assignmentId = queue!.Single().AssignmentId;

        var denyResponse = await client.PostAsync($"/api/matching/{assignmentId}/deny", content: null);

        denyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var queueAfter = await (await client.GetAsync($"/api/matching/queue?cohortId={cohortId}")).Content.ReadFromJsonAsync<List<PendingAssignmentDto>>(TestJsonOptions.Default);
        queueAfter.Should().BeEmpty();
    }

    [Fact]
    public async Task Approve_WhenCandidateAlreadyHasALiveAssignmentElsewhere_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var program = new Domain.Entities.Program { Name = "Conflict Program" };
        var cohort = new Cohort { Program = program, Name = "Conflict Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "sponsor2@example.com", DisplayName = "Sponsor Two" } };
        var candidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "busy@example.com", DisplayName = "Busy Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var projectA = new Project { Cohort = cohort, Sponsor = sponsor, Name = "Project A", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.InProgress };
        var projectB = new Project { Cohort = cohort, Sponsor = sponsor, Name = "Project B", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.Open };

        db.AddRange(program, cohort, sponsor, candidate, projectA, projectB);
        await db.SaveChangesAsync();

        var alreadyActive = new Assignment { ProjectId = projectA.ProjectId, CandidateId = candidate.CandidateId, Status = AssignmentStatus.Active };
        var newlyProposed = new Assignment { ProjectId = projectB.ProjectId, CandidateId = candidate.CandidateId, Status = AssignmentStatus.Proposed };
        db.AddRange(alreadyActive, newlyProposed);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.PostAsync($"/api/matching/{newlyProposed.AssignmentId}/approve", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
