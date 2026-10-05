using Microsoft.Extensions.DependencyInjection;
using System.Net;
using FluentAssertions;
using LaunchPad.Application.Common;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Drives the global limiter with a deliberately tiny permit limit from configuration —
/// the production default of 300/minute would need hundreds of requests to trip, which is
/// a slow test that proves the same thing.
/// </summary>
public sealed class TightRateLimitFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("RateLimiting:PermitLimit", "3");
        builder.UseSetting("RateLimiting:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:UploadsPerHour", "2");
        base.ConfigureWebHost(builder);
    }
}

public class RateLimitingTests : IClassFixture<TightRateLimitFactory>
{
    private readonly TightRateLimitFactory _factory;
    public RateLimitingTests(TightRateLimitFactory factory) => _factory = factory;

    [Fact]
    public async Task ExceedingTheGlobalLimit_Returns429WithRetryAfter()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);
        // The limiter partitions per Entra object id, and TestAuthHandler invents a fresh
        // one per request unless told otherwise — without this every request lands in its
        // own partition and the limit is never reached.
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        HttpResponseMessage? limited = null;
        for (var i = 0; i < 6 && limited is null; i++)
        {
            var response = await client.GetAsync("/api/skills");
            if (response.StatusCode == HttpStatusCode.TooManyRequests) limited = response;
        }

        limited.Should().NotBeNull("six requests against a limit of three should trip the limiter");

        // A 429 with no Retry-After invites an immediate retry, which is what caused it.
        limited!.Headers.RetryAfter.Should().NotBeNull();
    }
    [Fact]
    public async Task RateLimitedRequests_DoNotProvisionUsers()
    {
        // All invalid principals share a bounded anonymous partition, including varied oids.
        using var factory = new TightRateLimitFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "LaunchPad.Nobody");
        for (var i = 0; i < 3; i++) await client.GetAsync("/api/me");
        var oid = Guid.NewGuid();
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());
        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<LaunchPad.Infrastructure.Persistence.LaunchPadDbContext>()
            .AppUsers.Any(u => u.EntraObjectId == oid).Should().BeFalse();
    }

    [Fact]
    public async Task UploadQuota_AppliesBeforeBodyValidation_AndAcrossEndpoints()
    {
        using var factory = new TightRateLimitFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());
        for (var i = 0; i < 2; i++)
            (await client.PostAsync("/api/me/avatar", new StringContent("invalid image")))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var response = await client.PostAsync("/api/assignments/1/deliverables", new MultipartFormDataContent());
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task CommunityPosts_ShareUploadQuota_EvenWithTrailingSlashAndDifferentCasing()
    {
        using var factory = new TightRateLimitFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());
        for (var i = 0; i < 2; i++)
            (await client.PostAsync("/API/COMMUNITY/posts/", new MultipartFormDataContent()))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsync("/api/me/avatar", new StringContent("invalid image")))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

}

public sealed class TightAdmissionFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("RateLimiting:InstancePermitLimit", "2");
        base.ConfigureWebHost(builder);
    }
}

public class AdmissionBoundaryTests
{
    [Fact]
    public async Task RotatingValidIdentities_CannotBypassInstanceLimit()
    {
        using var factory = new TightAdmissionFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        for (var i = 0; i < 2; i++)
        {
            client.DefaultRequestHeaders.Remove(TestAuthHandler.OidHeader);
            client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());
            (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        var rejectedOid = Guid.NewGuid();
        client.DefaultRequestHeaders.Remove(TestAuthHandler.OidHeader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, rejectedOid.ToString());
        var response = await client.GetAsync("/api/me");
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<LaunchPad.Infrastructure.Persistence.LaunchPadDbContext>()
            .AppUsers.Any(u => u.EntraObjectId == rejectedOid).Should().BeFalse();
        (await client.GetAsync("/healthz")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MatchingQuota_IsSharedByOpsUsers_AndCannotBePoisonedAnonymously()
    {
        using var factory = new CustomWebApplicationFactory();
        var anonymous = factory.CreateClient();
        const string path = "/api/matching/run?cohortId=987654";
        (await anonymous.PostAsync(path, null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var ops = factory.CreateClient();
        ops.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);
        // A missing cohort avoids queueing real work, but still consumes an attempt.
        (await ops.PostAsync(path, null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        ops.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());
        (await ops.PostAsync(path, null)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
