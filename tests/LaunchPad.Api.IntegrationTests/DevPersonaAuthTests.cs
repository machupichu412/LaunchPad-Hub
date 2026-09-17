using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Api.LocalDemo;
using LaunchPad.Application.Common;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Persona auth accepts a bare header as identity, so the only thing standing between it
/// and a deployed API is Program.cs's gating. These tests pin that gating down using the
/// real authentication setup — not CustomWebApplicationFactory, whose TestAuthHandler
/// would replace exactly the thing under test.
/// </summary>
public class DevPersonaAuthTests
{
    private sealed class Factory(string environment, bool flag) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            if (flag) builder.UseSetting("Auth:UseDevPersonas", "true");
            // appsettings.json carries no AzureAd section (deployments inject it), and
            // without one JwtBearer fails the request with a 500 before it can reject it.
            builder.UseSetting("AzureAd:Instance", "https://login.microsoftonline.com/");
            builder.UseSetting("AzureAd:TenantId", "00000000-0000-0000-0000-000000000000");
            builder.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-000000000000");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<LaunchPadDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<LaunchPadDbContext>>();
                var name = $"personas-{Guid.NewGuid()}";
                services.AddDbContext<LaunchPadDbContext>(o => o.UseInMemoryDatabase(name));
            });
        }
    }

    private sealed record Me(string? ObjectId, string? DisplayName, string[] Roles);

    private static HttpRequestMessage MeAs(string persona)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add(DevPersonaAuthHandler.HeaderName, persona);
        return request;
    }

    [Fact]
    public async Task DevelopmentWithFlag_AuthenticatesAsThePersona()
    {
        using var factory = new Factory("Development", flag: true);
        var response = await factory.CreateClient().SendAsync(MeAs("ops-sponsor"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await response.Content.ReadFromJsonAsync<Me>();
        me!.ObjectId.Should().Be(DevPersonas.OpsSponsor.EntraObjectId.ToString());
        me.Roles.Should().BeEquivalentTo([Roles.ProgramOps, Roles.Sponsor]);
    }

    [Fact]
    public async Task DevelopmentWithFlag_UnknownPersonaIsUnauthenticated()
    {
        using var factory = new Factory("Development", flag: true);
        var response = await factory.CreateClient().SendAsync(MeAs("admin"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DevelopmentWithoutFlag_IgnoresTheHeader()
    {
        using var factory = new Factory("Development", flag: false);
        var response = await factory.CreateClient().SendAsync(MeAs("ops"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProductionWithoutFlag_IgnoresTheHeader()
    {
        using var factory = new Factory("Production", flag: false);
        var response = await factory.CreateClient().SendAsync(MeAs("ops"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void ProductionWithFlag_RefusesToStart()
    {
        using var factory = new Factory("Production", flag: true);
        var act = () => factory.CreateClient();
        act.Should().Throw<InvalidOperationException>().WithMessage("*UseDevPersonas*");
    }
}
