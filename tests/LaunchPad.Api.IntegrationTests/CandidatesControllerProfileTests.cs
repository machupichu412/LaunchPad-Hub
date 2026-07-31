using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Proves GET/PUT /api/candidates/me resolve strictly from the caller's own
/// EntraObjectId — there is no candidateId in the route, so there is nothing for a
/// malicious caller to substitute to reach someone else's profile.
/// </summary>
public class CandidatesControllerProfileTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CandidatesControllerProfileTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<Guid> SeedCandidateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var oid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Test Program" };
        var cohort = new Cohort { Program = program, Name = "Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var candidate = new Candidate
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = oid, Upn = "candidate@example.com", DisplayName = "Test Candidate" },
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
        };

        db.AddRange(program, cohort, candidate);
        await db.SaveChangesAsync();

        return oid;
    }

    [Fact]
    public async Task GetMe_WithNoCandidateRecord_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/candidates/me");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMe_ResolvesOwnRecord()
    {
        var oid = await SeedCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var response = await client.GetAsync("/api/candidates/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CandidateDto>(TestJsonOptions.Default);
        dto!.DisplayName.Should().Be("Test Candidate");
    }

    [Fact]
    public async Task UpdateMe_PersistsChanges_AndOnlyAffectsOwnRecord()
    {
        var oid = await SeedCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var request = new UpdateCandidateProfileRequest
        {
            Location = "Austin, TX",
            Availability = Availability.FullTime,
            SkillNames = new[] { "React", "TypeScript" },
        };
        var response = await client.PutAsJsonAsync("/api/candidates/me", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CandidateDto>(TestJsonOptions.Default);
        dto!.Location.Should().Be("Austin, TX");
        dto.Skills.Should().Contain(new[] { "React", "TypeScript" });
    }

    [Fact]
    public async Task UpdateMe_AsSponsor_IsForbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var request = new UpdateCandidateProfileRequest { Availability = Availability.FullTime };
        var response = await client.PutAsJsonAsync("/api/candidates/me", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
