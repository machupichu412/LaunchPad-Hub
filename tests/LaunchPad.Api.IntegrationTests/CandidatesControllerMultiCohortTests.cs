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
/// GET api/candidates?cohortIds=... — the additive multi-cohort filter behind Talent
/// Pipeline's cohort dropdown (see TalentPipeline.tsx). An empty/missing cohortIds
/// param means every cohort ("All cohorts" in the UI).
/// </summary>
public class CandidatesControllerMultiCohortTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CandidatesControllerMultiCohortTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(int CohortAId, int CohortBId)> SeedTwoCohortsWithOneCandidateEachAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var program = new Domain.Entities.Program { Name = "Multi-Cohort Test Program" };
        var cohortA = new Cohort { Program = program, Name = "Cohort A", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var cohortB = new Cohort { Program = program, Name = "Cohort B", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var candidateA = new Candidate { Cohort = cohortA, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "cohort-a@example.com", DisplayName = "Cohort A Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var candidateB = new Candidate { Cohort = cohortB, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "cohort-b@example.com", DisplayName = "Cohort B Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };

        db.AddRange(program, cohortA, cohortB, candidateA, candidateB);
        await db.SaveChangesAsync();

        return (cohortA.CohortId, cohortB.CohortId);
    }

    [Fact]
    public async Task Get_WithOneCohortId_ReturnsOnlyThatCohortsCandidates()
    {
        var (cohortAId, _) = await SeedTwoCohortsWithOneCandidateEachAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var candidates = await client.GetFromJsonAsync<List<CandidateDto>>($"/api/candidates?cohortIds={cohortAId}", TestJsonOptions.Default);

        candidates.Should().OnlyContain(c => c.DisplayName == "Cohort A Candidate");
    }

    [Fact]
    public async Task Get_WithBothCohortIds_ReturnsBothCohortsCandidates()
    {
        var (cohortAId, cohortBId) = await SeedTwoCohortsWithOneCandidateEachAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var candidates = await client.GetFromJsonAsync<List<CandidateDto>>(
            $"/api/candidates?cohortIds={cohortAId},{cohortBId}", TestJsonOptions.Default);

        candidates!.Select(c => c.DisplayName).Should().Contain(["Cohort A Candidate", "Cohort B Candidate"]);
    }

    [Fact]
    public async Task Get_WithNoCohortIds_ReturnsCandidatesFromEveryCohort()
    {
        var (_, _) = await SeedTwoCohortsWithOneCandidateEachAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var candidates = await client.GetFromJsonAsync<List<CandidateDto>>("/api/candidates", TestJsonOptions.Default);

        candidates!.Select(c => c.DisplayName).Should().Contain(["Cohort A Candidate", "Cohort B Candidate"]);
    }

    [Fact]
    public async Task Get_AsCandidateRole_IsForbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);

        var response = await client.GetAsync("/api/candidates");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }
}
