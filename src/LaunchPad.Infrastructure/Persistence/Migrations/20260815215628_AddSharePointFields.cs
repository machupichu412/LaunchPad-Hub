using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaunchPad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSharePointFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SharePointFolderId",
                table: "Project",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharePointFolderWebUrl",
                table: "Project",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharePointItemId",
                table: "Deliverable",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharePointFolderId",
                table: "Cohort",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharePointFolderWebUrl",
                table: "Cohort",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharePointFolderId",
                table: "Candidate",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharePointFolderWebUrl",
                table: "Candidate",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SharePointFolderId",
                table: "Project");

            migrationBuilder.DropColumn(
                name: "SharePointFolderWebUrl",
                table: "Project");

            migrationBuilder.DropColumn(
                name: "SharePointItemId",
                table: "Deliverable");

            migrationBuilder.DropColumn(
                name: "SharePointFolderId",
                table: "Cohort");

            migrationBuilder.DropColumn(
                name: "SharePointFolderWebUrl",
                table: "Cohort");

            migrationBuilder.DropColumn(
                name: "SharePointFolderId",
                table: "Candidate");

            migrationBuilder.DropColumn(
                name: "SharePointFolderWebUrl",
                table: "Candidate");
        }
    }
}
