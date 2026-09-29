using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PostyFox.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTermsOfService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "terms_acceptances",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TermsVersion = table.Column<int>(type: "integer", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terms_acceptances", x => new { x.UserId, x.TermsVersion });
                });

            migrationBuilder.CreateTable(
                name: "terms_of_service",
                columns: table => new
                {
                    Version = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Content = table.Column<string>(type: "text", nullable: false),
                    PublishedByUserId = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terms_of_service", x => x.Version);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terms_acceptances");

            migrationBuilder.DropTable(
                name: "terms_of_service");
        }
    }
}
