using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaunchPad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDeliveryStageAndKpiView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeliveryStage",
                table: "Project",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // EF does not generate CREATE VIEW — vProjectDeliveryKpi is the single source of
            // the delivery-stage Executive KPIs (AI solutions delivered, business value
            // generated, prototype maturity, pilot & adoption readiness), shared by the API
            // and Power BI, same rule vCandidateRisk follows. One row per cohort; a project
            // in Cancelled status (3) is excluded from every count, including ProjectCount,
            // so a cancelled project can't drag down (or, once cancelled+reopened, inflate)
            // a rate. See ProjectDeliveryKpi/ProjectDeliveryStage for what each count means.
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vProjectDeliveryKpi AS
                SELECT
                    p.CohortId,
                    COUNT(*) AS ProjectCount,
                    SUM(CASE WHEN p.DeliveryStage >= 1 THEN 1 ELSE 0 END) AS MvpCount,
                    SUM(CASE WHEN p.DeliveryStage >= 3 THEN 1 ELSE 0 END) AS PilotReadyCount,
                    SUM(CASE WHEN p.DeliveryStage >= 4 THEN 1 ELSE 0 END) AS BusinessValueDocumentedCount
                FROM dbo.Project p
                WHERE p.Status <> 3
                GROUP BY p.CohortId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW dbo.vProjectDeliveryKpi;");

            migrationBuilder.DropColumn(
                name: "DeliveryStage",
                table: "Project");
        }
    }
}
