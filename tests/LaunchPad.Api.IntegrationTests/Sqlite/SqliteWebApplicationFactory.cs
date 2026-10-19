using LaunchPad.Application.Candidates;
using LaunchPad.Application.Reporting;
using LaunchPad.Infrastructure.Persistence;
using LaunchPad.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LaunchPad.Api.IntegrationTests.Sqlite;

/// <summary>
/// The same API host as CustomWebApplicationFactory, on a real relational database instead of
/// the InMemory provider. That buys three things InMemory cannot represent at all:
///
///   * the filtered unique index enforcing one live assignment per candidate,
///   * vCandidateRisk and vProjectDeliveryKpi as actual views, so the real CandidateRepository
///     and OpsDashboardRepository run instead of TestCandidateRepositoryWithFakeRisk /
///     FakeOpsDashboardRepository,
///   * transactions, so the Serializable guards in AssignmentRepository take a real path.
///
/// It is a stand-in for SQL Server, not a substitute: rowversion, temporal tables, and SQL
/// Server's own locking and error numbers are not here. See SqlServerOnlyBehaviorTests.
///
/// One query cannot run here at all: OpsDashboardRepository.GetAtRiskCandidatesAsync orders by
/// a decimal (AvgScore), which SQLite refuses to translate. That is a SQLite limitation, not a
/// defect — the same query is fine on SQL Server, so GET /api/ops/risks is out of scope for
/// these tests and the risk view is asserted through GET /api/candidates/{id} instead.
///
/// One SQLite file per factory instance, deleted on dispose. File-backed rather than
/// :memory: so the several connections a concurrency test opens see the same database.
/// </summary>
public class SqliteWebApplicationFactory : CustomWebApplicationFactory
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(), $"launchpad-sqlite-{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_databasePath};Default Timeout=30";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Runs after the base class's callback, so it replaces the InMemory registration
            // and the two fakes the base only needs because InMemory cannot back a view.
            services.RemoveAll<DbContextOptions<LaunchPadDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<LaunchPadDbContext>>();
            services.AddDbContext<LaunchPadDbContext>(o => o
                .UseSqlite(ConnectionString)
                .ReplaceService<IModelCustomizer, SqliteModelCustomizer>());

            services.RemoveAll<ICandidateRepository>();
            services.AddScoped<ICandidateRepository, CandidateRepository>();
            services.RemoveAll<IOpsDashboardRepository>();
            services.AddScoped<IOpsDashboardRepository, OpsDashboardRepository>();
        });
    }

    /// <summary>
    /// Creates the schema. Called once per factory, before the first request — the migrations
    /// are T-SQL and cannot run here, so the tables come from the model and the views from
    /// SqliteSchema.
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await SqliteSchema.CreateViewsAsync(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        foreach (var path in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" })
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A pooled connection can still hold the file on Windows; a leftover temp
                // file is not worth failing a test run over.
            }
        }
    }
}
