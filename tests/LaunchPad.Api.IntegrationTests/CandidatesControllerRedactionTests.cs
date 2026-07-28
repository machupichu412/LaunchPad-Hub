using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
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

    [Theory]
    [InlineData(Roles.Sponsor)]
    [InlineData(Roles.HiringManager)]
    public async Task Get_OmitsHiddenScoreFields_ForUnauthorizedRoles(string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);

        var json = await client.GetStringAsync($"/api/candidates/{FakeCandidateRepository.Seeded.CandidateId}");

        json.Should().NotContainAny("averageScore", "hasPerformanceRisk", "hasEngagementRisk");
    }

    [Theory]
    [InlineData(Roles.Executive)]
    [InlineData(Roles.ProgramOps)]
    public async Task Get_IncludesScoreFields_ForAuthorizedRoles(string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);

        var dto = await client.GetFromJsonAsync<CandidateDto>(
            $"/api/candidates/{FakeCandidateRepository.Seeded.CandidateId}");

        dto!.AverageScore.Should().NotBeNull();
        dto.HasPerformanceRisk.Should().BeTrue();
    }
}
