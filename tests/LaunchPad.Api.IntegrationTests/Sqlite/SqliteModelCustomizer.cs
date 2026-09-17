using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LaunchPad.Api.IntegrationTests.Sqlite;

/// <summary>
/// Adapts the production model to SQLite without touching it. Registered with
/// ReplaceService&lt;IModelCustomizer&gt; rather than by subclassing LaunchPadDbContext, because
/// the API resolves LaunchPadDbContext itself from DI and a derived context would need its own
/// DbContextOptions type.
///
/// Only two things in the model are T-SQL specific. Everything else — the filtered unique index
/// on Assignment (SQLite has partial indexes and accepts [bracket] quoting), the unique indexes,
/// the restricted cascade paths — is carried over as written, which is the point of running here.
/// </summary>
public sealed class SqliteModelCustomizer : RelationalModelCustomizer
{
    public SqliteModelCustomizer(ModelCustomizerDependencies dependencies) : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        // T-SQL ISNULL/DECIMAL in the persisted computed column — see SqliteSchema.
        modelBuilder.Entity<Review>()
            .Property(r => r.OverallScore)
            .HasComputedColumnSql(SqliteSchema.OverallScoreSql, stored: true);

        // rowversion has no SQLite equivalent. Left as an ordinary nullable column: EF still
        // emits the concurrency predicate, it just always compares NULL to NULL, so optimistic
        // concurrency is NOT exercised here. That stays a SQL-Server-only behaviour.
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(e => e.GetProperties())
                     .Where(p => p.Name == nameof(Project.RowVersion)))
        {
            property.SetColumnType("BLOB");
            property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            property.IsConcurrencyToken = false;
        }
    }
}
