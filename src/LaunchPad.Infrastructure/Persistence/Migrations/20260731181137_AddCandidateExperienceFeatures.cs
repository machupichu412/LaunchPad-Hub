using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaunchPad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateExperienceFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GrowthAreas",
                table: "Review",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecommendConversion",
                table: "Review",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Strengths",
                table: "Review",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ProjectTodo",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "Candidate",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Degree",
                table: "Candidate",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Gpa",
                table: "Candidate",
                type: "decimal(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "School",
                table: "Candidate",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommunityPost",
                columns: table => new
                {
                    CommunityPostId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorAppUserId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PostType = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuthorRoleLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPost", x => x.CommunityPostId);
                    table.ForeignKey(
                        name: "FK_CommunityPost_AppUser_AuthorAppUserId",
                        column: x => x.AuthorAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "AppUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Deliverable",
                columns: table => new
                {
                    DeliverableId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignmentId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliverable", x => x.DeliverableId);
                    table.ForeignKey(
                        name: "FK_Deliverable_Assignment_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignment",
                        principalColumn: "AssignmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityComment",
                columns: table => new
                {
                    CommunityCommentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommunityPostId = table.Column<int>(type: "int", nullable: false),
                    AuthorAppUserId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityComment", x => x.CommunityCommentId);
                    table.ForeignKey(
                        name: "FK_CommunityComment_AppUser_AuthorAppUserId",
                        column: x => x.AuthorAppUserId,
                        principalTable: "AppUser",
                        principalColumn: "AppUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityComment_CommunityPost_CommunityPostId",
                        column: x => x.CommunityPostId,
                        principalTable: "CommunityPost",
                        principalColumn: "CommunityPostId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityPostReaction",
                columns: table => new
                {
                    CommunityPostId = table.Column<int>(type: "int", nullable: false),
                    AppUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPostReaction", x => new { x.CommunityPostId, x.AppUserId });
                    table.ForeignKey(
                        name: "FK_CommunityPostReaction_AppUser_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUser",
                        principalColumn: "AppUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityPostReaction_CommunityPost_CommunityPostId",
                        column: x => x.CommunityPostId,
                        principalTable: "CommunityPost",
                        principalColumn: "CommunityPostId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComment_AuthorAppUserId",
                table: "CommunityComment",
                column: "AuthorAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityComment_CommunityPostId",
                table: "CommunityComment",
                column: "CommunityPostId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPost_AuthorAppUserId",
                table: "CommunityPost",
                column: "AuthorAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPost_CreatedUtc",
                table: "CommunityPost",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPostReaction_AppUserId",
                table: "CommunityPostReaction",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverable_AssignmentId",
                table: "Deliverable",
                column: "AssignmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityComment");

            migrationBuilder.DropTable(
                name: "CommunityPostReaction");

            migrationBuilder.DropTable(
                name: "Deliverable");

            migrationBuilder.DropTable(
                name: "CommunityPost");

            migrationBuilder.DropColumn(
                name: "GrowthAreas",
                table: "Review");

            migrationBuilder.DropColumn(
                name: "RecommendConversion",
                table: "Review");

            migrationBuilder.DropColumn(
                name: "Strengths",
                table: "Review");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ProjectTodo");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "Candidate");

            migrationBuilder.DropColumn(
                name: "Degree",
                table: "Candidate");

            migrationBuilder.DropColumn(
                name: "Gpa",
                table: "Candidate");

            migrationBuilder.DropColumn(
                name: "School",
                table: "Candidate");
        }
    }
}
