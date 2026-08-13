using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Search;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Per-role scoping for the header search — a Candidate never sees another
/// candidate (or any project outside their own cohort's marketplace), and a Sponsor
/// never sees another sponsor's project. Program Ops / Executive / Hiring Manager
/// scoping (which hinges on SearchController's hardcoded DemoCohortId) lives in
/// SearchControllerOpsScopedTests, isolated so its cohort is guaranteed CohortId 1.
/// </summary>
public class SearchControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public SearchControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_AsCandidate_ReturnsOnlyOpenProjectsInTheirOwnCohort_AndNeverCandidates()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var candidateOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Candidate Search Program" };
        var cohort = new Cohort { Program = program, Name = "Candidate Search Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "cs-sponsor@example.com", DisplayName = "CS Sponsor" } };
        var candidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = candidateOid, Upn = "cs-candidate@example.com", DisplayName = "CS Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };
        var openProject = new Project { Cohort = cohort, Sponsor = sponsor, Name = "Searchable React Rebuild", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Approved, Status = ProjectStatus.Open };
        var draftProject = new Project { Cohort = cohort, Sponsor = sponsor, Name = "Searchable Draft Project", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Draft, Status = ProjectStatus.Open };
        var otherCandidate = new Candidate { Cohort = cohort, AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "cs-other@example.com", DisplayName = "Searchable Other Candidate" }, Availability = Availability.PartTime, Status = CandidateStatus.InProgress };

        db.AddRange(program, cohort, sponsor, candidate, openProject, draftProject, otherCandidate);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, candidateOid.ToString());

        var response = await client.GetAsync("/api/search?q=Searchable");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<SearchResultDto>>(TestJsonOptions.Default);
        results.Should().ContainSingle(r => r.Type == "Project" && r.Url == $"/marketplace/{openProject.ProjectId}");
        results.Should().NotContain(r => r.Title == "Searchable Draft Project"); // not Approved+Open
        results.Should().NotContain(r => r.Type == "Candidate"); // a Candidate never sees other candidates
    }

    [Fact]
    public async Task Get_AsSponsor_ReturnsOnlyTheirOwnProjects_NeverAnotherSponsorsProject()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var sponsorOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Sponsor Search Program" };
        var cohort = new Cohort { Program = program, Name = "Sponsor Search Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var owner = new Sponsor { AppUser = new AppUser { EntraObjectId = sponsorOid, Upn = "ss-owner@example.com", DisplayName = "SS Owner" } };
        var otherSponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "ss-other@example.com", DisplayName = "SS Other" } };
        var ownProject = new Project { Cohort = cohort, Sponsor = owner, Name = "Owned Searchable Project", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Draft, Status = ProjectStatus.Open };
        var otherProject = new Project { Cohort = cohort, Sponsor = otherSponsor, Name = "Not Mine Searchable Project", AvailabilityNeeded = Availability.PartTime, ApprovalStatus = ProjectApprovalStatus.Draft, Status = ProjectStatus.Open };

        db.AddRange(program, cohort, owner, otherSponsor, ownProject, otherProject);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, sponsorOid.ToString());

        var response = await client.GetAsync("/api/search?q=Searchable");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<SearchResultDto>>(TestJsonOptions.Default);
        results.Should().ContainSingle(r => r.Type == "Project" && r.Title == "Owned Searchable Project" && r.Url == $"/projects/{ownProject.ProjectId}/matches");
        results.Should().NotContain(r => r.Title == "Not Mine Searchable Project");
    }

    [Fact]
    public async Task Get_WithTermUnderTwoCharacters_ReturnsEmptyWithoutQuerying()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/search?q=a");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<SearchResultDto>>(TestJsonOptions.Default);
        results.Should().BeEmpty();
    }
}
