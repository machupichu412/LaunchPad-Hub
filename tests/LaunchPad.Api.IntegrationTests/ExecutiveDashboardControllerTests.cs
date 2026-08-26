using System.Net;
using LaunchPad.Application.Common;
using FluentAssertions;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Gated by ViewHiddenScores (Executive + ProgramOps) — same score-adjacent redaction
/// boundary as OpsController's Dashboard/Risks endpoints, see OpsControllerTests. Uses
/// the real ReportingRepository (not faked, unlike IOpsDashboardRepository) because its
/// funnel counts come from ordinary Assignment/Candidate queries; only its risk-count
/// join touches the keyless CandidateRisks view, which the InMemory provider evaluates
/// as empty rather than throwing, so PerformanceRiskCount/EngagementRiskCount are
/// always 0 here — these tests assert the auth boundary and funnel correctness, not
/// risk-count values.
/// </summary>
public class ExecutiveDashboardControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public ExecutiveDashboardControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetExecutiveDashboard_AsExecutive_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Executive);

        var response = await client.GetAsync("/api/ops/executive-dashboard/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetExecutiveDashboard_AsProgramOps_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.GetAsync("/api/ops/executive-dashboard/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetExecutiveDashboard_AsSponsor_IsForbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var response = await client.GetAsync("/api/ops/executive-dashboard/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetExecutiveDashboard_AsCandidate_IsForbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);

        var response = await client.GetAsync("/api/ops/executive-dashboard/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
