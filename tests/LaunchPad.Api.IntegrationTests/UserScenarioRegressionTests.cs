using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Assignments;
using LaunchPad.Application.Common;
using LaunchPad.Application.Matching;
using LaunchPad.Application.Projects;
using LaunchPad.Application.Reviews;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Regressions for bugs found by the multi-role browser scenarios in
/// docs/testing/user-test-findings.md. Each test names its finding id.
/// </summary>
public class UserScenarioRegressionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public UserScenarioRegressionTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed record World(
        int CohortA, int CohortB, int ProjectInA, int ProjectInB,
        Guid SponsorOid, Guid CandidateAOid, Guid CandidateBOid, int CandidateAId, int CandidateBId);

    private async Task<World> SeedTwoCohortsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var tag = Guid.NewGuid().ToString("N")[..8];

        var program = new Domain.Entities.Program { Name = $"Scenario {tag}" };
        var cohortA = new Cohort { Program = program, Name = $"A {tag}", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var cohortB = new Cohort { Program = program, Name = $"B {tag}", StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 12, 1), Status = CohortStatus.Active };
        var sponsorOid = Guid.NewGuid();
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = sponsorOid, Upn = $"sponsor-{tag}@example.com", DisplayName = "Scenario Sponsor" } };
        Candidate NewCandidate(Cohort cohort, Guid oid) => new()
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = oid, Upn = $"{oid}@example.com", DisplayName = "Scenario Candidate" },
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
        };
        var candidateAOid = Guid.NewGuid();
        var candidateBOid = Guid.NewGuid();
        var candidateA = NewCandidate(cohortA, candidateAOid);
        var candidateB = NewCandidate(cohortB, candidateBOid);
        Project NewProject(Cohort cohort, string name) => new()
        {
            Cohort = cohort,
            Sponsor = sponsor,
            Name = name,
            AvailabilityNeeded = Availability.PartTime,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = ProjectStatus.Open,
            MaxCandidates = 1,
        };
        var projectA = NewProject(cohortA, "Open in A");
        var projectB = NewProject(cohortB, "Open in B");

        db.AddRange(program, cohortA, cohortB, sponsor, candidateA, candidateB, projectA, projectB);
        await db.SaveChangesAsync();

        return new World(cohortA.CohortId, cohortB.CohortId, projectA.ProjectId, projectB.ProjectId,
            sponsorOid, candidateAOid, candidateBOid, candidateA.CandidateId, candidateB.CandidateId);
    }

    private HttpClient ClientAs(string roles, Guid? oid = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, (oid ?? Guid.NewGuid()).ToString());
        return client;
    }

    private async Task<int> AddAssignmentAsync(int projectId, int candidateId, AssignmentStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var assignment = new Assignment { ProjectId = projectId, CandidateId = candidateId, Status = status, StartDate = new DateOnly(2026, 1, 1) };
        db.Add(assignment);
        await db.SaveChangesAsync();
        return assignment.AssignmentId;
    }

    // F-01
    [Fact]
    public async Task Candidate_CannotOpenOrRateAProjectFromAnotherCohort()
    {
        var world = await SeedTwoCohortsAsync();
        var candidateB = ClientAs(Roles.Candidate, world.CandidateBOid);

        (await candidateB.GetAsync($"/api/projects/{world.ProjectInA}/open-detail"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await candidateB.PostAsJsonAsync($"/api/projects/{world.ProjectInA}/interest", new RateInterestRequest { Rating = 5 }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Their own cohort's project is still reachable.
        (await candidateB.GetAsync($"/api/projects/{world.ProjectInB}/open-detail"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // F-02
    [Theory]
    [InlineData(AssignmentStatus.Proposed)]
    [InlineData(AssignmentStatus.OpsApproved)]
    [InlineData(AssignmentStatus.Active)]
    public async Task OpsDeny_RefusesAnAssignmentThatIsNotAwaitingOps(AssignmentStatus status)
    {
        var world = await SeedTwoCohortsAsync();
        var assignmentId = await AddAssignmentAsync(world.ProjectInA, world.CandidateAId, status);

        var response = await ClientAs(Roles.ProgramOps).PostAsync($"/api/matching/{assignmentId}/deny", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        (await db.Assignments.SingleAsync(a => a.AssignmentId == assignmentId)).Status.Should().Be(status);
    }

    // F-03
    [Fact]
    public async Task CancelledProject_LeavesTheApprovalQueueAndCannotBeApproved()
    {
        var world = await SeedTwoCohortsAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
            var project = await db.Projects.SingleAsync(p => p.ProjectId == world.ProjectInA);
            project.ApprovalStatus = ProjectApprovalStatus.PendingOps;
            project.Status = ProjectStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        var ops = ClientAs(Roles.ProgramOps);
        var queue = await ops.GetFromJsonAsync<List<ProjectDto>>($"/api/projects/pending-approval?cohortId={world.CohortA}", TestJsonOptions.Default);
        queue.Should().NotContain(p => p.ProjectId == world.ProjectInA);

        (await ops.PostAsync($"/api/projects/{world.ProjectInA}/approve", content: null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // F-04
    [Fact]
    public async Task CreateProject_InACohortThatDoesNotExist_IsAValidationError()
    {
        var world = await SeedTwoCohortsAsync();
        var request = new CreateProjectRequest
        {
            CohortId = 987_654,
            Name = "Nowhere",
            AvailabilityNeeded = Availability.PartTime,
            MaxCandidates = 1,
        };

        var response = await ClientAs(Roles.Sponsor, world.SponsorOid).PostAsJsonAsync("/api/projects", request, TestJsonOptions.Default);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Cohort not found");
    }

    // F-05
    [Fact]
    public async Task CohortStatusChange_IsAudited()
    {
        var world = await SeedTwoCohortsAsync();
        var opsOid = Guid.NewGuid();

        var response = await ClientAs(Roles.ProgramOps, opsOid).PatchAsJsonAsync(
            $"/api/cohorts/{world.CohortB}/status", new { status = "Completed" }, TestJsonOptions.Default);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        (await db.AuditEvents.Where(e => e.EntityName == "Cohort" && e.EntityId == world.CohortB.ToString()).ToListAsync())
            .Should().ContainSingle(e => e.Action == "StatusChanged");
    }

    // F-06
    [Fact]
    public async Task SubmittingTheSameReviewTwice_IsAConflictNotADuplicateRow()
    {
        var world = await SeedTwoCohortsAsync();
        var assignmentId = await AddAssignmentAsync(world.ProjectInA, world.CandidateAId, AssignmentStatus.Active);
        var sponsor = ClientAs(Roles.Sponsor, world.SponsorOid);
        var request = new SubmitReviewRequest
        {
            AssignmentId = assignmentId,
            ReviewType = ReviewType.SponsorOnCandidate,
            Checkpoint = Checkpoint.Midpoint,
            Commitment = 3,
            Availability = 3,
            Guidance = 3,
            OutputQuality = 3,
        };

        (await sponsor.PostAsJsonAsync("/api/reviews", request, TestJsonOptions.Default)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await sponsor.PostAsJsonAsync("/api/reviews", request, TestJsonOptions.Default)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        (await db.Reviews.CountAsync(r => r.AssignmentId == assignmentId)).Should().Be(1);
    }
    // F-07
    [Fact]
    public async Task SchedulingReviews_AfterTheReviewWasAlreadySubmitted_CreatesTheTodoAlreadyCompleted()
    {
        var world = await SeedTwoCohortsAsync();
        var assignmentId = await AddAssignmentAsync(world.ProjectInA, world.CandidateAId, AssignmentStatus.Active);
        var review = new SubmitReviewRequest
        {
            AssignmentId = assignmentId,
            ReviewType = ReviewType.SponsorOnCandidate,
            Checkpoint = Checkpoint.Midpoint,
            Commitment = 4,
            Availability = 4,
            Guidance = 4,
            OutputQuality = 4,
        };
        (await ClientAs(Roles.Sponsor, world.SponsorOid).PostAsJsonAsync("/api/reviews", review, TestJsonOptions.Default))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var schedule = await ClientAs(Roles.ProgramOps).PostAsJsonAsync(
            $"/api/cohorts/{world.CohortA}/schedule-reviews", new { checkpoint = "Midpoint", dueDate = "2026-10-01" }, TestJsonOptions.Default);
        schedule.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var todos = await db.ProjectTodos.Where(t => t.AssignmentId == assignmentId && t.LinkedReviewCheckpoint == Checkpoint.Midpoint).ToListAsync();
        todos.Single(t => t.LinkedReviewType == ReviewType.SponsorOnCandidate).Status.Should().Be(TodoStatus.Completed);
        todos.Single(t => t.LinkedReviewType == ReviewType.CandidateOnSponsor).Status.Should().Be(TodoStatus.NotStarted);
    }

    // G-01
    [Fact]
    public async Task NightlySweep_StartsApprovedWorkAndCompletesFinishedWork()
    {
        var world = await SeedTwoCohortsAsync();
        var starting = await AddAssignmentAsync(world.ProjectInA, world.CandidateAId, AssignmentStatus.OpsApproved);
        var finishing = await AddAssignmentAsync(world.ProjectInB, world.CandidateBId, AssignmentStatus.Active);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        // Cohort A runs Jan-Jun 2026, cohort B Jul-Dec — so on this date A's project has
        // started and B's has not yet ended.
        var projectA = await db.Projects.SingleAsync(p => p.ProjectId == world.ProjectInA);
        projectA.StartDate = new DateOnly(2026, 1, 1);
        var projectB = await db.Projects.SingleAsync(p => p.ProjectId == world.ProjectInB);
        projectB.EndDate = new DateOnly(2026, 2, 1);
        await db.SaveChangesAsync();

        var runner = scope.ServiceProvider.GetRequiredService<IAssignmentLifecycleRunner>();
        // Counts cover every assignment in the shared test database, so assert on these two.
        var result = await runner.RunAsync(new DateOnly(2026, 3, 1));

        result.Activated.Should().BeGreaterThanOrEqualTo(1);
        (await db.Assignments.SingleAsync(a => a.AssignmentId == starting)).Status.Should().Be(AssignmentStatus.Active);
        (await db.Assignments.SingleAsync(a => a.AssignmentId == finishing)).Status.Should().Be(AssignmentStatus.Completed);
    }

    // G-01
    [Fact]
    public async Task OpsCanStartAndCompleteAnAssignmentByHand_ButOnlyFromTheRightStatus()
    {
        var world = await SeedTwoCohortsAsync();
        var assignmentId = await AddAssignmentAsync(world.ProjectInA, world.CandidateAId, AssignmentStatus.OpsApproved);
        var ops = ClientAs(Roles.ProgramOps);

        // Can't complete work that never started.
        (await ops.PostAsJsonAsync($"/api/assignments/{assignmentId}/status", new { status = "Completed" }, TestJsonOptions.Default))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await ops.PostAsJsonAsync($"/api/assignments/{assignmentId}/status", new { status = "Active", reason = "Kicked off early" }, TestJsonOptions.Default))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await ops.PostAsJsonAsync($"/api/assignments/{assignmentId}/status", new { status = "Completed" }, TestJsonOptions.Default))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Approvals still belong to the matching queue.
        (await ops.PostAsJsonAsync($"/api/assignments/{assignmentId}/status", new { status = "OpsApproved" }, TestJsonOptions.Default))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var sponsor = ClientAs(Roles.Sponsor, world.SponsorOid);
        (await sponsor.PostAsJsonAsync($"/api/assignments/{assignmentId}/status", new { status = "Active" }, TestJsonOptions.Default))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // G-02
    [Theory]
    [InlineData("payload.exe", "application/octet-stream", new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, "file type isn't accepted")]
    [InlineData("fake.pdf", "application/pdf", new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, "don't match its extension")]
    public async Task Deliverable_UploadsMustBeAnAcceptedTypeAndLookLikeIt(
        string fileName, string contentType, byte[] content, string expectedMessage)
    {
        var world = await SeedTwoCohortsAsync();
        var assignmentId = await AddAssignmentAsync(world.ProjectInA, world.CandidateAId, AssignmentStatus.Active);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Deliverable"), "title");
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        var response = await ClientAs(Roles.Candidate, world.CandidateAOid)
            .PostAsync($"/api/assignments/{assignmentId}/deliverables", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(expectedMessage);
    }

    // G-02
    [Fact]
    public async Task Deliverable_ARealPdfIsStillAccepted()
    {
        var world = await SeedTwoCohortsAsync();
        var assignmentId = await AddAssignmentAsync(world.ProjectInA, world.CandidateAId, AssignmentStatus.Active);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Deliverable"), "title");
        var fileContent = new ByteArrayContent([.. "%PDF-1.7 report"u8]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "report.pdf");

        (await ClientAs(Roles.Candidate, world.CandidateAOid).PostAsync($"/api/assignments/{assignmentId}/deliverables", form))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // G-02
    [Fact]
    public async Task Avatar_MustActuallyBeTheImageTypeItClaims()
    {
        var candidate = ClientAs(Roles.Candidate, Guid.NewGuid());

        var lying = new ByteArrayContent([.. "<script>alert(1)</script>"u8]);
        lying.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        (await candidate.PostAsync("/api/me/avatar", lying)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var realPng = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01]);
        realPng.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        (await candidate.PostAsync("/api/me/avatar", realPng)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
