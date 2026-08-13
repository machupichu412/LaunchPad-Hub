using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Sponsors;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>POST /api/sponsors/me — self-service onboarding, mirroring
/// CandidatesControllerCreateMeTests. Simpler than the candidate case: no cohort
/// resolution, since Sponsor isn't cohort-scoped (see CreateSponsorProfileRequest).</summary>
public class SponsorsControllerCreateMeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public SponsorsControllerCreateMeTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateMe_AsNewSponsor_CreatesProfile_AndTiesToCallersAppUserOnNextRequest()
    {
        var oid = Guid.NewGuid();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var request = new CreateSponsorProfileRequest { Organization = "Contoso", Title = "VP Engineering" };
        var createResponse = await client.PostAsJsonAsync("/api/sponsors/me", request);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<SponsorDto>(TestJsonOptions.Default);
        created!.Organization.Should().Be("Contoso");
        created.Title.Should().Be("VP Engineering");

        var getResponse = await client.GetAsync("/api/sponsors/me");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<SponsorDto>(TestJsonOptions.Default);
        fetched!.SponsorId.Should().Be(created.SponsorId);
    }

    [Fact]
    public async Task CreateMe_WhenProfileAlreadyExists_ReturnsConflict()
    {
        var oid = Guid.NewGuid();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var request = new CreateSponsorProfileRequest { Organization = "Contoso" };
        var first = await client.PostAsJsonAsync("/api/sponsors/me", request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/sponsors/me", request);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateMe_AsCandidate_IsForbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);

        var request = new CreateSponsorProfileRequest { Organization = "Contoso" };
        var response = await client.PostAsJsonAsync("/api/sponsors/me", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
