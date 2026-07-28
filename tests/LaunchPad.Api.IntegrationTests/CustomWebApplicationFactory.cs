using LaunchPad.Application.Candidates;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LaunchPad.Api.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // AddDbContext registers both DbContextOptions<T> and per-provider
            // IDbContextOptionsConfiguration<T> entries additively — removing only the
            // former still leaves the SqlServer provider registered alongside InMemory.
            services.RemoveAll<DbContextOptions<LaunchPadDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<LaunchPadDbContext>>();
            services.AddDbContext<LaunchPadDbContext>(o => o.UseInMemoryDatabase($"test-{Guid.NewGuid()}"));

            services.RemoveAll<ICandidateRepository>();
            services.AddSingleton<ICandidateRepository, FakeCandidateRepository>();

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
