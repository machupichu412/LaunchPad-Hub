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
/// Cohort management is Program Ops's admin capability (see the build-out plan):
/// anyone on ViewTalentPipeline can read the list, but only ProgramOps can create one.
/// </summary>
public class CohortsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CohortsControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task SeedProgramAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        if (await db.Programs.AnyAsync()) return;

        db.Add(new Domain.Entities.Program { Name = "Test Program", IsActive = true });
        await db.SaveChangesAsync();
    }

    private async Task<int> SeedCohortWithCountsAsync(int candidateCount, int projectCount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var program = new Domain.Entities.Program { Name = "Counted Program" };
        var cohort = new Cohort { Program = program, Name = "Counted Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "sponsor@example.com", DisplayName = "Sponsor" } };

        db.AddRange(program, cohort, sponsor);
        for (var i = 0; i < candidateCount; i++)
        {
            db.Add(new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = $"c{i}@example.com", DisplayName = $"Candidate {i}" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress });
        }
        for (var i = 0; i < projectCount; i++)
        {
            db.Add(new Project { Cohort = cohort, Sponsor = sponsor, Name = $"Project {i}", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Draft, Status = ProjectStatus.Open });
        }
        await db.SaveChangesAsync();

        return cohort.CohortId;
    }

    [Fact]
    public async Task Create_AsProgramOps_Succeeds()
    {
        await SeedProgramAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var request = new CreateCohortRequest { Name = "New Cohort", StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 9, 1) };
        var response = await client.PostAsJsonAsync("/api/cohorts", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CohortDto>(TestJsonOptions.Default);
        dto!.Name.Should().Be("New Cohort");
        dto.CandidateCount.Should().Be(0);
        dto.ProjectCount.Should().Be(0);
    }

    [Fact]
    public async Task Create_AsSponsor_IsForbidden()
    {
        await SeedProgramAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var request = new CreateCohortRequest { Name = "Should Not Be Created", StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 9, 1) };
        var response = await client.PostAsJsonAsync("/api/cohorts", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_ReturnsCorrectCandidateAndProjectCounts()
    {
        var cohortId = await SeedCohortWithCountsAsync(candidateCount: 2, projectCount: 3);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.GetAsync("/api/cohorts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cohorts = await response.Content.ReadFromJsonAsync<List<CohortDto>>(TestJsonOptions.Default);
        var found = cohorts!.Single(c => c.CohortId == cohortId);
        found.CandidateCount.Should().Be(2);
        found.ProjectCount.Should().Be(3);
    }
}
