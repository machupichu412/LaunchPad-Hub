using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaunchPad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityImagesAndHashtags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CommunityPost_CreatedUtc",
                table: "CommunityPost");

            migrationBuilder.AddColumn<int>(
                name: "CommentCount",
                table: "CommunityPost",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ImageBlobPath",
                table: "CommunityPost",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "CommunityPost",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LikeCount",
                table: "CommunityPost",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Hashtag",
                columns: table => new
                {
                    HashtagId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hashtag", x => x.HashtagId);
                });

            migrationBuilder.CreateTable(
                name: "CommunityPostHashtag",
                columns: table => new
                {
                    CommunityPostId = table.Column<int>(type: "int", nullable: false),
                    HashtagId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPostHashtag", x => new { x.CommunityPostId, x.HashtagId });
                    table.ForeignKey(
                        name: "FK_CommunityPostHashtag_CommunityPost_CommunityPostId",
                        column: x => x.CommunityPostId,
                        principalTable: "CommunityPost",
                        principalColumn: "CommunityPostId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityPostHashtag_Hashtag_HashtagId",
                        column: x => x.HashtagId,
                        principalTable: "Hashtag",
                        principalColumn: "HashtagId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPost_CreatedUtc_CommunityPostId",
                table: "CommunityPost",
                columns: new[] { "CreatedUtc", "CommunityPostId" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPostHashtag_HashtagId_CommunityPostId",
                table: "CommunityPostHashtag",
                columns: new[] { "HashtagId", "CommunityPostId" });

            migrationBuilder.CreateIndex(
                name: "IX_Hashtag_Tag",
                table: "Hashtag",
                column: "Tag",
                unique: true);

            // Backfill the new denormalized counters from whatever reaction/comment rows
            // already exist — LikeCount/CommentCount are DEFAULT 0 for brand-new rows, but
            // existing posts (including LocalDemoSeeder-inserted rows on a real SQL Server
            // target, e.g. the LocalFull profile) need their counts computed once here. Going
            // forward the counters are maintained transactionally by CommunityRepository, not
            // recomputed — this backfill only ever runs once, at migration time.
            migrationBuilder.Sql(@"
                UPDATE cp SET cp.LikeCount = agg.Cnt
                FROM CommunityPost cp
                CROSS APPLY (SELECT COUNT(*) AS Cnt FROM CommunityPostReaction r WHERE r.CommunityPostId = cp.CommunityPostId) agg;

                UPDATE cp SET cp.CommentCount = agg.Cnt
                FROM CommunityPost cp
                CROSS APPLY (SELECT COUNT(*) AS Cnt FROM CommunityComment c WHERE c.CommunityPostId = cp.CommunityPostId) agg;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityPostHashtag");

            migrationBuilder.DropTable(
                name: "Hashtag");

            migrationBuilder.DropIndex(
                name: "IX_CommunityPost_CreatedUtc_CommunityPostId",
                table: "CommunityPost");

            migrationBuilder.DropColumn(
                name: "CommentCount",
                table: "CommunityPost");

            migrationBuilder.DropColumn(
                name: "ImageBlobPath",
                table: "CommunityPost");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "CommunityPost");

            migrationBuilder.DropColumn(
                name: "LikeCount",
                table: "CommunityPost");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPost_CreatedUtc",
                table: "CommunityPost",
                column: "CreatedUtc");
        }
    }
}
