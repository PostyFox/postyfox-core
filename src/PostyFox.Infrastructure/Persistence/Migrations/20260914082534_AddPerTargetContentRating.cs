using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostyFox.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerTargetContentRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rating",
                table: "posts");

            migrationBuilder.AddColumn<string>(
                name: "DefaultRating",
                table: "user_connectors",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftTargetRatingJson",
                table: "posts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rating",
                table: "post_targets",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultRating",
                table: "user_connectors");

            migrationBuilder.DropColumn(
                name: "DraftTargetRatingJson",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "post_targets");

            migrationBuilder.AddColumn<string>(
                name: "Rating",
                table: "posts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }
    }
}
