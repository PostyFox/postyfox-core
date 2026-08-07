using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostyFox.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectorCookiePairings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "connector_cookie_pairings",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConnectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connector_cookie_pairings", x => x.TokenHash);
                    table.ForeignKey(
                        name: "FK_connector_cookie_pairings_user_connectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalTable: "user_connectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_connector_cookie_pairings_ConnectorId",
                table: "connector_cookie_pairings",
                column: "ConnectorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_connector_cookie_pairings_ExpiresAt",
                table: "connector_cookie_pairings",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "connector_cookie_pairings");
        }
    }
}
