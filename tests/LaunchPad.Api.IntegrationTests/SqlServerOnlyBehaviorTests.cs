using FluentAssertions;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Exercises SQL-Server-only behavior that the EF Core InMemory provider used by
/// every other test in this project (via CustomWebApplicationFactory) can't
/// represent at all: the filtered unique index enforcing one active Assignment per
/// candidate, and the vCandidateRisk view. Neither is reachable through the normal
/// in-memory test host.
///
/// Skipped entirely unless SQLSERVER_TEST_CONNECTION is set — local `dotnet test`
/// runs (no SQL Server available) and the fast CI jobs stay unaffected; only the
/// dedicated integration-real-sql CI job (and scripts/run-local-full.sh, manually)
/// set this and actually run these. See the "Homelab Azure-service emulation" plan.
/// </summary>
public class SqlServerOnlyBehaviorTests : IAsyncLifetime
{
    private static readonly string? ConnectionString = Environment.GetEnvironmentVariable("SQLSERVER_TEST_CONNECTION");

    private LaunchPadDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LaunchPadDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new LaunchPadDbContext(options);
    }

    public async Task InitializeAsync()
    {
        if (ConnectionString is null) return;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FilteredUniqueIndex_RejectsASecondActiveAssignmentForTheSameCandidate()
    {
        // xunit v2 has no built-in dynamic skip — an early return (reported as a
        // pass) keeps this file dependency-free rather than pulling in
        // Xunit.SkippableFact for one narrow case. See the class doc comment for
        // when this actually runs.
        if (ConnectionString is null) return;

        await using var db = CreateContext();
        var (candidate, projectA, projectB) = await SeedCandidateAndTwoProjectsAsync(db);

        db.Assignments.Add(new Assignment
        {
            CandidateId = candidate.CandidateId,
            ProjectId = projectA.ProjectId,
            Status = AssignmentStatus.OpsApproved, // in the filter: IN (2,3)
        });
        await db.SaveChangesAsync();

        db.Assignments.Add(new Assignment
        {
            CandidateId = candidate.CandidateId,
            ProjectId = projectB.ProjectId,
            Status = AssignmentStatus.Active, // also in the filter — should collide
        });

        var act = () => db.SaveChangesAsync();

        // The EF InMemory provider has no concept of a filtered index and would
        // happily accept both rows — only a real SQL Server enforces this.
        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Number.Should().Be(2601); // unique index violation
    }

    [Fact]
    public async Task VCandidateRiskView_IsQueryable()
    {
        if (ConnectionString is null) return;

        await using var db = CreateContext();
        var (candidate, _, _) = await SeedCandidateAndTwoProjectsAsync(db);

        // The InMemory provider can't back a keyless entity mapped to a database
        // view at all — CustomWebApplicationFactory works around this with
        // TestCandidateRepositoryWithFakeRisk. This confirms the real view (created
        // via raw SQL in the InitialCreate migration) actually exists and resolves.
        var risk = await db.CandidateRisks.FirstOrDefaultAsync(r => r.CandidateId == candidate.CandidateId);
        risk.Should().NotBeNull();
    }

    private static async Task<(Candidate Candidate, Project ProjectA, Project ProjectB)> SeedCandidateAndTwoProjectsAsync(LaunchPadDbContext db)
    {
        var program = new LaunchPad.Domain.Entities.Program { Name = $"SqlOnlyTest-{Guid.NewGuid()}", IsActive = true };
        var cohort = new Cohort
        {
            Program = program,
            Name = $"Cohort-{Guid.NewGuid()}",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 6, 1),
            Status = CohortStatus.Active,
        };

        var candidateAppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = $"{Guid.NewGuid()}@example.com", DisplayName = "SQL Test Candidate" };
        var sponsorAppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = $"{Guid.NewGuid()}@example.com", DisplayName = "SQL Test Sponsor" };

        var candidate = new Candidate
        {
            AppUser = candidateAppUser,
            Cohort = cohort,
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
        };

        var sponsor = new Sponsor { AppUser = sponsorAppUser, Organization = "Test Org" };

        var projectA = new Project
        {
            Cohort = cohort,
            Sponsor = sponsor,
            Name = "Project A",
            AvailabilityNeeded = Availability.PartTime,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = ProjectStatus.Open,
        };
        var projectB = new Project
        {
            Cohort = cohort,
            Sponsor = sponsor,
            Name = "Project B",
            AvailabilityNeeded = Availability.PartTime,
            ApprovalStatus = ProjectApprovalStatus.Approved,
            Status = ProjectStatus.Open,
        };

        db.AddRange(program, cohort, candidateAppUser, sponsorAppUser, candidate, sponsor, projectA, projectB);
        await db.SaveChangesAsync();

        return (candidate, projectA, projectB);
    }
}
