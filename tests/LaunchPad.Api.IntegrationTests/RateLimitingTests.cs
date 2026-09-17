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
}
