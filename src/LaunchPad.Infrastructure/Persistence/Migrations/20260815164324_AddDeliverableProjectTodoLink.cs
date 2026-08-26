using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaunchPad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliverableProjectTodoLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectTodoId",
                table: "Deliverable",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliverable_ProjectTodoId",
                table: "Deliverable",
                column: "ProjectTodoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Deliverable_ProjectTodo_ProjectTodoId",
                table: "Deliverable",
                column: "ProjectTodoId",
                principalTable: "ProjectTodo",
                principalColumn: "ProjectTodoId",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deliverable_ProjectTodo_ProjectTodoId",
                table: "Deliverable");

            migrationBuilder.DropIndex(
                name: "IX_Deliverable_ProjectTodoId",
                table: "Deliverable");

            migrationBuilder.DropColumn(
                name: "ProjectTodoId",
                table: "Deliverable");
        }
    }
}
