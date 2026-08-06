using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaunchPad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectInterestAndApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Project",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectInterest",
                columns: table => new
                {
                    ProjectInterestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    CandidateId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<byte>(type: "tinyint", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectInterest", x => x.ProjectInterestId);
                    table.ForeignKey(
                        name: "FK_ProjectInterest_Candidate_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidate",
                        principalColumn: "CandidateId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectInterest_Project_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInterest_ProjectId",
                table: "ProjectInterest",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "UX_ProjectInterest_Candidate_Project",
                table: "ProjectInterest",
                columns: new[] { "CandidateId", "ProjectId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectInterest");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Project");
        }
    }
}
