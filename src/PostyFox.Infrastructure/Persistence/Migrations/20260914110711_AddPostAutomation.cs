using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostyFox.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPostAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DraftTargetAutomationsJson",
                table: "posts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "post_target_automations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DelayHours = table.Column<double>(type: "double precision", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EnqueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_target_automations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_post_target_automations_post_targets_PostTargetId",
                        column: x => x.PostTargetId,
                        principalTable: "post_targets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_post_target_automations_DueAt",
                table: "post_target_automations",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_post_target_automations_PostTargetId",
                table: "post_target_automations",
                column: "PostTargetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post_target_automations");

            migrationBuilder.DropColumn(
                name: "DraftTargetAutomationsJson",
                table: "posts");
        }
    }
}
