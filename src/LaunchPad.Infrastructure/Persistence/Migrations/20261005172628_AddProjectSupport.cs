using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaunchPad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSubjectMatterExpert",
                table: "AppUser",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProjectExpert",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    AppUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectExpert", x => new { x.ProjectId, x.AppUserId });
                    table.ForeignKey(
                        name: "FK_ProjectExpert_AppUser_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUser",
                        principalColumn: "AppUserId");
                    table.ForeignKey(
                        name: "FK_ProjectExpert_Project_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "ProjectQuestion",
                columns: table => new
                {
                    ProjectQuestionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    AuthorAppUserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliverableId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectQuestion", x => x.ProjectQuestionId);
                    table.ForeignKey(
                        name: "FK_ProjectQuestion_AppUser_AuthorAppUserId",
                        column: x => x.AuthorAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "AppUserId");
                    table.ForeignKey(
                        name: "FK_ProjectQuestion_Deliverable_DeliverableId",
                        column: x => x.DeliverableId,
                        principalTable: "Deliverable",
                        principalColumn: "DeliverableId");
                    table.ForeignKey(
                        name: "FK_ProjectQuestion_Project_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "ProjectAnswer",
                columns: table => new
                {
                    ProjectAnswerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectQuestionId = table.Column<int>(type: "int", nullable: false),
                    AuthorAppUserId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FileItemId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAnswer", x => x.ProjectAnswerId);
                    table.ForeignKey(
                        name: "FK_ProjectAnswer_AppUser_AuthorAppUserId",
                        column: x => x.AuthorAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "AppUserId");
                    table.ForeignKey(
                        name: "FK_ProjectAnswer_ProjectQuestion_ProjectQuestionId",
                        column: x => x.ProjectQuestionId,
                        principalTable: "ProjectQuestion",
                        principalColumn: "ProjectQuestionId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnswer_AuthorAppUserId",
                table: "ProjectAnswer",
                column: "AuthorAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnswer_ProjectQuestionId",
                table: "ProjectAnswer",
                column: "ProjectQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpert_AppUserId",
                table: "ProjectExpert",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectQuestion_AuthorAppUserId",
                table: "ProjectQuestion",
                column: "AuthorAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectQuestion_DeliverableId",
                table: "ProjectQuestion",
                column: "DeliverableId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectQuestion_ProjectId_CreatedUtc",
                table: "ProjectQuestion",
                columns: new[] { "ProjectId", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectAnswer");

            migrationBuilder.DropTable(
                name: "ProjectExpert");

            migrationBuilder.DropTable(
                name: "ProjectQuestion");

            migrationBuilder.DropColumn(
                name: "IsSubjectMatterExpert",
                table: "AppUser");
        }
    }
}
