using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// POST /api/candidates/me — self-service onboarding. Each of these test classes gets
/// its own CustomWebApplicationFactory instance (its own in-memory DB), which matters
/// here specifically: GetActiveAsync scans every Cohort in the database, not just what
/// one test seeded, so "exactly one active cohort" / "zero active" / "multiple active"
/// scenarios must each live in their own isolated class — sharing a DB across tests
/// with different cohort-activity expectations would make them order-dependent.
/// </summary>
public class CandidatesControllerCreateMeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CandidatesControllerCreateMeTests(CustomWebApplicationFactory factory) => _factory = factory;

    // Idempotent — multiple tests in this class share one CustomWebApplicationFactory
    // (one in-memory DB), and GetActiveAsync scans every Cohort in it. If each test
    // inserted its own new Active cohort, the second test to run would see "multiple
    // active cohorts" instead of one. Reusing a single fixed-name cohort/skill keeps
    // the "exactly one active cohort" precondition true no matter how many tests
    // (or which order) call this.
    private async Task<int> SeedActiveCohortAndSkillAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var existingSkill = await db.Skills.FirstOrDefaultAsync(s => s.Name == "React");
        if (existingSkill is not null) return existingSkill.SkillId;

        var program = new Domain.Entities.Program { Name = "Onboarding Test Program" };
        var cohort = new Cohort { Program = program, Name = "Onboarding Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var category = new SkillCategory { Name = "Engineering" };
        var skill = new Skill { Name = "React", SkillCategory = category };

        db.AddRange(program, cohort, category, skill);
        await db.SaveChangesAsync();

        return skill.SkillId;
    }

    [Fact]
    public async Task CreateMe_AsNewCandidate_CreatesProfile_AndTiesToCallersAppUserOnNextRequest()
    {
        var skillId = await SeedActiveCohortAndSkillAsync();
        var oid = Guid.NewGuid();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var request = new CreateCandidateProfileRequest
        {
            Location = "Seattle, WA",
            Availability = Availability.PartTime,
            Bio = "Aspiring engineer.",
            SkillIds = new[] { skillId },
        };
        var createResponse = await client.PostAsJsonAsync("/api/candidates/me", request);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CandidateDto>(TestJsonOptions.Default);
        created!.Location.Should().Be("Seattle, WA");
        created.Skills.Should().Contain("React");

        // Proves the Entra ID is tied to the new profile for subsequent requests —
        // no separate "linking" step, just GetByEntraObjectIdAsync finding it.
        var getResponse = await client.GetAsync("/api/candidates/me");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<CandidateDto>(TestJsonOptions.Default);
        fetched!.CandidateId.Should().Be(created.CandidateId);
    }

    /// <summary>Same inline-provisioning proof as CohortsControllerTests.Create_AsProgramOps_ProvisionsSharePointFolder,
    /// extended to the candidate self-onboarding path.</summary>
    [Fact]
    public async Task CreateMe_AsNewCandidate_ProvisionsSharePointFolder()
    {
        var skillId = await SeedActiveCohortAndSkillAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var request = new CreateCandidateProfileRequest { Availability = Availability.PartTime, SkillIds = new[] { skillId } };
        var response = await client.PostAsJsonAsync("/api/candidates/me", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CandidateDto>(TestJsonOptions.Default);
        created!.SharePointFolderWebUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateMe_WhenProfileAlreadyExists_ReturnsConflict()
    {
        await SeedActiveCohortAndSkillAsync();
        var oid = Guid.NewGuid();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var request = new CreateCandidateProfileRequest { Availability = Availability.PartTime };
        var first = await client.PostAsJsonAsync("/api/candidates/me", request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/candidates/me", request);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateMe_WithUnknownSkillId_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var request = new CreateCandidateProfileRequest { Availability = Availability.PartTime, SkillIds = new[] { 999999 } };
        var response = await client.PostAsJsonAsync("/api/candidates/me", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateMe_AsSponsor_IsForbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var request = new CreateCandidateProfileRequest { Availability = Availability.PartTime };
        var response = await client.PostAsJsonAsync("/api/candidates/me", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

public class CandidatesControllerCreateMeNoCohortTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CandidatesControllerCreateMeNoCohortTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateMe_WithNoActiveCohort_ReturnsConflict()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
            var program = new Domain.Entities.Program { Name = "Planned-Only Program" };
            var cohort = new Cohort { Program = program, Name = "Not Yet Active", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2027, 1, 1), Status = CohortStatus.Planned };
            db.AddRange(program, cohort);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var request = new CreateCandidateProfileRequest { Availability = Availability.PartTime };
        var response = await client.PostAsJsonAsync("/api/candidates/me", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}

public class CandidatesControllerCreateMeMultipleCohortsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CandidatesControllerCreateMeMultipleCohortsTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateMe_WithMultipleActiveCohorts_ReturnsConflict()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
            var program = new Domain.Entities.Program { Name = "Parallel Cohorts Program" };
            var cohortA = new Cohort { Program = program, Name = "Spring", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 5, 1), Status = CohortStatus.Active };
            var cohortB = new Cohort { Program = program, Name = "Fall", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2027, 1, 1), Status = CohortStatus.Active };
            db.AddRange(program, cohortA, cohortB);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var request = new CreateCandidateProfileRequest { Availability = Availability.PartTime };
        var response = await client.PostAsJsonAsync("/api/candidates/me", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
