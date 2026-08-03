using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformTeamAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_platform_primary_owner",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "platform_access_profile",
                table: "users",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform_permissions_json",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "session_version",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE users
                SET platform_access_profile = 'partner_admin',
                    platform_permissions_json = '["platform.dashboard","platform.tenants.read","platform.tenants.manage","platform.tenants.delete","platform.finance.read","platform.finance.manage","platform.leads","platform.support","platform.logs","platform.impersonate"]',
                    session_version = 1
                WHERE role = 'PlatformOwner';

                WITH primary_owner AS (
                    SELECT id FROM users
                    WHERE role = 'PlatformOwner' AND is_active = TRUE
                    ORDER BY created_at, id
                    LIMIT 1
                )
                UPDATE users
                SET is_platform_primary_owner = TRUE,
                    platform_access_profile = 'primary_owner',
                    platform_permissions_json = '["platform.*"]'
                WHERE id IN (SELECT id FROM primary_owner);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_platform_primary_owner",
                table: "users");

            migrationBuilder.DropColumn(
                name: "platform_access_profile",
                table: "users");

            migrationBuilder.DropColumn(
                name: "platform_permissions_json",
                table: "users");

            migrationBuilder.DropColumn(
                name: "session_version",
                table: "users");
        }
    }
}
