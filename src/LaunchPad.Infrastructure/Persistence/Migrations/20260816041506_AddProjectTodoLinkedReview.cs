using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaunchPad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTodoLinkedReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectTodo_AssignmentId",
                table: "ProjectTodo");

            migrationBuilder.AddColumn<int>(
                name: "LinkedReviewCheckpoint",
                table: "ProjectTodo",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkedReviewType",
                table: "ProjectTodo",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProjectTodo_LinkedReview_Once",
                table: "ProjectTodo",
                columns: new[] { "AssignmentId", "LinkedReviewType", "LinkedReviewCheckpoint" },
                unique: true,
                filter: "[LinkedReviewType] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ProjectTodo_LinkedReview_Once",
                table: "ProjectTodo");

            migrationBuilder.DropColumn(
                name: "LinkedReviewCheckpoint",
                table: "ProjectTodo");

            migrationBuilder.DropColumn(
                name: "LinkedReviewType",
                table: "ProjectTodo");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTodo_AssignmentId",
                table: "ProjectTodo",
                column: "AssignmentId");
        }
    }
}
