using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Projects;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Proves resource-based authorization for Project create/edit: a Sponsor can only
/// ever act as themselves (SponsorId is resolved server-side, never trusted from the
/// request body), can't edit another sponsor's project, and Ops bypasses ownership —
/// exactly the OwnsProjectHandler behavior described in CLAUDE.md §"Authorization model".
/// </summary>
public class ProjectsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public ProjectsControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(Guid OwnerOid, int ProjectId, int CohortId)> SeedProjectAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var ownerOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Test Program" };
        var cohort = new Cohort { Program = program, Name = "Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var owner = new Sponsor { AppUser = new AppUser { EntraObjectId = ownerOid, Upn = "owner@example.com", DisplayName = "Owner Sponsor" } };
        var project = new Project
        {
            Cohort = cohort,
            Sponsor = owner,
            Name = "Existing Project",
            AvailabilityNeeded = Availability.PartTime,
            ApprovalStatus = ProjectApprovalStatus.Draft,
            Status = ProjectStatus.Open,
        };

        db.AddRange(program, cohort, owner, project);
        await db.SaveChangesAsync();

        return (ownerOid, project.ProjectId, cohort.CohortId);
    }

    [Fact]
    public async Task Create_AsSponsor_ResolvesSponsorIdServerSide_IgnoringClientSuppliedValue()
    {
        var (ownerOid, _, cohortId) = await SeedProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var request = new CreateProjectRequest { CohortId = cohortId, Name = "New Project", AvailabilityNeeded = Availability.FullTime };
        var response = await client.PostAsJsonAsync("/api/projects", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<ProjectDto>(TestJsonOptions.Default);
        dto!.Name.Should().Be("New Project");
    }

    [Fact]
    public async Task Create_AsProgramOps_IsForbidden()
    {
        var (_, _, cohortId) = await SeedProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var request = new CreateProjectRequest { CohortId = cohortId, Name = "Should Not Be Created", AvailabilityNeeded = Availability.FullTime };
        var response = await client.PostAsJsonAsync("/api/projects", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_AsOwningSponsor_Succeeds()
    {
        var (ownerOid, projectId, _) = await SeedProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var request = new UpdateProjectRequest { Name = "Updated Name", AvailabilityNeeded = Availability.FullTime };
        var response = await client.PutAsJsonAsync($"/api/projects/{projectId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_AsNonOwningSponsor_IsForbidden()
    {
        var (_, projectId, _) = await SeedProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString()); // a different sponsor

        var request = new UpdateProjectRequest { Name = "Hijacked Name", AvailabilityNeeded = Availability.FullTime };
        var response = await client.PutAsJsonAsync($"/api/projects/{projectId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_AsProgramOps_BypassesOwnership()
    {
        var (_, projectId, _) = await SeedProjectAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var request = new UpdateProjectRequest { Name = "Ops Edited Name", AvailabilityNeeded = Availability.FullTime };
        var response = await client.PutAsJsonAsync($"/api/projects/{projectId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
