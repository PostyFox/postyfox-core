using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostyFox.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    KeyHash = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "external_interests",
                columns: table => new
                {
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    ExternalAccount = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_interests", x => new { x.SourceType, x.ExternalAccount, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "external_triggers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    ExternalAccount = table.Column<string>(type: "text", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetConnectorId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotifyFrequencyHrs = table.Column<int>(type: "integer", nullable: false),
                    LastFiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_triggers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    HtmlDescription = table.Column<string>(type: "text", nullable: false),
                    TagsJson = table.Column<string>(type: "text", nullable: false),
                    MediaManifestJson = table.Column<string>(type: "text", nullable: false),
                    VariablesJson = table.Column<string>(type: "text", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RootStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "secrets",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CipherText = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secrets", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "service_definitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigSchema = table.Column<string>(type: "text", nullable: false),
                    SecureConfigSchema = table.Column<string>(type: "text", nullable: true),
                    Platform = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    MarkdownBody = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_dedupe",
                columns: table => new
                {
                    Source = table.Column<string>(type: "text", nullable: false),
                    MessageId = table.Column<string>(type: "text", nullable: false),
                    SeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_dedupe", x => new { x.Source, x.MessageId });
                });

            migrationBuilder.CreateTable(
                name: "post_targets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    RenderedContentJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: true),
                    ExternalUrl = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_targets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_post_targets_posts_PostId",
                        column: x => x.PostId,
                        principalTable: "posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_connectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ServiceDefinitionId = table.Column<string>(type: "character varying(64)", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    ConfigJson = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_connectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_connectors_service_definitions_ServiceDefinitionId",
                        column: x => x.ServiceDefinitionId,
                        principalTable: "service_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_Prefix",
                table: "api_keys",
                column: "Prefix");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_UserId",
                table: "api_keys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_external_triggers_SourceType_ExternalAccount",
                table: "external_triggers",
                columns: new[] { "SourceType", "ExternalAccount" });

            migrationBuilder.CreateIndex(
                name: "IX_post_targets_PostId",
                table: "post_targets",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_posts_UserId",
                table: "posts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_templates_UserId",
                table: "templates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_connectors_ServiceDefinitionId",
                table: "user_connectors",
                column: "ServiceDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_connectors_UserId",
                table: "user_connectors",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.DropTable(
                name: "external_interests");

            migrationBuilder.DropTable(
                name: "external_triggers");

            migrationBuilder.DropTable(
                name: "post_targets");

            migrationBuilder.DropTable(
                name: "secrets");

            migrationBuilder.DropTable(
                name: "templates");

            migrationBuilder.DropTable(
                name: "user_connectors");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "webhook_dedupe");

            migrationBuilder.DropTable(
                name: "posts");

            migrationBuilder.DropTable(
                name: "service_definitions");
        }
    }
}
