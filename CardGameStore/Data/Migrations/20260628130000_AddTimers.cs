using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    public partial class AddTimers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS timers (
                    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                    name             VARCHAR(100) NOT NULL DEFAULT 'Timer',
                    duration_seconds INTEGER      NOT NULL DEFAULT 1800,
                    paused_remaining INTEGER      NULL,
                    state            INTEGER      NOT NULL DEFAULT 0,
                    started_at       TIMESTAMPTZ  NULL,
                    sound_preset     VARCHAR(50)  NOT NULL DEFAULT 'bell',
                    warn_at_seconds  INTEGER      NOT NULL DEFAULT 60,
                    created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    updated_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("timers");
        }
    }
}
