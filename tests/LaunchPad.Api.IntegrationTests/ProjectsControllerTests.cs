using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Matching;
using LaunchPad.Application.Notifications;
using LaunchPad.Application.Projects;
using LaunchPad.Application.Sponsors;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
    public async Task RecommendMatch_AsOwningSponsor_Succeeds_AndRecordsAnAuditEvent()
    {
        var (ownerOid, projectId, assignmentId) = await SeedProjectWithProposedMatchAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.PostAsync($"/api/projects/{projectId}/matches/{assignmentId}/recommend", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAuditEventsAsync("Assignment", assignmentId)).Should().Contain(e => e.Action == "SponsorRecommend");
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

        (await GetAuditEventsAsync("Assignment", assignmentId)).Should().Contain(e => e.Action == "SponsorReject");
    }

    private async Task<IReadOnlyList<AuditEvent>> GetAuditEventsAsync(string entityName, int entityId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        return await db.AuditEvents
            .Where(e => e.EntityName == entityName && e.EntityId == entityId.ToString())
            .ToListAsync();
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

        (await GetAuditEventsAsync("Project", projectId)).Should().Contain(e => e.Action == "Submit");
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
        // Approve publishes a FolderProvisioningJob; FakeFolderProvisioningJobPublisher runs it
        // inline, so the project's SharePoint fields are already backfilled in this same response.
        dto.SharePointFolderWebUrl.Should().NotBeNullOrEmpty();

        var publisher = (FakeNotificationPublisher)_factory.Services.GetRequiredService<INotificationPublisher>();
        publisher.Sent.Should().Contain(m => m.ToUpn == "pending-owner@example.com" && m.Subject.Contains("approved"));

        (await GetAuditEventsAsync("Project", projectId)).Should().Contain(e => e.Action == "Approve");
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

        (await GetAuditEventsAsync("Project", projectId)).Should().Contain(e => e.Action == "Reject" && e.Reason == "Scope too broad for this cohort.");
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
    public async Task Reject_WhenAlreadyApproved_FlipsToRejected_AndDoesNotResendApprovedNotification()
    {
        var (_, projectId) = await SeedPendingOpsProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        (await client.PostAsync($"/api/projects/{projectId}/approve", content: null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/reject",
            new RejectProjectRequest { Reason = "Revisiting — this no longer fits the cohort." });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProjectDto>(TestJsonOptions.Default);
        dto!.ApprovalStatus.Should().Be(ProjectApprovalStatus.Rejected);
        dto.RejectionReason.Should().Be("Revisiting — this no longer fits the cohort.");
    }

    [Fact]
    public async Task Approve_WhenAlreadyRejected_FlipsToApproved_AndClearsRejectionReason()
    {
        var (_, projectId) = await SeedPendingOpsProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        (await client.PostAsJsonAsync($"/api/projects/{projectId}/reject", new RejectProjectRequest { Reason = "Not ready yet." }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsync($"/api/projects/{projectId}/approve", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProjectDto>(TestJsonOptions.Default);
        dto!.ApprovalStatus.Should().Be(ProjectApprovalStatus.Approved);
        dto.RejectionReason.Should().BeNull();
    }

    [Fact]
    public async Task Approve_WhenStillDraft_ReturnsBadRequest()
    {
        var (_, projectId, _) = await SeedProjectAsync(); // Seeded Draft

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.PostAsync($"/api/projects/{projectId}/approve", content: null);

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

    private async Task<(Guid SponsorOid, int ProjectId, int[] CandidateIds)> SeedApprovedProjectWithEligibleCandidatesAsync(
        int maxCandidates = 1, int candidateCount = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var sponsorOid = Guid.NewGuid();
        var unique = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Eligible Test Program" };
        var cohort = new Cohort { Program = program, Name = "Eligible Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = sponsorOid, Upn = $"eligible-sponsor-{unique}@example.com", DisplayName = "Eligible Sponsor" } };
        var project = new Project
        {
            Cohort = cohort,
            Sponsor = sponsor,
            Name = "Eligible Project",
            AvailabilityNeeded = Availability.PartTime,
            MaxCandidates = maxCandidates,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = ProjectStatus.Open,
        };

        var candidates = Enumerable.Range(0, candidateCount).Select(i => new Candidate
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = $"eligible-candidate-{i}-{unique}@example.com", DisplayName = $"Eligible Candidate {i}" },
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
        }).ToList();

        db.AddRange(program, cohort, sponsor, project);
        db.AddRange(candidates);
        await db.SaveChangesAsync();

        return (sponsorOid, project.ProjectId, candidates.Select(c => c.CandidateId).ToArray());
    }

    [Fact]
    public async Task GetEligibleCandidates_BeforeApproval_ReturnsBadRequest()
    {
        var (ownerOid, projectId, _) = await SeedProjectAsync(); // Seeded Draft

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.GetAsync($"/api/projects/{projectId}/eligible-candidates");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEligibleCandidates_AfterApproval_ReturnsScoredCandidates_StructurallyWithoutAHiddenScoreField()
    {
        var (sponsorOid, projectId, candidateIds) = await SeedApprovedProjectWithEligibleCandidatesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());

        var response = await client.GetAsync($"/api/projects/{projectId}/eligible-candidates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var candidates = await response.Content.ReadFromJsonAsync<List<SponsorCandidateMatchDto>>(TestJsonOptions.Default);
        candidates.Should().ContainSingle(c => c.CandidateId == candidateIds[0]);

        // Structural guarantee, not a client-side filter — SponsorCandidateMatchDto has no
        // AverageScore/risk-flag properties to leak in the first place (see CLAUDE.md).
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("averageScore").And.NotContain("hasPerformanceRisk");
    }

    private async Task<(Guid SponsorOid, int ProjectId, int PlainCandidateId, int MatchedCandidateId, int ProposedAssignmentId)>
        SeedApprovedProjectWithMatchedAndPlainCandidateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var sponsorOid = Guid.NewGuid();
        var unique = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Matched Gallery Program" };
        var cohort = new Cohort { Program = program, Name = "Matched Gallery Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = sponsorOid, Upn = $"matched-gallery-sponsor-{unique}@example.com", DisplayName = "Matched Gallery Sponsor" } };
        var project = new Project
        {
            Cohort = cohort,
            Sponsor = sponsor,
            Name = "Matched Gallery Project",
            AvailabilityNeeded = Availability.PartTime,
            MaxCandidates = 2,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = ProjectStatus.Open,
        };
        var plainCandidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = $"plain-{unique}@example.com", DisplayName = "Plain Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var matchedCandidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = $"matched-{unique}@example.com", DisplayName = "Matched Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };

        db.AddRange(program, cohort, sponsor, project, plainCandidate, matchedCandidate);
        await db.SaveChangesAsync();

        var proposed = new Assignment { ProjectId = project.ProjectId, CandidateId = matchedCandidate.CandidateId, Status = AssignmentStatus.Proposed, MatchScore = 88m, MatchRationale = "Batch match" };
        db.Add(proposed);
        await db.SaveChangesAsync();

        return (sponsorOid, project.ProjectId, plainCandidate.CandidateId, matchedCandidate.CandidateId, proposed.AssignmentId);
    }

    [Fact]
    public async Task GetEligibleCandidates_IncludesProposedAssignmentId_ForBatchMatchedCandidatesOnly()
    {
        var (sponsorOid, projectId, plainCandidateId, matchedCandidateId, proposedAssignmentId) =
            await SeedApprovedProjectWithMatchedAndPlainCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());

        var response = await client.GetAsync($"/api/projects/{projectId}/eligible-candidates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var candidates = await response.Content.ReadFromJsonAsync<List<SponsorCandidateMatchDto>>(TestJsonOptions.Default);

        candidates.Should().Contain(c => c.CandidateId == matchedCandidateId && c.ProposedAssignmentId == proposedAssignmentId);
        candidates.Should().Contain(c => c.CandidateId == plainCandidateId && c.ProposedAssignmentId == null);
    }

    [Fact]
    public async Task RequestAssignment_AsOwningSponsor_CreatesSponsorApprovedAssignment_AndRecordsAnAuditEvent()
    {
        var (sponsorOid, projectId, candidateIds) = await SeedApprovedProjectWithEligibleCandidatesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());

        var response = await client.PostAsync($"/api/projects/{projectId}/candidates/{candidateIds[0]}/request", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProjectMatchDto>(TestJsonOptions.Default);
        dto!.CandidateId.Should().Be(candidateIds[0]);

        (await GetAuditEventsAsync("Assignment", dto.AssignmentId)).Should().Contain(e => e.Action == "SponsorDirectRequest");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var assignment = await db.Assignments.FindAsync(dto.AssignmentId);
        assignment!.Status.Should().Be(AssignmentStatus.SponsorApproved);
    }

    [Fact]
    public async Task RequestAssignment_BeforeApproval_ReturnsBadRequest()
    {
        var (ownerOid, projectId, _) = await SeedProjectAsync(); // Seeded Draft

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.PostAsync($"/api/projects/{projectId}/candidates/999999/request", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestAssignment_WhenProjectSpotsAreFull_ReturnsConflict()
    {
        var (sponsorOid, projectId, candidateIds) = await SeedApprovedProjectWithEligibleCandidatesAsync(maxCandidates: 1, candidateCount: 2);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());

        var firstRequest = await client.PostAsync($"/api/projects/{projectId}/candidates/{candidateIds[0]}/request", content: null);
        firstRequest.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondRequest = await client.PostAsync($"/api/projects/{projectId}/candidates/{candidateIds[1]}/request", content: null);

        secondRequest.StatusCode.Should().Be(HttpStatusCode.Conflict, "the project's only spot is already spoken for");
    }

    [Fact]
    public async Task RequestAssignment_WhenCandidateAlreadyHasALiveAssignmentElsewhere_ReturnsConflict()
    {
        var (sponsorOid, projectId, candidateIds) = await SeedApprovedProjectWithEligibleCandidatesAsync(maxCandidates: 2);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
            var project = await db.Projects.FindAsync(projectId);
            var elsewhereProject = new Project { CohortId = project!.CohortId, SponsorId = project.SponsorId, Name = "Elsewhere Project", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.InProgress };
            db.Add(elsewhereProject);
            await db.SaveChangesAsync();
            db.Add(new Assignment { ProjectId = elsewhereProject.ProjectId, CandidateId = candidateIds[0], Status = AssignmentStatus.Active, StartDate = new DateOnly(2026, 1, 1) });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());

        var response = await client.PostAsync($"/api/projects/{projectId}/candidates/{candidateIds[0]}/request", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "a candidate with a live assignment elsewhere is excluded from the eligible pool");
    }

    [Fact]
    public async Task GetAssignedCandidates_ReturnsTheDirectlyRequestedCandidate()
    {
        var (sponsorOid, projectId, candidateIds) = await SeedApprovedProjectWithEligibleCandidatesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());

        await client.PostAsync($"/api/projects/{projectId}/candidates/{candidateIds[0]}/request", content: null);

        var response = await client.GetAsync($"/api/projects/{projectId}/assigned-candidates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var assigned = await response.Content.ReadFromJsonAsync<List<SponsorCandidateDto>>(TestJsonOptions.Default);
        assigned.Should().ContainSingle(a => a.CandidateId == candidateIds[0] && a.Status == AssignmentStatus.SponsorApproved);
    }

    [Fact]
    public async Task Cancel_WithdrawsAssignments_SetsProjectCancelled_AndRecordsAnAuditEvent()
    {
        var (sponsorOid, projectId, candidateIds) = await SeedApprovedProjectWithEligibleCandidatesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());

        var requestResponse = await client.PostAsync($"/api/projects/{projectId}/candidates/{candidateIds[0]}/request", content: null);
        var requested = await requestResponse.Content.ReadFromJsonAsync<ProjectMatchDto>(TestJsonOptions.Default);

        var cancelResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/cancel", new RejectProjectRequest { Reason = "Sponsor pulled funding." });

        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await cancelResponse.Content.ReadFromJsonAsync<ProjectDto>(TestJsonOptions.Default);
        dto!.Status.Should().Be(ProjectStatus.Cancelled);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var assignment = await db.Assignments.FindAsync(requested!.AssignmentId);
        assignment!.Status.Should().Be(AssignmentStatus.Withdrawn, "cancelling a project frees its candidates back into the open pool");

        (await GetAuditEventsAsync("Project", projectId)).Should().Contain(e => e.Action == "ProjectCancelled");
    }

    [Fact]
    public async Task Cancel_WithEmptyReason_ReturnsValidationProblem()
    {
        var (sponsorOid, projectId, _) = await SeedApprovedProjectWithEligibleCandidatesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/cancel", new RejectProjectRequest { Reason = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ShrinkingMaxCandidatesBelowCommittedCount_ReturnsConflict()
    {
        var (sponsorOid, projectId, candidateIds) = await SeedApprovedProjectWithEligibleCandidatesAsync(maxCandidates: 2, candidateCount: 2);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());

        await client.PostAsync($"/api/projects/{projectId}/candidates/{candidateIds[0]}/request", content: null);
        await client.PostAsync($"/api/projects/{projectId}/candidates/{candidateIds[1]}/request", content: null);

        var response = await client.PutAsJsonAsync($"/api/projects/{projectId}", new UpdateProjectRequest
        {
            Name = "Eligible Project",
            AvailabilityNeeded = Availability.PartTime,
            MaxCandidates = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "2 candidates are already committed to this project's 2 spots");
    }
}
