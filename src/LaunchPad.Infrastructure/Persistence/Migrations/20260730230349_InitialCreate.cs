using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaunchPad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUser",
                columns: table => new
                {
                    AppUserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntraObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Upn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUser", x => x.AppUserId);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvent",
                columns: table => new
                {
                    AuditEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorAppUserId = table.Column<int>(type: "int", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvent", x => x.AuditEventId);
                });

            migrationBuilder.CreateTable(
                name: "Program",
                columns: table => new
                {
                    ProgramId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Program", x => x.ProgramId);
                });

            migrationBuilder.CreateTable(
                name: "Skill",
                columns: table => new
                {
                    SkillId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skill", x => x.SkillId);
                });

            migrationBuilder.CreateTable(
                name: "Sponsor",
                columns: table => new
                {
                    SponsorId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<int>(type: "int", nullable: false),
                    Organization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RemovalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sponsor", x => x.SponsorId);
                    table.ForeignKey(
                        name: "FK_Sponsor_AppUser_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUser",
                        principalColumn: "AppUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cohort",
                columns: table => new
                {
                    CohortId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProgramId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cohort", x => x.CohortId);
                    table.ForeignKey(
                        name: "FK_Cohort_Program_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Program",
                        principalColumn: "ProgramId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Candidate",
                columns: table => new
                {
                    CandidateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<int>(type: "int", nullable: false),
                    CohortId = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Availability = table.Column<int>(type: "int", nullable: false),
                    GraduationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PortfolioUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResumeBlobPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidate", x => x.CandidateId);
                    table.ForeignKey(
                        name: "FK_Candidate_AppUser_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUser",
                        principalColumn: "AppUserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Candidate_Cohort_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohort",
                        principalColumn: "CohortId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Project",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CohortId = table.Column<int>(type: "int", nullable: false),
                    SponsorId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvailabilityNeeded = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Project", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_Project_Cohort_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohort",
                        principalColumn: "CohortId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Project_Sponsor_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "Sponsor",
                        principalColumn: "SponsorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateSkill",
                columns: table => new
                {
                    CandidateId = table.Column<int>(type: "int", nullable: false),
                    SkillId = table.Column<int>(type: "int", nullable: false),
                    Proficiency = table.Column<byte>(type: "tinyint", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateSkill", x => new { x.CandidateId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_CandidateSkill_Candidate_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidate",
                        principalColumn: "CandidateId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CandidateSkill_Skill_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skill",
                        principalColumn: "SkillId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Assignment",
                columns: table => new
                {
                    AssignmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    CandidateId = table.Column<int>(type: "int", nullable: false),
                    MatchScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MatchRationale = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SponsorApprovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SponsorApprovedBy = table.Column<int>(type: "int", nullable: true),
                    OpsApprovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpsApprovedBy = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignment", x => x.AssignmentId);
                    table.ForeignKey(
                        name: "FK_Assignment_Candidate_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidate",
                        principalColumn: "CandidateId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Assignment_Project_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSkill",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    SkillId = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSkill", x => new { x.ProjectId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_ProjectSkill_Project_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectSkill_Skill_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skill",
                        principalColumn: "SkillId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTodo",
                columns: table => new
                {
                    ProjectTodoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignmentId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTodo", x => x.ProjectTodoId);
                    table.ForeignKey(
                        name: "FK_ProjectTodo_Assignment_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignment",
                        principalColumn: "AssignmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Review",
                columns: table => new
                {
                    ReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignmentId = table.Column<int>(type: "int", nullable: false),
                    ReviewType = table.Column<int>(type: "int", nullable: false),
                    Checkpoint = table.Column<int>(type: "int", nullable: false),
                    SubmittedBy = table.Column<int>(type: "int", nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Commitment = table.Column<byte>(type: "tinyint", nullable: true),
                    Availability = table.Column<byte>(type: "tinyint", nullable: true),
                    Guidance = table.Column<byte>(type: "tinyint", nullable: true),
                    OutputQuality = table.Column<byte>(type: "tinyint", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverallScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true, computedColumnSql: "CAST((ISNULL([Commitment],0)+ISNULL([Availability],0)+ISNULL([Guidance],0)+ISNULL([OutputQuality],0)) AS DECIMAL(5,2)) / NULLIF((CASE WHEN [Commitment] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [Availability] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [Guidance] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [OutputQuality] IS NULL THEN 0 ELSE 1 END), 0)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Review", x => x.ReviewId);
                    table.ForeignKey(
                        name: "FK_Review_Assignment_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignment",
                        principalColumn: "AssignmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_EntraObjectId",
                table: "AppUser",
                column: "EntraObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assignment_ProjectId",
                table: "Assignment",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "UX_Assignment_Active",
                table: "Assignment",
                column: "CandidateId",
                unique: true,
                filter: "[Status] IN (2,3)");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_EntityName_EntityId",
                table: "AuditEvent",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Candidate_AppUserId_CohortId",
                table: "Candidate",
                columns: new[] { "AppUserId", "CohortId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Candidate_CohortId",
                table: "Candidate",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSkill_SkillId",
                table: "CandidateSkill",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Cohort_ProgramId",
                table: "Cohort",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_CohortId",
                table: "Project",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_SponsorId",
                table: "Project",
                column: "SponsorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSkill_SkillId",
                table: "ProjectSkill",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTodo_AssignmentId",
                table: "ProjectTodo",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "UX_Review_Once",
                table: "Review",
                columns: new[] { "AssignmentId", "ReviewType", "Checkpoint", "SubmittedBy" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skill_Name",
                table: "Skill",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sponsor_AppUserId",
                table: "Sponsor",
                column: "AppUserId");

            // EF does not generate CREATE VIEW — vCandidateRisk is the single source of
            // risk signals shared by the API and Power BI; never duplicate this logic
            // in application code. See launchpad-build-guide.md §4.4.
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vCandidateRisk AS
                WITH scores AS (
                    SELECT a.CandidateId,
                           AVG(CASE WHEN r.Checkpoint = 0 THEN r.OverallScore END) AS MidScore,
                           AVG(CASE WHEN r.Checkpoint = 1 THEN r.OverallScore END) AS FinalScore,
                           AVG(r.OverallScore) AS AvgScore
                    FROM dbo.Assignment a
                    JOIN dbo.Review r ON r.AssignmentId = a.AssignmentId AND r.ReviewType = 0
                    GROUP BY a.CandidateId
                ),
                activity AS (
                    SELECT a.CandidateId,
                           MAX(t.CompletedUtc) AS LastCompletionUtc,
                           SUM(CASE WHEN t.Status <> 2 AND t.DueDate < CAST(SYSUTCDATETIME() AS DATE)
                                    THEN 1 ELSE 0 END) AS StaleTodoCount
                    FROM dbo.Assignment a
                    LEFT JOIN dbo.ProjectTodo t ON t.AssignmentId = a.AssignmentId
                    GROUP BY a.CandidateId
                )
                SELECT c.CandidateId,
                       s.AvgScore, s.MidScore, s.FinalScore,
                       CAST(CASE WHEN s.AvgScore < 3.0
                                  OR (s.FinalScore IS NOT NULL AND s.MidScore IS NOT NULL
                                      AND s.FinalScore < s.MidScore - 0.5)
                            THEN 1 ELSE 0 END AS BIT) AS HasPerformanceRisk,
                       CAST(CASE WHEN ISNULL(act.StaleTodoCount,0) >= 3
                                  OR act.LastCompletionUtc < DATEADD(DAY,-14,SYSUTCDATETIME())
                            THEN 1 ELSE 0 END AS BIT) AS HasEngagementRisk,
                       ISNULL(act.StaleTodoCount,0) AS StaleTodoCount
                FROM dbo.Candidate c
                LEFT JOIN scores   s   ON s.CandidateId = c.CandidateId
                LEFT JOIN activity act ON act.CandidateId = c.CandidateId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW dbo.vCandidateRisk;");

            migrationBuilder.DropTable(
                name: "AuditEvent");

            migrationBuilder.DropTable(
                name: "CandidateSkill");

            migrationBuilder.DropTable(
                name: "ProjectSkill");

            migrationBuilder.DropTable(
                name: "ProjectTodo");

            migrationBuilder.DropTable(
                name: "Review");

            migrationBuilder.DropTable(
                name: "Skill");

            migrationBuilder.DropTable(
                name: "Assignment");

            migrationBuilder.DropTable(
                name: "Candidate");

            migrationBuilder.DropTable(
                name: "Project");

            migrationBuilder.DropTable(
                name: "Cohort");

            migrationBuilder.DropTable(
                name: "Sponsor");

            migrationBuilder.DropTable(
                name: "Program");

            migrationBuilder.DropTable(
                name: "AppUser");
        }
    }
}
