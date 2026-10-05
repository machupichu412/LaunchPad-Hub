using Microsoft.Extensions.DependencyInjection;
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

    [Theory]
    [InlineData("/api/me")]
    [InlineData("/api/skills")]
    [InlineData("/api/app-users/1/avatar")]
    [InlineData("/api/notifications")]
    public async Task UnknownRole_IsRejectedWithoutProvisioning(string path)
    {
        var oid = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "LaunchPad.Nobody");
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());
        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPad.Infrastructure.Persistence.LaunchPadDbContext>();
        db.AppUsers.Any(u => u.EntraObjectId == oid).Should().BeFalse();
    }

    [Theory]
    [InlineData("bad-oid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidIdentity_IsRejected(string oid)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid);
        (await client.GetAsync("/api/skills")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("other_scope")]
    public async Task MissingDelegatedScope_IsRejected(string scopes)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopesHeader, scopes);
        (await client.GetAsync("/api/skills")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
