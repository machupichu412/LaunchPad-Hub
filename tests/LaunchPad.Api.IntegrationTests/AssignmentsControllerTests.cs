using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Assignments;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
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
    public async Task CreateDeliverable_AsOwningCandidate_Succeeds()
    {
        var (ownerOid, assignmentId, _) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.PostAsJsonAsync($"/api/assignments/{assignmentId}/deliverables", new CreateDeliverableRequest { Title = "Sprint 1 Recap", FileName = "recap.pdf" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<DeliverableDto>(TestJsonOptions.Default);
        dto!.Title.Should().Be("Sprint 1 Recap");
    }

    [Fact]
    public async Task CreateDeliverable_AsNonOwningCandidate_IsForbidden()
    {
        var (_, assignmentId, _) = await SeedAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync($"/api/assignments/{assignmentId}/deliverables", new CreateDeliverableRequest { Title = "Hijacked", FileName = "hijacked.pdf" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
}
