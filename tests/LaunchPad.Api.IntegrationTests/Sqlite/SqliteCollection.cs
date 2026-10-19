using Xunit;

namespace LaunchPad.Api.IntegrationTests.Sqlite;

/// <summary>
/// One API host and one SQLite file for every test in this namespace. Each test seeds its own
/// program/cohort/candidate with a fresh tag and asserts only on the ids it created, so sharing
/// the database costs nothing — and standing up a WebApplicationFactory per test both dominated
/// the run time and tripped an MVC application-part race when several hosts started at once.
/// </summary>
public sealed class SqliteFixture : IAsyncLifetime
{
    public SqliteWebApplicationFactory Factory { get; } = new();

    public Task InitializeAsync() => Factory.InitializeDatabaseAsync();

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class SqliteCollection : ICollectionFixture<SqliteFixture>
{
    public const string Name = "Sqlite";
}
