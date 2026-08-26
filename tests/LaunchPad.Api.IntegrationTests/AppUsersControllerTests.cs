using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// GET /api/app-users/{id}/avatar — deliberately [Authorize]-only, no role restriction: any
/// role can post/comment in Community, so any role may need to resolve any other author's
/// avatar. A photo carries none of the hidden-score sensitivity CLAUDE.md's redaction rule
/// covers.
/// </summary>
public class AppUsersControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public AppUsersControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(Guid Oid, int AppUserId)> SeedAppUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var oid = Guid.NewGuid();
        var appUser = new AppUser { EntraObjectId = oid, Upn = $"{Guid.NewGuid()}@example.com", DisplayName = "Avatar Test User" };
        db.Add(appUser);
        await db.SaveChangesAsync();

        return (oid, appUser.AppUserId);
    }

    [Fact]
    public async Task GetAvatar_Unauthenticated_IsUnauthorized()
    {
        var (_, appUserId) = await SeedAppUserAsync();

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/app-users/{appUserId}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAvatar_WhenUserHasNoPhoto_ReturnsNotFound()
    {
        var (_, appUserId) = await SeedAppUserAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var response = await client.GetAsync($"/api/app-users/{appUserId}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAvatar_ForUnknownAppUserId_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);

        var response = await client.GetAsync("/api/app-users/999999/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAvatar_AfterTheOwnerUploadsOne_ReturnsItToADifferentRole()
    {
        var (ownerOid, appUserId) = await SeedAppUserAsync();

        var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        ownerClient.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, ownerOid.ToString());

        var imageBytes = new byte[] { 9, 9, 9 };
        var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var uploadResponse = await ownerClient.PostAsync("/api/me/avatar", content);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A different role, different identity — proves this is a genuinely cross-role
        // lookup, not scoped to the caller's own AppUser row the way /api/me/avatar is.
        var candidateClient = _factory.CreateClient();
        candidateClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        candidateClient.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await candidateClient.GetAsync($"/api/app-users/{appUserId}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(imageBytes);
    }
}
