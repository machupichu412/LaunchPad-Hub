using System.Net;
using FluentAssertions;
using LaunchPad.Application.Common;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Proves the fail-closed FallbackPolicy from launchpad-build-guide.md §5.1: a
/// zero-role (or fully unauthenticated) request must never reach a controller action.
/// </summary>
public class AuthorizationBoundaryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public AuthorizationBoundaryTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task UnauthenticatedRequest_IsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithNoRoles_CannotReachRoleGatedEndpoint()
    {
        var client = _factory.CreateClient();
        // A role with no meaning to any policy — authenticated, but satisfies nothing.
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "LaunchPad.Nobody");

        var response = await client.GetAsync($"/api/candidates/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithAnyValidRole_CanReachMe()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
