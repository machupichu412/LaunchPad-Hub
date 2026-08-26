using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Reviews;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// CLAUDE.md's redaction rule has no exception for the sponsor who submitted the
/// ratings — SponsorReviewDto (see ReviewDto.cs) never carries OverallScore or the
/// four numeric dimensions, even in the response to the person who just typed them in.
/// </summary>
public class ReviewsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public ReviewsControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(Guid OwnerOid, Guid CandidateOid, int AssignmentId)> SeedActiveAssignmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var ownerOid = Guid.NewGuid();
        var candidateOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Review Test Program" };
        var cohort = new Cohort { Program = program, Name = "Review Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var owner = new Sponsor { AppUser = new AppUser { EntraObjectId = ownerOid, Upn = "review-owner@example.com", DisplayName = "Review Owner Sponsor" } };
        var candidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = candidateOid, Upn = "review-candidate@example.com", DisplayName = "Review Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var project = new Project { Cohort = cohort, Sponsor = owner, Name = "Reviewed Project", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.InProgress };

        db.AddRange(program, cohort, owner, candidate, project);
        await db.SaveChangesAsync();

        var assignment = new Assignment { ProjectId = project.ProjectId, CandidateId = candidate.CandidateId, Status = AssignmentStatus.Active, StartDate = cohort.StartDate };
        db.Add(assignment);
        await db.SaveChangesAsync();

        return (ownerOid, candidateOid, assignment.AssignmentId);
    }

    private static SubmitReviewRequest MakeRequest(int assignmentId, ReviewType reviewType = ReviewType.SponsorOnCandidate) => new()
    {
        AssignmentId = assignmentId,
        ReviewType = reviewType,
        Checkpoint = Checkpoint.Midpoint,
        Commitment = 4,
        Availability = 5,
        Guidance = 3,
        OutputQuality = 4,
        Comments = "Doing well overall.",
        Strengths = "Fast learner, clear communicator.",
        GrowthAreas = "Could ask for help sooner.",
        RecommendConversion = true,
    };

    [Fact]
    public async Task Submit_AsOwningSponsorOnActiveAssignment_Succeeds()
    {
        var (ownerOid, _, assignmentId) = await SeedActiveAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.PostAsJsonAsync("/api/reviews", MakeRequest(assignmentId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<SponsorReviewDto>(TestJsonOptions.Default);
        dto!.Strengths.Should().Be("Fast learner, clear communicator.");
        dto.RecommendConversion.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var auditEvents = await db.AuditEvents.Where(e => e.EntityName == "Review" && e.EntityId == dto.ReviewId.ToString()).ToListAsync();
        auditEvents.Should().Contain(e => e.Action == "Submit");
    }

    [Fact]
    public async Task Submit_ResponseBody_NeverContainsOverallScoreOrNumericDimensions()
    {
        var (ownerOid, _, assignmentId) = await SeedActiveAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.PostAsJsonAsync("/api/reviews", MakeRequest(assignmentId));
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContainEquivalentOf("overallScore");
        body.Should().NotContainEquivalentOf("commitment");
        body.Should().NotContainEquivalentOf("\"availability\"");
        body.Should().NotContainEquivalentOf("guidance");
        body.Should().NotContainEquivalentOf("outputQuality");
        body.Should().ContainEquivalentOf("strengths").And.ContainEquivalentOf("growthAreas").And.ContainEquivalentOf("recommendConversion");
    }

    [Fact]
    public async Task Submit_OnANonActiveAssignment_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var ownerOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Not Active Program" };
        var cohort = new Cohort { Program = program, Name = "Not Active Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var owner = new Sponsor { AppUser = new AppUser { EntraObjectId = ownerOid, Upn = "not-active-owner@example.com", DisplayName = "Not Active Owner" } };
        var candidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "not-active-candidate@example.com", DisplayName = "Not Active Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var project = new Project { Cohort = cohort, Sponsor = owner, Name = "Not Active Project", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.Open };

        db.AddRange(program, cohort, owner, candidate, project);
        await db.SaveChangesAsync();

        var assignment = new Assignment { ProjectId = project.ProjectId, CandidateId = candidate.CandidateId, Status = AssignmentStatus.Proposed };
        db.Add(assignment);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.PostAsJsonAsync("/api/reviews", MakeRequest(assignment.AssignmentId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Submit_AsNonOwningSponsor_IsForbidden()
    {
        var (_, _, assignmentId) = await SeedActiveAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/reviews", MakeRequest(assignmentId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetByAssignment_AsOwningSponsor_ReturnsSubmittedReview()
    {
        var (ownerOid, _, assignmentId) = await SeedActiveAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        await client.PostAsJsonAsync("/api/reviews", MakeRequest(assignmentId));
        var response = await client.GetAsync($"/api/reviews/assignment/{assignmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reviews = await response.Content.ReadFromJsonAsync<List<SponsorReviewDto>>(TestJsonOptions.Default);
        reviews.Should().ContainSingle(r => r.AssignmentId == assignmentId);
    }

    [Theory]
    [InlineData(ReviewType.CandidateOnSponsor)]
    [InlineData(ReviewType.ProjectEval)]
    public async Task Submit_AsOwningCandidate_Succeeds(ReviewType reviewType)
    {
        var (_, candidateOid, assignmentId) = await SeedActiveAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, candidateOid.ToString());

        var response = await client.PostAsJsonAsync("/api/reviews", MakeRequest(assignmentId, reviewType));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<SponsorReviewDto>(TestJsonOptions.Default);
        dto!.Strengths.Should().Be("Fast learner, clear communicator.");
    }

    [Fact]
    public async Task Submit_AsNonOwningCandidate_IsForbidden()
    {
        var (_, _, assignmentId) = await SeedActiveAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/reviews", MakeRequest(assignmentId, ReviewType.CandidateOnSponsor));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Submit_CandidateOnSponsor_AsSponsor_IsForbidden()
    {
        var (ownerOid, _, assignmentId) = await SeedActiveAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var response = await client.PostAsJsonAsync("/api/reviews", MakeRequest(assignmentId, ReviewType.CandidateOnSponsor));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Submit_SponsorOnCandidate_AsCandidate_IsForbidden()
    {
        var (_, candidateOid, assignmentId) = await SeedActiveAssignmentAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, candidateOid.ToString());

        var response = await client.PostAsJsonAsync("/api/reviews", MakeRequest(assignmentId, ReviewType.SponsorOnCandidate));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(ReviewType.SponsorOnCandidate)]
    [InlineData(ReviewType.CandidateOnSponsor)]
    [InlineData(ReviewType.ProjectEval)]
    public async Task Submit_AutoCompletesLinkedTodo(ReviewType reviewType)
    {
        var (ownerOid, candidateOid, assignmentId) = await SeedActiveAssignmentAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
            db.Add(new ProjectTodo
            {
                AssignmentId = assignmentId,
                Title = "Submit your midpoint review",
                Status = TodoStatus.NotStarted,
                LinkedReviewType = reviewType,
                LinkedReviewCheckpoint = Checkpoint.Midpoint,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var (role, oid) = reviewType == ReviewType.SponsorOnCandidate ? (Roles.Sponsor, ownerOid) : (Roles.Candidate, candidateOid);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var response = await client.PostAsJsonAsync("/api/reviews", MakeRequest(assignmentId, reviewType));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var todo = await verifyDb.ProjectTodos.SingleAsync(t => t.AssignmentId == assignmentId && t.LinkedReviewType == reviewType);
        todo.Status.Should().Be(TodoStatus.Completed);
        todo.CompletedUtc.Should().NotBeNull();
    }
}
