using System.Net;
using LaunchPad.Application.Common;
using FluentAssertions;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Dashboard/Risks are gated by ViewHiddenScores (Executive + ProgramOps) — the same
/// class of redacted, score-adjacent data CLAUDE.md's redaction rule covers. These
/// tests prove the authorization boundary; see FakeOpsDashboardRepository for why
/// aggregation correctness isn't asserted here (CandidateRisk is keyless/unseedable).
/// </summary>
public class OpsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public OpsControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetDashboard_AsProgramOps_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.GetAsync("/api/ops/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDashboard_AsExecutive_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Executive);

        var response = await client.GetAsync("/api/ops/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDashboard_AsCandidate_IsForbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);

        var response = await client.GetAsync("/api/ops/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRisks_AsProgramOps_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.GetAsync("/api/ops/risks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRisks_AsSponsor_IsForbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var response = await client.GetAsync("/api/ops/risks");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
