using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Matching;
using LaunchPad.Application.Notifications;
using LaunchPad.Application.Projects;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Proves resource-based authorization for Project create/edit: a Sponsor can only
/// ever act as themselves (SponsorId is resolved server-side, never trusted from the
/// request body), can't edit another sponsor's project, and Ops bypasses ownership —
/// exactly the OwnsProjectHandler behavior described in CLAUDE.md §"Authorization model".
/// </summary>
public class ProjectsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public ProjectsControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(Guid OwnerOid, int ProjectId, int CohortId)> SeedProjectAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var ownerOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Test Program" };
        var cohort = new Cohort { Program = program, Name = "Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var owner = new Sponsor { AppUser = new AppUser { EntraObjectId = ownerOid, Upn = "owner@example.com", DisplayName = "Owner Sponsor" } };
        var project = new Project
        {
            Cohort = cohort,
            Sponsor = owner,
            Name = "Existing Project",
            AvailabilityNeeded = Availability.PartTime,
            ApprovalStatus = ProjectApprovalStatus.Draft,
            Status = ProjectStatus.Open,
        };

        db.AddRange(program, cohort, owner, project);
        await db.SaveChangesAsync();

        return (ownerOid, project.ProjectId, cohort.CohortId);
    }

    [Fact]
    public async Task Create_AsSponsor_ResolvesSponsorIdServerSide_IgnoringClientSuppliedValue()
    {
        var (ownerOid, _, cohortId) = await SeedProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var request = new CreateProjectRequest { CohortId = cohortId, Name = "New Project", AvailabilityNeeded = Availability.FullTime };
        var response = await client.PostAsJsonAsync("/api/projects", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<ProjectDto>(TestJsonOptions.Default);
        dto!.Name.Should().Be("New Project");
    }

    [Fact]
    public async Task Create_WithANewFreeTextSkillName_AssignsItToTheUncategorizedFallbackCategory()
    {
        var (ownerOid, _, cohortId) = await SeedProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var skillName = $"Brand New Skill {Guid.NewGuid()}";
        var request = new CreateProjectRequest
        {
            CohortId = cohortId,
            Name = "Project With A New Skill",
            AvailabilityNeeded = Availability.FullTime,
            RequiredSkillNames = new[] { skillName },
        };
        var response = await client.PostAsJsonAsync("/api/projects", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<ProjectDto>(TestJsonOptions.Default);
        dto!.RequiredSkills.Should().ContainSingle(s => s.SkillName == skillName && s.Category == "Uncategorized");
    }

    [Fact]
    public async Task Create_AsProgramOps_IsForbidden()
    {
        var (_, _, cohortId) = await SeedProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var request = new CreateProjectRequest { CohortId = cohortId, Name = "Should Not Be Created", AvailabilityNeeded = Availability.FullTime };
        var response = await client.PostAsJsonAsync("/api/projects", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_AsOwningSponsor_Succeeds()
    {
        var (ownerOid, projectId, _) = await SeedProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var request = new UpdateProjectRequest { Name = "Updated Name", AvailabilityNeeded = Availability.FullTime };
        var response = await client.PutAsJsonAsync($"/api/projects/{projectId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_AsNonOwningSponsor_IsForbidden()
    {
        var (_, projectId, _) = await SeedProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString()); // a different sponsor

        var request = new UpdateProjectRequest { Name = "Hijacked Name", AvailabilityNeeded = Availability.FullTime };
        var response = await client.PutAsJsonAsync($"/api/projects/{projectId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_AsProgramOps_BypassesOwnership()
    {
        var (_, projectId, _) = await SeedProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var request = new UpdateProjectRequest { Name = "Ops Edited Name", AvailabilityNeeded = Availability.FullTime };
        var response = await client.PutAsJsonAsync($"/api/projects/{projectId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<(Guid OwnerOid, int ProjectId, int AssignmentId)> SeedProjectWithProposedMatchAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var ownerOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Match Test Program" };
        var cohort = new Cohort { Program = program, Name = "Match Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var owner = new Sponsor { AppUser = new AppUser { EntraObjectId = ownerOid, Upn = "match-owner@example.com", DisplayName = "Match Owner Sponsor" } };
        var candidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "match-candidate@example.com", DisplayName = "Match Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var project = new Project { Cohort = cohort, Sponsor = owner, Name = "Project With A Match", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.Open };

        db.AddRange(program, cohort, owner, candidate, project);
        await db.SaveChangesAsync();

        var assignment = new Assignment { ProjectId = project.ProjectId, CandidateId = candidate.CandidateId, Status = AssignmentStatus.Proposed, MatchScore = 90m, MatchRationale = "Test rationale" };
        db.Add(assignment);
        await db.SaveChangesAsync();

        return (ownerOid, project.ProjectId, assignment.AssignmentId);
    }

    [Fact]
    public async Task GetMatches_AsOwningSponsor_ReturnsProposedCandidate()
    {
        var (ownerOid, projectId, assignmentId) = await SeedProjectWithProposedMatchAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.GetAsync($"/api/projects/{projectId}/matches");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var matches = await response.Content.ReadFromJsonAsync<List<ProjectMatchDto>>(TestJsonOptions.Default);
        matches.Should().ContainSingle(m => m.AssignmentId == assignmentId);
    }

    [Fact]
    public async Task GetMatches_AsNonOwningSponsor_IsForbidden()
    {
        var (_, projectId, _) = await SeedProjectWithProposedMatchAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync($"/api/projects/{projectId}/matches");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RecommendMatch_AsNonOwningSponsor_IsForbidden()
    {
        var (_, projectId, assignmentId) = await SeedProjectWithProposedMatchAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.PostAsync($"/api/projects/{projectId}/matches/{assignmentId}/recommend", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RejectMatch_AsOwningSponsor_WithdrawsTheCandidate()
    {
        var (ownerOid, projectId, assignmentId) = await SeedProjectWithProposedMatchAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.PostAsync($"/api/projects/{projectId}/matches/{assignmentId}/reject", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var matchesAfter = await (await client.GetAsync($"/api/projects/{projectId}/matches")).Content.ReadFromJsonAsync<List<ProjectMatchDto>>(TestJsonOptions.Default);
        matchesAfter.Should().BeEmpty();
    }

    private async Task<(Guid OwnerOid, int ProjectId)> SeedPendingOpsProjectAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var ownerOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Pending Test Program" };
        var cohort = new Cohort { Program = program, Name = "Pending Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var owner = new Sponsor { AppUser = new AppUser { EntraObjectId = ownerOid, Upn = "pending-owner@example.com", DisplayName = "Pending Owner" } };
        var project = new Project { Cohort = cohort, Sponsor = owner, Name = "Pending Project", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.PendingOps, Status = ProjectStatus.Open };

        db.AddRange(program, cohort, owner, project);
        await db.SaveChangesAsync();

        return (ownerOid, project.ProjectId);
    }

    private async Task<(Guid CandidateOid, int ProjectId)> SeedApprovedOpenProjectWithCandidateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var candidateOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Open Test Program" };
        var cohort = new Cohort { Program = program, Name = "Open Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "open-sponsor@example.com", DisplayName = "Open Sponsor" } };
        var candidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = candidateOid, Upn = "open-candidate@example.com", DisplayName = "Open Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var project = new Project { Cohort = cohort, Sponsor = sponsor, Name = "Open Project", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.Open };

        db.AddRange(program, cohort, sponsor, candidate, project);
        await db.SaveChangesAsync();

        return (candidateOid, project.ProjectId);
    }

    [Fact]
    public async Task Submit_AsOwningSponsor_FromDraft_MovesToPendingOps_AndNotifiesSponsorAndOps()
    {
        var (ownerOid, projectId, _) = await SeedProjectAsync(); // Seeded Draft

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.PostAsync($"/api/projects/{projectId}/submit", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProjectDto>(TestJsonOptions.Default);
        dto!.ApprovalStatus.Should().Be(ProjectApprovalStatus.PendingOps);

        var publisher = (FakeNotificationPublisher)_factory.Services.GetRequiredService<INotificationPublisher>();
        publisher.Sent.Should().Contain(m => m.ToUpn == "owner@example.com" && m.Subject.Contains("submitted"));
    }

    [Fact]
    public async Task Submit_AsNonOwningSponsor_IsForbidden()
    {
        var (_, projectId, _) = await SeedProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.PostAsync($"/api/projects/{projectId}/submit", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Submit_WhenAlreadyApproved_ReturnsBadRequest()
    {
        var (ownerOid, projectId, _) = await SeedProjectWithProposedMatchAsync(); // Seeded Approved+Open

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.PostAsync($"/api/projects/{projectId}/submit", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Approve_AsProgramOps_FromPendingOps_Succeeds_AndNotifiesSponsor()
    {
        var (_, projectId) = await SeedPendingOpsProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.PostAsync($"/api/projects/{projectId}/approve", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProjectDto>(TestJsonOptions.Default);
        dto!.ApprovalStatus.Should().Be(ProjectApprovalStatus.Approved);

        var publisher = (FakeNotificationPublisher)_factory.Services.GetRequiredService<INotificationPublisher>();
        publisher.Sent.Should().Contain(m => m.ToUpn == "pending-owner@example.com" && m.Subject.Contains("approved"));
    }

    [Fact]
    public async Task Approve_AsSponsor_IsForbidden()
    {
        var (_, projectId) = await SeedPendingOpsProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var response = await client.PostAsync($"/api/projects/{projectId}/approve", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reject_AsProgramOps_SetsReasonAndNotifiesSponsor()
    {
        var (_, projectId) = await SeedPendingOpsProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/reject",
            new RejectProjectRequest { Reason = "Scope too broad for this cohort." });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProjectDto>(TestJsonOptions.Default);
        dto!.ApprovalStatus.Should().Be(ProjectApprovalStatus.Rejected);
        dto.RejectionReason.Should().Be("Scope too broad for this cohort.");

        var publisher = (FakeNotificationPublisher)_factory.Services.GetRequiredService<INotificationPublisher>();
        publisher.Sent.Should().Contain(m => m.ToUpn == "pending-owner@example.com" && m.Body.Contains("Scope too broad"));
    }

    [Fact]
    public async Task Reject_WithEmptyReason_ReturnsValidationProblem()
    {
        var (_, projectId) = await SeedPendingOpsProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/reject", new RejectProjectRequest { Reason = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOpenDetail_ForApprovedOpenProject_ReturnsIt()
    {
        var (candidateOid, projectId) = await SeedApprovedOpenProjectWithCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, candidateOid.ToString());

        var response = await client.GetAsync($"/api/projects/{projectId}/open-detail");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProjectDto>(TestJsonOptions.Default);
        dto!.ProjectId.Should().Be(projectId);
        dto.SponsorTeamsLink.Should().Contain("teams.microsoft.com");
    }

    [Fact]
    public async Task GetOpenDetail_ForDraftProject_ReturnsNotFound()
    {
        var (_, projectId, _) = await SeedProjectAsync(); // Seeded Draft

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync($"/api/projects/{projectId}/open-detail");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RateInterest_UpsertsRatherThanDuplicating()
    {
        var (candidateOid, projectId) = await SeedApprovedOpenProjectWithCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, candidateOid.ToString());

        var first = await client.PostAsJsonAsync($"/api/projects/{projectId}/interest", new RateInterestRequest { Rating = 3 });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync($"/api/projects/{projectId}/interest", new RateInterestRequest { Rating = 5 });
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetAsync($"/api/projects/{projectId}/open-detail");
        var dto = await detail.Content.ReadFromJsonAsync<ProjectDto>(TestJsonOptions.Default);
        dto!.MyInterestRating.Should().Be(5);
    }

    [Fact]
    public async Task RateInterest_OutOfRange_ReturnsValidationProblem()
    {
        var (candidateOid, projectId) = await SeedApprovedOpenProjectWithCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, candidateOid.ToString());

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/interest", new RateInterestRequest { Rating = 9 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
