using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Search;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// SearchController's ProgramOps/Executive/HiringManager scope hinges on a hardcoded
/// DemoCohortId (the same simplification TalentPipeline/OpsProjects/ApprovalQueue
/// already make pending real cohort selection) rather than the caller's own row, so
/// these tests need their seeded cohort to land on CohortId 1. Isolated in its own
/// class (own CustomWebApplicationFactory/DB) with an idempotent seed helper so
/// running more than one [Fact] here never mints a second cohort and breaks that
/// assumption — the exact bug this repo hit once before with a non-idempotent seed
/// helper (see CandidatesControllerCreateMeTests' history).
/// </summary>
public class SearchControllerOpsScopedTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public SearchControllerOpsScopedTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(int ProjectId, int CandidateId)> SeedDemoCohortDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var existingProject = await db.Projects.FirstOrDefaultAsync(p => p.Name == "Ops Searchable Project");
        if (existingProject is not null)
        {
            var existingCandidate = await db.Candidates.FirstAsync(c => c.AppUser.DisplayName == "Ops Searchable Candidate");
            return (existingProject.ProjectId, existingCandidate.CandidateId);
        }

        var program = new Domain.Entities.Program { Name = "Ops Search Program" };
        var cohort = new Cohort { Program = program, Name = "Ops Search Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "os-sponsor@example.com", DisplayName = "OS Sponsor" } };
        var project = new Project { Cohort = cohort, Sponsor = sponsor, Name = "Ops Searchable Project", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.Open };
        var candidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "os-candidate@example.com", DisplayName = "Ops Searchable Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };

        db.AddRange(program, cohort, sponsor, project, candidate);
        await db.SaveChangesAsync();

        cohort.CohortId.Should().Be(1, "SearchController's DemoCohortId is hardcoded to 1 — this cohort must be the first ever created in this class's DB");

        return (project.ProjectId, candidate.CandidateId);
    }

    [Fact]
    public async Task Get_AsProgramOps_ReturnsBothProjectsAndCandidatesInTheDemoCohort()
    {
        var (projectId, _) = await SeedDemoCohortDataAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.GetAsync("/api/search?q=Ops Searchable");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<SearchResultDto>>(TestJsonOptions.Default);
        results.Should().Contain(r => r.Type == "Project" && r.Title == "Ops Searchable Project" && r.Url == $"/ops/projects/{projectId}");
        results.Should().Contain(r => r.Type == "Candidate" && r.Title == "Ops Searchable Candidate" && r.Url == "/pipeline");
    }

    [Fact]
    public async Task Get_AsExecutive_ReturnsCandidatesButNeverProjects()
    {
        await SeedDemoCohortDataAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Executive);

        var response = await client.GetAsync("/api/search?q=Ops Searchable");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<SearchResultDto>>(TestJsonOptions.Default);
        results.Should().Contain(r => r.Type == "Candidate");
        results.Should().NotContain(r => r.Type == "Project");
    }
}
