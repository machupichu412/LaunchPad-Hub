using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Api.IntegrationTests.Sqlite;

/// <summary>
/// The parts of the schema EF does not create from the model, ported from T-SQL to SQLite.
///
/// These are hand-ported, not shared with the migrations: the SQL Server definitions live in
/// <c>20260730230349_InitialCreate</c> and <c>20260828064019_AddProjectDeliveryStageAndKpiView</c>
/// and stay authoritative. A SQLite run therefore proves the *application* reads a real view
/// correctly and that the risk/KPI rules produce the expected answers — it does not prove the
/// T-SQL view text is valid. Only SqlServerOnlyBehaviorTests does that. Keep the two in step by
/// hand; if the T-SQL changes, change these with it.
/// </summary>
public static class SqliteSchema
{
    /// <summary>
    /// ReviewConfiguration's computed column, with T-SQL ISNULL swapped for SQLite IFNULL.
    /// The shape (average of whichever criteria were scored, NULL when none were) is identical.
    /// </summary>
    public const string OverallScoreSql =
        "CAST((IFNULL(Commitment,0)+IFNULL(Availability,0)+IFNULL(Guidance,0)+IFNULL(OutputQuality,0)) AS REAL) " +
        "/ NULLIF((CASE WHEN Commitment IS NULL THEN 0 ELSE 1 END) + (CASE WHEN Availability IS NULL THEN 0 ELSE 1 END) " +
        "+ (CASE WHEN Guidance IS NULL THEN 0 ELSE 1 END) + (CASE WHEN OutputQuality IS NULL THEN 0 ELSE 1 END), 0)";

    // The score columns are CAST to TEXT because EF's SQLite provider stores and reads decimal
    // as TEXT; handing it a REAL here would work by accident on some values and not others.
    private const string CandidateRiskView = """
        CREATE VIEW vCandidateRisk AS
        WITH scores AS (
            SELECT a.CandidateId,
                   AVG(CASE WHEN r.Checkpoint = 0 THEN r.OverallScore END) AS MidScore,
                   AVG(CASE WHEN r.Checkpoint = 1 THEN r.OverallScore END) AS FinalScore,
                   AVG(r.OverallScore) AS AvgScore
            FROM Assignment a
            JOIN Review r ON r.AssignmentId = a.AssignmentId AND r.ReviewType = 0
            GROUP BY a.CandidateId
        ),
        activity AS (
            SELECT a.CandidateId,
                   MAX(t.CompletedUtc) AS LastCompletionUtc,
                   SUM(CASE WHEN t.Status <> 2 AND t.DueDate < date('now')
                            THEN 1 ELSE 0 END) AS StaleTodoCount
            FROM Assignment a
            LEFT JOIN ProjectTodo t ON t.AssignmentId = a.AssignmentId
            GROUP BY a.CandidateId
        )
        SELECT c.CandidateId,
               CAST(s.AvgScore AS TEXT)   AS AvgScore,
               CAST(s.MidScore AS TEXT)   AS MidScore,
               CAST(s.FinalScore AS TEXT) AS FinalScore,
               CASE WHEN s.AvgScore < 3.0
                         OR (s.FinalScore IS NOT NULL AND s.MidScore IS NOT NULL
                             AND s.FinalScore < s.MidScore - 0.5)
                    THEN 1 ELSE 0 END AS HasPerformanceRisk,
               CASE WHEN IFNULL(act.StaleTodoCount,0) >= 3
                         OR act.LastCompletionUtc < datetime('now','-14 days')
                    THEN 1 ELSE 0 END AS HasEngagementRisk,
               IFNULL(act.StaleTodoCount,0) AS StaleTodoCount
        FROM Candidate c
        LEFT JOIN scores   s   ON s.CandidateId = c.CandidateId
        LEFT JOIN activity act ON act.CandidateId = c.CandidateId;
        """;

    private const string ProjectDeliveryKpiView = """
        CREATE VIEW vProjectDeliveryKpi AS
        SELECT
            p.CohortId,
            COUNT(*) AS ProjectCount,
            SUM(CASE WHEN p.DeliveryStage >= 1 THEN 1 ELSE 0 END) AS MvpCount,
            SUM(CASE WHEN p.DeliveryStage >= 3 THEN 1 ELSE 0 END) AS PilotReadyCount,
            SUM(CASE WHEN p.DeliveryStage >= 4 THEN 1 ELSE 0 END) AS BusinessValueDocumentedCount
        FROM Project p
        WHERE p.Status <> 3
        GROUP BY p.CohortId;
        """;

    public static async Task CreateViewsAsync(LaunchPadDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(CandidateRiskView, ct);
        await db.Database.ExecuteSqlRawAsync(ProjectDeliveryKpiView, ct);
    }
}
