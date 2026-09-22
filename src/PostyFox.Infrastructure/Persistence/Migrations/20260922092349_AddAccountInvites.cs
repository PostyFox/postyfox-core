using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostyFox.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_invites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "text", nullable: false),
                    OwnerEmail = table.Column<string>(type: "text", nullable: false),
                    InviteeEmail = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    TokenPrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedByUserId = table.Column<string>(type: "text", nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_invites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "account_members",
                columns: table => new
                {
                    OwnerUserId = table.Column<string>(type: "text", nullable: false),
                    MemberUserId = table.Column<string>(type: "text", nullable: false),
                    OwnerEmail = table.Column<string>(type: "text", nullable: false),
                    MemberEmail = table.Column<string>(type: "text", nullable: false),
                    InviteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_members", x => new { x.OwnerUserId, x.MemberUserId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_invites_InviteeEmail",
                table: "account_invites",
                column: "InviteeEmail");

            migrationBuilder.CreateIndex(
                name: "IX_account_invites_OwnerUserId",
                table: "account_invites",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_account_invites_TokenPrefix",
                table: "account_invites",
                column: "TokenPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_account_members_MemberUserId",
                table: "account_members",
                column: "MemberUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_invites");

            migrationBuilder.DropTable(
                name: "account_members");
        }
    }
}
