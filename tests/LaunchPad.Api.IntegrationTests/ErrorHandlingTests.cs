using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Api.Middleware;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// An endpoint that throws — the only way to exercise the global handler, since no real
/// controller is supposed to be able to reach it. Lives in the test assembly and is
/// mounted via an ApplicationPart, so it can never ship in the API.
/// </summary>
[ApiController]
[Route("__test/throw")]
public sealed class ThrowingTestController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Throw() => throw new InvalidOperationException("Connection string: Server=secret;Password=hunter2");
}

/// <summary>
/// Standalone rather than reusing CustomWebApplicationFactory: this runs in Production so
/// UseDeveloperExceptionPage is out of the pipeline and GlobalExceptionHandler is what
/// actually answers — the deployed behaviour, which is the only one worth asserting on.
/// The throwing endpoint is anonymous and touches no data, so none of that factory's
/// repository fakes are needed; only the DbContext swap, to keep startup off real SQL.
/// </summary>
public sealed class ProductionErrorFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LaunchPadDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<LaunchPadDbContext>>();
            services.AddDbContext<LaunchPadDbContext>(o => o.UseInMemoryDatabase($"errors-{Guid.NewGuid()}"));

            services.AddControllers().AddApplicationPart(typeof(ThrowingTestController).Assembly);
        });
    }
}

public class ErrorHandlingTests : IClassFixture<ProductionErrorFactory>
{
    private readonly ProductionErrorFactory _factory;
    public ErrorHandlingTests(ProductionErrorFactory factory) => _factory = factory;

    [Fact]
    public async Task UnhandledException_ReturnsProblemDetails_WithoutLeakingExceptionText()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/throw");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("hunter2");
        body.Should().NotContain("Password");
        body.Should().NotContain("InvalidOperationException");
        body.Should().NotContain("at LaunchPad");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(500);
        problem.Title.Should().Be("An unexpected error occurred.");
    }

    [Fact]
    public async Task UnhandledException_EchoesTheCallersCorrelationId()
    {
        var client = _factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, "/__test/throw");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        var response = await client.SendAsync(request);

        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Should().Contain(correlationId);

        // Also in the body — a user reporting a failure can only quote what they can see.
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(correlationId);
    }
}
