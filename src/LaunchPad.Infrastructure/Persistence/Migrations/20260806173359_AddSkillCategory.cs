using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaunchPad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SkillCategory",
                columns: table => new
                {
                    SkillCategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillCategory", x => x.SkillCategoryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkillCategory_Name",
                table: "SkillCategory",
                column: "Name",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "SkillCategoryId",
                table: "Skill",
                type: "int",
                nullable: true);

            // Backfill: one SkillCategory row per distinct existing Skill.Category value,
            // plus a catch-all "Uncategorized" row for anything null/blank — mirrors the
            // fallback ISkillRepository.GetOrCreateByNamesAsync now uses for skills
            // created ad hoc from free-text input.
            migrationBuilder.Sql(@"
                INSERT INTO [SkillCategory] ([Name])
                SELECT DISTINCT [Category] FROM [Skill]
                WHERE [Category] IS NOT NULL AND LTRIM(RTRIM([Category])) <> '';

                IF NOT EXISTS (SELECT 1 FROM [SkillCategory] WHERE [Name] = 'Uncategorized')
                    INSERT INTO [SkillCategory] ([Name]) VALUES ('Uncategorized');

                UPDATE s
                SET s.[SkillCategoryId] = sc.[SkillCategoryId]
                FROM [Skill] s
                INNER JOIN [SkillCategory] sc ON sc.[Name] = s.[Category]
                WHERE s.[Category] IS NOT NULL AND LTRIM(RTRIM(s.[Category])) <> '';

                UPDATE s
                SET s.[SkillCategoryId] = (SELECT [SkillCategoryId] FROM [SkillCategory] WHERE [Name] = 'Uncategorized')
                FROM [Skill] s
                WHERE s.[SkillCategoryId] IS NULL;
            ");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Skill");

            migrationBuilder.AlterColumn<int>(
                name: "SkillCategoryId",
                table: "Skill",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skill_SkillCategoryId",
                table: "Skill",
                column: "SkillCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Skill_SkillCategory_SkillCategoryId",
                table: "Skill",
                column: "SkillCategoryId",
                principalTable: "SkillCategory",
                principalColumn: "SkillCategoryId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Skill",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE s
                SET s.[Category] = sc.[Name]
                FROM [Skill] s
                INNER JOIN [SkillCategory] sc ON sc.[SkillCategoryId] = s.[SkillCategoryId]
                WHERE sc.[Name] <> 'Uncategorized';
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_Skill_SkillCategory_SkillCategoryId",
                table: "Skill");

            migrationBuilder.DropIndex(
                name: "IX_Skill_SkillCategoryId",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "SkillCategoryId",
                table: "Skill");

            migrationBuilder.DropTable(
                name: "SkillCategory");
        }
    }
}
