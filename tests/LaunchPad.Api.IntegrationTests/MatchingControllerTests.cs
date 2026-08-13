using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Matching;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// The real two-stage flow: "Run matching" proposes up to 3 ranked candidates per
/// open project (Proposed); the owning sponsor recommends one on their own project
/// (ProjectsController's matches actions, Proposed -> SponsorApproved); Ops approves
/// here, but only from SponsorApproved (Policies.ApproveMatch, ProgramOps-only).
/// </summary>
public class MatchingControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public MatchingControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(int CohortId, int ProjectId, Guid SponsorOid, int[] CandidateIds)> SeedMatchingScenarioAsync(int candidateCount = 3)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var sponsorOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Test Program" };
        var cohort = new Cohort { Program = program, Name = "Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = sponsorOid, Upn = "sponsor@example.com", DisplayName = "Test Sponsor" }, Organization = "Test Org" };
        var skill = new Skill { Name = "React", SkillCategory = new SkillCategory { Name = "Engineering" } };

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

        var candidates = Enumerable.Range(0, candidateCount).Select(i => new Candidate
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = $"candidate{i}@example.com", DisplayName = $"Test Candidate {i}" },
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
            Skills = new List<CandidateSkill> { new() { Skill = skill, Proficiency = 4, Source = SkillSource.SelfReported } },
        }).ToList();

        db.AddRange(program, cohort, sponsor, skill, project);
        db.AddRange(candidates);
        await db.SaveChangesAsync();

        return (cohort.CohortId, project.ProjectId, sponsorOid, candidates.Select(c => c.CandidateId).ToArray());
    }

    [Fact]
    public async Task Run_AsProgramOps_ProposesUpToThreeMatchesPerProject()
    {
        var (cohortId, projectId, sponsorOid, _) = await SeedMatchingScenarioAsync(candidateCount: 4);

        var opsClient = _factory.CreateClient();
        opsClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var runResponse = await opsClient.PostAsync($"/api/matching/run?cohortId={cohortId}", content: null);
        runResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var runResult = await runResponse.Content.ReadFromJsonAsync<RunMatchingResult>(TestJsonOptions.Default);
        runResult!.ProposedCount.Should().Be(3, "topN defaults to 3 even though 4 candidates were eligible");

        var sponsorClient = _factory.CreateClient();
        sponsorClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        sponsorClient.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());

        var matches = await (await sponsorClient.GetAsync($"/api/projects/{projectId}/matches")).Content.ReadFromJsonAsync<List<ProjectMatchDto>>(TestJsonOptions.Default);
        matches.Should().HaveCount(3);
    }

    [Fact]
    public async Task Run_TwiceInARow_DoesNotDuplicateProposalsForTheSameProject()
    {
        var (cohortId, _, _, _) = await SeedMatchingScenarioAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var first = await (await client.PostAsync($"/api/matching/run?cohortId={cohortId}", content: null)).Content.ReadFromJsonAsync<RunMatchingResult>(TestJsonOptions.Default);
        first!.ProposedCount.Should().BeGreaterThan(0);

        var second = await (await client.PostAsync($"/api/matching/run?cohortId={cohortId}", content: null)).Content.ReadFromJsonAsync<RunMatchingResult>(TestJsonOptions.Default);
        second!.ProposedCount.Should().Be(0, "the project already has matches awaiting sponsor/ops review");
    }

    [Fact]
    public async Task Run_AsNonProgramOps_IsForbidden()
    {
        var (cohortId, _, _, _) = await SeedMatchingScenarioAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var response = await client.PostAsync($"/api/matching/run?cohortId={cohortId}", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approve_OnABareProposedAssignment_ReturnsBadRequest()
    {
        var (cohortId, projectId, sponsorOid, candidateIds) = await SeedMatchingScenarioAsync(candidateCount: 1);

        var opsClient = _factory.CreateClient();
        opsClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);
        await opsClient.PostAsync($"/api/matching/run?cohortId={cohortId}", content: null);

        var sponsorClient = _factory.CreateClient();
        sponsorClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        sponsorClient.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());
        var matches = await (await sponsorClient.GetAsync($"/api/projects/{projectId}/matches")).Content.ReadFromJsonAsync<List<ProjectMatchDto>>(TestJsonOptions.Default);
        var assignmentId = matches!.Single(m => m.CandidateId == candidateIds[0]).AssignmentId;

        var approveResponse = await opsClient.PostAsync($"/api/matching/{assignmentId}/approve", content: null);

        approveResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the sponsor hasn't recommended this candidate yet");
    }

    [Fact]
    public async Task FullTwoStageFlow_RecommendThenApprove_Succeeds_AndWithdrawsSiblingProposals()
    {
        var (cohortId, projectId, sponsorOid, candidateIds) = await SeedMatchingScenarioAsync(candidateCount: 3);

        var opsClient = _factory.CreateClient();
        opsClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);
        await opsClient.PostAsync($"/api/matching/run?cohortId={cohortId}", content: null);

        var sponsorClient = _factory.CreateClient();
        sponsorClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        sponsorClient.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());
        var matches = await (await sponsorClient.GetAsync($"/api/projects/{projectId}/matches")).Content.ReadFromJsonAsync<List<ProjectMatchDto>>(TestJsonOptions.Default);
        matches.Should().HaveCount(3);
        var picked = matches![0];

        var recommendResponse = await sponsorClient.PostAsync($"/api/projects/{projectId}/matches/{picked.AssignmentId}/recommend", content: null);
        recommendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // The other two proposed candidates for this project are auto-withdrawn.
        var matchesAfterRecommend = await (await sponsorClient.GetAsync($"/api/projects/{projectId}/matches")).Content.ReadFromJsonAsync<List<ProjectMatchDto>>(TestJsonOptions.Default);
        matchesAfterRecommend.Should().BeEmpty("recommending a winner withdraws the other Proposed candidates on this project");

        // Now it's actionable in Ops's queue.
        var queue = await (await opsClient.GetAsync($"/api/matching/queue?cohortId={cohortId}")).Content.ReadFromJsonAsync<List<PendingAssignmentDto>>(TestJsonOptions.Default);
        queue.Should().ContainSingle(a => a.AssignmentId == picked.AssignmentId);

        var approveResponse = await opsClient.PostAsync($"/api/matching/{picked.AssignmentId}/approve", content: null);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var queueAfterApprove = await (await opsClient.GetAsync($"/api/matching/queue?cohortId={cohortId}")).Content.ReadFromJsonAsync<List<PendingAssignmentDto>>(TestJsonOptions.Default);
        queueAfterApprove.Should().BeEmpty();

        (await GetAuditEventsAsync("Assignment", picked.AssignmentId)).Should().Contain(e => e.Action == "SponsorRecommend");
        (await GetAuditEventsAsync("Assignment", picked.AssignmentId)).Should().Contain(e => e.Action == "OpsApprove");
    }

    private async Task<IReadOnlyList<AuditEvent>> GetAuditEventsAsync(string entityName, int entityId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        return await db.AuditEvents
            .Where(e => e.EntityName == entityName && e.EntityId == entityId.ToString())
            .ToListAsync();
    }

    [Fact]
    public async Task Approve_CascadesWithdrawOfTheSameCandidatesOtherPendingOffers()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var program = new Domain.Entities.Program { Name = "Cascade Program" };
        var cohort = new Cohort { Program = program, Name = "Cascade Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "cascade-sponsor@example.com", DisplayName = "Cascade Sponsor" } };
        var candidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "coveted@example.com", DisplayName = "Coveted Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var projectP = new Project { Cohort = cohort, Sponsor = sponsor, Name = "Project P", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.Open };
        var projectQ = new Project { Cohort = cohort, Sponsor = sponsor, Name = "Project Q", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.Open };

        db.AddRange(program, cohort, sponsor, candidate, projectP, projectQ);
        await db.SaveChangesAsync();

        var onP = new Assignment { ProjectId = projectP.ProjectId, CandidateId = candidate.CandidateId, Status = AssignmentStatus.SponsorApproved };
        var onQ = new Assignment { ProjectId = projectQ.ProjectId, CandidateId = candidate.CandidateId, Status = AssignmentStatus.Proposed };
        db.AddRange(onP, onQ);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.PostAsync($"/api/matching/{onP.AssignmentId}/approve", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // A fresh scope/DbContext — reusing the one above would just return its
        // locally-cached (now stale) copy of onQ instead of re-reading the mutation
        // the HTTP call made through its own request-scoped DbContext.
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var reloadedQ = await verifyDb.Assignments.FindAsync(onQ.AssignmentId);
        reloadedQ!.Status.Should().Be(AssignmentStatus.Withdrawn, "the candidate is committed to project P now");
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
        var newlySponsorApproved = new Assignment { ProjectId = projectB.ProjectId, CandidateId = candidate.CandidateId, Status = AssignmentStatus.SponsorApproved };
        db.AddRange(alreadyActive, newlySponsorApproved);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.PostAsync($"/api/matching/{newlySponsorApproved.AssignmentId}/approve", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deny_OnASponsorApprovedAssignment_ReturnsItToTheProject()
    {
        var (cohortId, projectId, sponsorOid, _) = await SeedMatchingScenarioAsync(candidateCount: 1);

        var opsClient = _factory.CreateClient();
        opsClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);
        await opsClient.PostAsync($"/api/matching/run?cohortId={cohortId}", content: null);

        var sponsorClient = _factory.CreateClient();
        sponsorClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        sponsorClient.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());
        var matches = await (await sponsorClient.GetAsync($"/api/projects/{projectId}/matches")).Content.ReadFromJsonAsync<List<ProjectMatchDto>>(TestJsonOptions.Default);
        var assignmentId = matches!.Single().AssignmentId;
        await sponsorClient.PostAsync($"/api/projects/{projectId}/matches/{assignmentId}/recommend", content: null);

        var denyResponse = await opsClient.PostAsync($"/api/matching/{assignmentId}/deny", content: null);

        denyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var queueAfter = await (await opsClient.GetAsync($"/api/matching/queue?cohortId={cohortId}")).Content.ReadFromJsonAsync<List<PendingAssignmentDto>>(TestJsonOptions.Default);
        queueAfter.Should().BeEmpty();

        (await GetAuditEventsAsync("Assignment", assignmentId)).Should().Contain(e => e.Action == "OpsDeny");
    }
}
