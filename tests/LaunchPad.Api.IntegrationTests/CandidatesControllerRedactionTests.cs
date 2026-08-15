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
/// CLAUDE.md: "Every endpoint returning a CandidateDto needs an integration test per
/// role asserting the serialized response contains no score field for unauthorized
/// roles." This exercises the full pipeline — auth, policy, controller, mapper — not
/// just the mapper in isolation (see LaunchPad.Application.Tests for that).
/// </summary>
public class CandidatesControllerRedactionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CandidatesControllerRedactionTests(CustomWebApplicationFactory factory) => _factory = factory;

    // Risk data itself comes from TestCandidateRepositoryWithFakeRisk (registered in
    // CustomWebApplicationFactory) — CandidateRisk is keyless and can't be seeded via
    // .Add(). This only needs to seed a real Candidate for the mapper to attach to.
    private async Task<int> SeedCandidateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var program = new Domain.Entities.Program { Name = "Test Program" };
        var cohort = new Cohort { Program = program, Name = "Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var candidate = new Candidate
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "redaction-test@example.com", DisplayName = "Jordan Rivera" },
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
        };

        db.AddRange(program, cohort, candidate);
        await db.SaveChangesAsync();

        return candidate.CandidateId;
    }

    [Theory]
    [InlineData(Roles.Sponsor)]
    [InlineData(Roles.HiringManager)]
    public async Task Get_OmitsHiddenScoreFields_ForUnauthorizedRoles(string role)
    {
        var candidateId = await SeedCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);

        var json = await client.GetStringAsync($"/api/candidates/{candidateId}");

        json.Should().NotContainAny("averageScore", "hasPerformanceRisk", "hasEngagementRisk");
    }

    [Theory]
    [InlineData(Roles.Executive)]
    [InlineData(Roles.ProgramOps)]
    public async Task Get_IncludesScoreFields_ForAuthorizedRoles(string role)
    {
        var candidateId = await SeedCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);

        var dto = await client.GetFromJsonAsync<CandidateDto>($"/api/candidates/{candidateId}", TestJsonOptions.Default);

        dto!.AverageScore.Should().NotBeNull();
        dto.HasPerformanceRisk.Should().BeTrue();
    }

    [Theory]
    [InlineData(Roles.Sponsor)]
    [InlineData(Roles.HiringManager)]
    public async Task Get_OmitsSuggestedHireOutcome_ForUnauthorizedRoles(string role)
    {
        var candidateId = await SeedCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);

        var json = await client.GetStringAsync($"/api/candidates/{candidateId}");

        json.Should().NotContain("suggestedHireOutcome");
    }
}
