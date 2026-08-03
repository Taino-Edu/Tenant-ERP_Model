using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class AddPlatformIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_integrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    client_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    client_secret_encrypted = table.Column<string>(type: "text", nullable: true),
                    certificate_crt_encrypted = table.Column<string>(type: "text", nullable: true),
                    certificate_key_encrypted = table.Column<string>(type: "text", nullable: true),
                    conta_corrente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    pix_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_integrations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_integrations_provider",
                table: "platform_integrations",
                column: "provider",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_integrations");
        }
    }
}
