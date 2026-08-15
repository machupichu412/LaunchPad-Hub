using LaunchPad.Application.Candidates;
using LaunchPad.Application.Matching;
using LaunchPad.Application.Notifications;
using LaunchPad.Application.Reporting;
using LaunchPad.Application.SharePoint;
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
    // Must be fixed per factory instance, not evaluated inside the options lambda —
    // AddDbContext rebuilds DbContextOptions<T> for every new scope, so a
    // Guid.NewGuid() call inside the lambda produces a fresh, isolated in-memory
    // database per HTTP request (and per manual CreateScope() call in a test), which
    // silently made seeded data invisible to the request that was supposed to see it.
    private readonly string _databaseName = $"test-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // AddDbContext registers both DbContextOptions<T> and per-provider
            // IDbContextOptionsConfiguration<T> entries additively — removing only the
            // former still leaves the SqlServer provider registered alongside InMemory.
            services.RemoveAll<DbContextOptions<LaunchPadDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<LaunchPadDbContext>>();
            services.AddDbContext<LaunchPadDbContext>(o => o.UseInMemoryDatabase(_databaseName));

            // CandidateRisk is keyless (backed by a SQL view in prod) — it can never
            // be seeded via .Add() under EF Core. See TestCandidateRepositoryWithFakeRisk.
            services.RemoveAll<ICandidateRepository>();
            services.AddScoped<ICandidateRepository, TestCandidateRepositoryWithFakeRisk>();

            // Same keyless-CandidateRisk constraint as above — OpsDashboardRepository
            // is built almost entirely around joining it. See FakeOpsDashboardRepository.
            services.RemoveAll<IOpsDashboardRepository>();
            services.AddScoped<IOpsDashboardRepository, FakeOpsDashboardRepository>();

            // Records notifications instead of publishing to a real Service Bus
            // namespace — see FakeNotificationPublisher.
            services.RemoveAll<INotificationPublisher>();
            services.AddSingleton<INotificationPublisher, FakeNotificationPublisher>();

            // Runs cohort matching synchronously in-request instead of publishing to a
            // real Service Bus namespace — see FakeMatchingJobPublisher.
            services.RemoveAll<IMatchingJobPublisher>();
            services.AddScoped<IMatchingJobPublisher, FakeMatchingJobPublisher>();

            // Same "run inline, no real external system" shape as matching above — see
            // FakeFolderProvisioner/FakeDocumentStorage/FakeFolderProvisioningJobPublisher.
            services.RemoveAll<IFolderProvisioner>();
            services.AddSingleton<IFolderProvisioner, FakeFolderProvisioner>();
            services.RemoveAll<IDocumentStorage>();
            services.AddSingleton<IDocumentStorage, FakeDocumentStorage>();
            services.RemoveAll<IFolderProvisioningJobPublisher>();
            services.AddScoped<IFolderProvisioningJobPublisher, FakeFolderProvisioningJobPublisher>();

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
