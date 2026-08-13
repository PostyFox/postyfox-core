using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostyFox.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectorDestinations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetId",
                table: "post_targets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetName",
                table: "post_targets",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "connector_destinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connector_destinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_connector_destinations_user_connectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalTable: "user_connectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_connector_destinations_ConnectorId",
                table: "connector_destinations",
                column: "ConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_connector_destinations_ConnectorId_ExternalId",
                table: "connector_destinations",
                columns: new[] { "ConnectorId", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "connector_destinations");

            migrationBuilder.DropColumn(
                name: "TargetId",
                table: "post_targets");

            migrationBuilder.DropColumn(
                name: "TargetName",
                table: "post_targets");
        }
    }
}
