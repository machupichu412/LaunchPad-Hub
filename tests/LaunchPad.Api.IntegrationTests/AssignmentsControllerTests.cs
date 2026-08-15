using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using LaunchPad.Application.Assignments;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Proves resource-based authorization for a Candidate's own Assignment sub-resources
/// (todos/deliverables/evaluations): the owning candidate can reach them, a different
/// candidate is forbidden, and Ops bypasses ownership — the OwnsAssignmentHandler
/// behavior described in CLAUDE.md §"Authorization model". Also proves the Evaluations
/// endpoint never leaks a numeric/star rating to a Candidate — CLAUDE.md's single most
/// important control, extended to this new endpoint.
/// </summary>
public class AssignmentsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public AssignmentsControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(Guid OwnerOid, int AssignmentId, int TodoId)> SeedAssignmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var ownerOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Test Program" };
        var cohort = new Cohort { Program = program, Name = "Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "sponsor@example.com", DisplayName = "Test Sponsor" }, Organization = "Test Org" };
        var project = new Project
        {
            Cohort = cohort,
            Sponsor = sponsor,
            Name = "Test Project",
            AvailabilityNeeded = Availability.PartTime,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = ProjectStatus.InProgress,
        };
        var owner = new Candidate { AppUser = new AppUser { EntraObjectId = ownerOid, Upn = "owner@example.com", DisplayName = "Owner Candidate" }, Cohort = cohort, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var assignment = new Assignment { Project = project, Candidate = owner, Status = AssignmentStatus.Active, MatchScore = 90m };

        db.AddRange(program, cohort, sponsor, project, owner, assignment);
        await db.SaveChangesAsync();

        var todo = new ProjectTodo { Assignment = assignment, Title = "Do the thing", Status = TodoStatus.NotStarted, Priority = TodoPriority.Medium };
        var review = new Review
        {
            Assignment = assignment,
            ReviewType = ReviewType.SponsorOnCandidate,
            Checkpoint = Checkpoint.Midpoint,
            SubmittedBy = sponsor.AppUser.AppUserId,
            Strengths = "Great communicator.",
            GrowthAreas = "Could ask for help sooner.",
            RecommendConversion = true,
        };
        db.AddRange(todo, review);
        await db.SaveChangesAsync();

        return (ownerOid, assignment.AssignmentId, todo.ProjectTodoId);
    }

    /// <summary>Builds the same multipart/form-data shape AssignmentsController.SubmitDeliverable
    /// expects — title/projectTodoId as form fields, file as a form file part.</summary>
    private static MultipartFormDataContent BuildDeliverableForm(
        string title, string fileName, int? projectTodoId = null, byte[]? content = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(title), "title");
        if (projectTodoId is int id)
        {
            form.Add(new StringContent(id.ToString()), "projectTodoId");
        }

        var fileContent = new ByteArrayContent(content ?? Encoding.UTF8.GetBytes("fake file bytes"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", fileName);
        return form;
    }

    [Fact]
    public async Task GetMine_AsOwningCandidate_ReturnsOwnAssignment()
    {
        var (ownerOid, assignmentId, _) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.GetAsync("/api/assignments/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MyAssignmentDto>(TestJsonOptions.Default);
        dto!.AssignmentId.Should().Be(assignmentId);
    }

    [Fact]
    public async Task GetTodos_AsOwningCandidate_Succeeds()
    {
        var (ownerOid, assignmentId, _) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.GetAsync($"/api/assignments/{assignmentId}/todos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var todos = await response.Content.ReadFromJsonAsync<List<ProjectTodoDto>>(TestJsonOptions.Default);
        todos.Should().ContainSingle();
    }

    [Fact]
    public async Task GetTodos_AsNonOwningCandidate_IsForbidden()
    {
        var (_, assignmentId, _) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString()); // a different candidate

        var response = await client.GetAsync($"/api/assignments/{assignmentId}/todos");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTodos_AsProgramOps_BypassesOwnership()
    {
        var (_, assignmentId, _) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.GetAsync($"/api/assignments/{assignmentId}/todos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateTodoStatus_AsOwningCandidate_Succeeds()
    {
        var (ownerOid, assignmentId, todoId) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.PatchAsJsonAsync($"/api/assignments/{assignmentId}/todos/{todoId}", new UpdateTodoStatusRequest { Status = TodoStatus.Completed });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProjectTodoDto>(TestJsonOptions.Default);
        dto!.Status.Should().Be(TodoStatus.Completed);
    }

    [Fact]
    public async Task UpdateTodoStatus_AsNonOwningCandidate_IsForbidden()
    {
        var (_, assignmentId, todoId) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.PatchAsJsonAsync($"/api/assignments/{assignmentId}/todos/{todoId}", new UpdateTodoStatusRequest { Status = TodoStatus.Completed });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SubmitDeliverable_AsOwningCandidate_Succeeds()
    {
        var (ownerOid, assignmentId, _) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        using var form = BuildDeliverableForm("Sprint 1 Recap", "recap.pdf");
        var response = await client.PostAsync($"/api/assignments/{assignmentId}/deliverables", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<DeliverableDto>(TestJsonOptions.Default);
        dto!.Title.Should().Be("Sprint 1 Recap");
        dto.HasFile.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitDeliverable_AsNonOwningCandidate_IsForbidden()
    {
        var (_, assignmentId, _) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        using var form = BuildDeliverableForm("Hijacked", "hijacked.pdf");
        var response = await client.PostAsync($"/api/assignments/{assignmentId}/deliverables", form);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Proves the synchronous self-heal path: a candidate with no SharePoint folder
    /// yet (SeedAssignmentAsync never sets one) gets it backfilled inline on their first upload,
    /// instead of failing because async provisioning hasn't run yet.</summary>
    [Fact]
    public async Task SubmitDeliverable_WhenCandidateHasNoFolderYet_SelfHealsBeforeUploading()
    {
        var (ownerOid, assignmentId, _) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        using var form = BuildDeliverableForm("First Upload", "first.pdf");
        var response = await client.PostAsync($"/api/assignments/{assignmentId}/deliverables", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var candidate = await db.Candidates.SingleAsync(c => c.AppUser.EntraObjectId == ownerOid);
        candidate.SharePointFolderId.Should().NotBeNullOrEmpty();
    }

    /// <summary>Round-trips a deliverable's bytes through the actual HTTP pipeline —
    /// FakeDocumentStorage stands in for Graph, but the upload and download endpoints
    /// themselves are exercised for real.</summary>
    [Fact]
    public async Task SubmitThenDownloadDeliverable_ReturnsTheExactBytesUploaded()
    {
        var (ownerOid, assignmentId, _) = await SeedAssignmentAsync();
        var originalBytes = Encoding.UTF8.GetBytes("the exact deliverable content");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        using var form = BuildDeliverableForm("Round Trip", "roundtrip.txt", content: originalBytes);
        var submitResponse = await client.PostAsync($"/api/assignments/{assignmentId}/deliverables", form);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<DeliverableDto>(TestJsonOptions.Default);

        var downloadResponse = await client.GetAsync($"/api/assignments/{assignmentId}/deliverables/{submitted!.DeliverableId}/file");

        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        downloadedBytes.Should().Equal(originalBytes);
    }

    [Fact]
    public async Task GetEvaluations_AsOwningCandidate_NeverContainsAScoreField()
    {
        var (ownerOid, assignmentId, _) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.GetAsync($"/api/assignments/{assignmentId}/evaluations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().ContainEquivalentOf("Strengths").And.ContainEquivalentOf("GrowthAreas").And.ContainEquivalentOf("RecommendConversion");
        body.Should().NotContainEquivalentOf("Score");
        body.Should().NotContainEquivalentOf("Commitment");
        body.Should().NotContainEquivalentOf("OutputQuality");
    }

    [Fact]
    public async Task GetEvaluations_AsNonOwningCandidate_IsForbidden()
    {
        var (_, assignmentId, _) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync($"/api/assignments/{assignmentId}/evaluations");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Distinct from SeedAssignmentAsync — that helper never exposes the sponsor's
    /// EntraObjectId, since it never needed to until sponsors could reach this controller.</summary>
    private async Task<(Guid SponsorOid, Guid CandidateOid, int AssignmentId, int TodoId)> SeedAssignmentWithSponsorAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var sponsorOid = Guid.NewGuid();
        var candidateOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Sponsor Todo Program" };
        var cohort = new Cohort { Program = program, Name = "Sponsor Todo Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = sponsorOid, Upn = "todo-sponsor@example.com", DisplayName = "Todo Sponsor" }, Organization = "Test Org" };
        var project = new Project { Cohort = cohort, Sponsor = sponsor, Name = "Sponsor Todo Project", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.InProgress };
        var candidate = new Candidate { AppUser = new AppUser { EntraObjectId = candidateOid, Upn = "todo-candidate@example.com", DisplayName = "Todo Candidate" }, Cohort = cohort, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var assignment = new Assignment { Project = project, Candidate = candidate, Status = AssignmentStatus.Active, MatchScore = 90m };

        db.AddRange(program, cohort, sponsor, project, candidate, assignment);
        await db.SaveChangesAsync();

        var todo = new ProjectTodo { Assignment = assignment, Title = "Existing todo", Status = TodoStatus.NotStarted, Priority = TodoPriority.Medium };
        db.Add(todo);
        await db.SaveChangesAsync();

        return (sponsorOid, candidateOid, assignment.AssignmentId, todo.ProjectTodoId);
    }

    [Fact]
    public async Task CreateTodo_AsOwningSponsor_Succeeds()
    {
        var (sponsorOid, _, assignmentId, _) = await SeedAssignmentWithSponsorAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());

        var response = await client.PostAsJsonAsync(
            $"/api/assignments/{assignmentId}/todos",
            new CreateTodoRequest { Title = "Draft wireframes", Priority = TodoPriority.High, DueDate = null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProjectTodoDto>(TestJsonOptions.Default);
        dto!.Title.Should().Be("Draft wireframes");
        dto.Status.Should().Be(TodoStatus.NotStarted);
    }

    [Fact]
    public async Task CreateTodo_AsNonOwningSponsor_IsForbidden()
    {
        var (_, _, assignmentId, _) = await SeedAssignmentWithSponsorAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync(
            $"/api/assignments/{assignmentId}/todos",
            new CreateTodoRequest { Title = "Hijacked", Priority = TodoPriority.Medium, DueDate = null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateTodo_AsCandidate_IsForbidden_EvenOnOwnAssignment()
    {
        var (_, candidateOid, assignmentId, _) = await SeedAssignmentWithSponsorAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, candidateOid.ToString());

        var response = await client.PostAsJsonAsync(
            $"/api/assignments/{assignmentId}/todos",
            new CreateTodoRequest { Title = "Self-assigned", Priority = TodoPriority.Medium, DueDate = null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "a Candidate can only check todos off, never create their own");
    }

    [Fact]
    public async Task CreateTodo_AsProgramOps_BypassesOwnership()
    {
        var (_, _, assignmentId, _) = await SeedAssignmentWithSponsorAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.PostAsJsonAsync(
            $"/api/assignments/{assignmentId}/todos",
            new CreateTodoRequest { Title = "Ops-added todo", Priority = TodoPriority.Low, DueDate = null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitDeliverable_WithProjectTodoIdOnSameAssignment_AttachesSuccessfully()
    {
        var (_, candidateOid, assignmentId, todoId) = await SeedAssignmentWithSponsorAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, candidateOid.ToString());

        using var form = BuildDeliverableForm("Wireframes v1", "wireframes.pdf", projectTodoId: todoId);
        var response = await client.PostAsync($"/api/assignments/{assignmentId}/deliverables", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<DeliverableDto>(TestJsonOptions.Default);
        dto!.ProjectTodoId.Should().Be(todoId);
        dto.ProjectTodoTitle.Should().Be("Existing todo");
    }

    [Fact]
    public async Task SubmitDeliverable_WithProjectTodoIdFromADifferentAssignment_ReturnsBadRequest()
    {
        var (_, candidateOid, assignmentId, _) = await SeedAssignmentWithSponsorAsync();
        var (_, _, _, otherAssignmentsTodoId) = await SeedAssignmentWithSponsorAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, candidateOid.ToString());

        using var form = BuildDeliverableForm("Cross-assignment", "x.pdf", projectTodoId: otherAssignmentsTodoId);
        var response = await client.PostAsync($"/api/assignments/{assignmentId}/deliverables", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
