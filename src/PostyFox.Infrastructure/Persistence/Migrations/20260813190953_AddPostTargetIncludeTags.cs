using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostyFox.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPostTargetIncludeTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DraftTargetIncludeTagsJson",
                table: "posts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeTags",
                table: "post_targets",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DraftTargetIncludeTagsJson",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "IncludeTags",
                table: "post_targets");
        }
    }
}
