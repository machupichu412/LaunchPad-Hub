using FluentAssertions;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests.Sqlite;

/// <summary>
/// Proves the harness itself is what it claims to be, so a failure in RelationalBehaviorTests
/// reads as a real finding rather than a setup problem.
/// </summary>
[Collection(SqliteCollection.Name)]
public class SqliteHarnessSmokeTests
{
    private readonly SqliteWebApplicationFactory _factory;

    public SqliteHarnessSmokeTests(SqliteFixture fixture) => _factory = fixture.Factory;

    [Fact]
    public async Task BothViewsExistAndAreQueryable()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        // Querying at all is the assertion: a keyless entity mapped to a missing view throws
        // "no such table" here, which is exactly what the InMemory provider could never catch.
        var act = async () =>
        {
            await db.CandidateRisks.ToListAsync();
            await db.ProjectDeliveryKpis.ToListAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TheFilteredUniqueIndexIsCreatedWithItsProductionFilter()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type='index' AND name='UX_Assignment_Active'";
        var sql = (string?)await command.ExecuteScalarAsync();

        // Carried over from AssignmentConfiguration verbatim — SQLite accepts both the
        // partial index and T-SQL's [bracket] quoting, so there is no second filter to drift.
        sql.Should().NotBeNull();
        sql!.Should().Contain("UNIQUE").And.Contain("IN (2,3)");
    }

    [Fact]
    public async Task TheComputedOverallScoreColumnIsTheDatabasesToWrite()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='Review'";
        var sql = (string?)await command.ExecuteScalarAsync();

        // SQLite's shorthand for GENERATED ALWAYS AS (...) STORED. The point is that the
        // column is computed by the database, so application code writing to it is impossible
        // here — the same guarantee the T-SQL persisted computed column gives in production.
        sql.Should().NotBeNull();
        sql!.Should().Contain("\"OverallScore\" AS (").And.Contain("STORED");
    }
}
