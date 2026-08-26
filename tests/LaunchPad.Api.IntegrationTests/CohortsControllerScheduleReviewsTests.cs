using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Cohorts;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// POST api/cohorts/{id}/schedule-reviews — Ops's cohort-wide review scheduling action.
/// Creates up to 3 to-dos (SponsorOnCandidate, CandidateOnSponsor, ProjectEval) per
/// Active assignment in the cohort, idempotently.
/// </summary>
public class CohortsControllerScheduleReviewsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CohortsControllerScheduleReviewsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(int CohortId, int ActiveAssignmentId, int ProposedAssignmentId)> SeedCohortWithAssignmentsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var program = new Domain.Entities.Program { Name = "Schedule Reviews Program" };
        var cohort = new Cohort { Program = program, Name = "Schedule Reviews Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "schedule-sponsor@example.com", DisplayName = "Schedule Sponsor" } };
        var activeCandidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "active-candidate@example.com", DisplayName = "Active Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var proposedCandidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "proposed-candidate@example.com", DisplayName = "Proposed Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var activeProject = new Project { Cohort = cohort, Sponsor = sponsor, Name = "Active Project", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.InProgress };
        var proposedProject = new Project { Cohort = cohort, Sponsor = sponsor, Name = "Proposed Project", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.Open };

        db.AddRange(program, cohort, sponsor, activeCandidate, proposedCandidate, activeProject, proposedProject);
        await db.SaveChangesAsync();

        var activeAssignment = new Assignment { ProjectId = activeProject.ProjectId, CandidateId = activeCandidate.CandidateId, Status = AssignmentStatus.Active, StartDate = cohort.StartDate };
        var proposedAssignment = new Assignment { ProjectId = proposedProject.ProjectId, CandidateId = proposedCandidate.CandidateId, Status = AssignmentStatus.Proposed };
        db.AddRange(activeAssignment, proposedAssignment);
        await db.SaveChangesAsync();

        return (cohort.CohortId, activeAssignment.AssignmentId, proposedAssignment.AssignmentId);
    }

    [Fact]
    public async Task ScheduleReviews_CreatesThreeTodosPerActiveAssignment_AndSkipsNonActive()
    {
        var (cohortId, activeAssignmentId, proposedAssignmentId) = await SeedCohortWithAssignmentsAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var request = new ScheduleReviewsRequest { Checkpoint = Checkpoint.Midpoint, DueDate = new DateOnly(2026, 3, 1) };
        var response = await client.PostAsJsonAsync($"/api/cohorts/{cohortId}/schedule-reviews", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ScheduleReviewsResult>(TestJsonOptions.Default);
        result!.AssignmentsScheduled.Should().Be(1);
        result.TodosCreated.Should().Be(3);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var activeTodos = await db.ProjectTodos.Where(t => t.AssignmentId == activeAssignmentId).ToListAsync();
        activeTodos.Should().HaveCount(3);
        activeTodos.Select(t => t.LinkedReviewType).Should().BeEquivalentTo(
            new ReviewType?[] { ReviewType.SponsorOnCandidate, ReviewType.CandidateOnSponsor, ReviewType.ProjectEval });
        activeTodos.Should().OnlyContain(t => t.LinkedReviewCheckpoint == Checkpoint.Midpoint && t.DueDate == new DateOnly(2026, 3, 1));

        var proposedTodos = await db.ProjectTodos.Where(t => t.AssignmentId == proposedAssignmentId).ToListAsync();
        proposedTodos.Should().BeEmpty();
    }

    [Fact]
    public async Task ScheduleReviews_CalledTwiceForSameCheckpoint_DoesNotDuplicate()
    {
        var (cohortId, activeAssignmentId, _) = await SeedCohortWithAssignmentsAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);
        var request = new ScheduleReviewsRequest { Checkpoint = Checkpoint.Midpoint, DueDate = new DateOnly(2026, 3, 1) };

        var first = await client.PostAsJsonAsync($"/api/cohorts/{cohortId}/schedule-reviews", request);
        var firstResult = await first.Content.ReadFromJsonAsync<ScheduleReviewsResult>(TestJsonOptions.Default);
        firstResult!.TodosCreated.Should().Be(3);

        var second = await client.PostAsJsonAsync($"/api/cohorts/{cohortId}/schedule-reviews", request);
        var secondResult = await second.Content.ReadFromJsonAsync<ScheduleReviewsResult>(TestJsonOptions.Default);
        secondResult!.TodosCreated.Should().Be(0);
        secondResult.AssignmentsScheduled.Should().Be(0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var todos = await db.ProjectTodos.Where(t => t.AssignmentId == activeAssignmentId).ToListAsync();
        todos.Should().HaveCount(3);
    }

    [Fact]
    public async Task ScheduleReviews_ForANewCheckpoint_AddsAnotherThreeTodos()
    {
        var (cohortId, activeAssignmentId, _) = await SeedCohortWithAssignmentsAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        await client.PostAsJsonAsync($"/api/cohorts/{cohortId}/schedule-reviews",
            new ScheduleReviewsRequest { Checkpoint = Checkpoint.Midpoint, DueDate = new DateOnly(2026, 3, 1) });
        await client.PostAsJsonAsync($"/api/cohorts/{cohortId}/schedule-reviews",
            new ScheduleReviewsRequest { Checkpoint = Checkpoint.Final, DueDate = new DateOnly(2026, 5, 1) });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var todos = await db.ProjectTodos.Where(t => t.AssignmentId == activeAssignmentId).ToListAsync();
        todos.Should().HaveCount(6);
    }

    [Fact]
    public async Task ScheduleReviews_AsSponsor_IsForbidden()
    {
        var (cohortId, _, _) = await SeedCohortWithAssignmentsAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var response = await client.PostAsJsonAsync($"/api/cohorts/{cohortId}/schedule-reviews",
            new ScheduleReviewsRequest { Checkpoint = Checkpoint.Midpoint, DueDate = new DateOnly(2026, 3, 1) });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
