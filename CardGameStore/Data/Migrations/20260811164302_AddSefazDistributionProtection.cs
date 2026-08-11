using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSefazDistributionProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sefaz_distribution_state",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cnpj = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: false),
                    ambiente = table.Column<string>(type: "text", nullable: false),
                    ultimo_nsu = table.Column<long>(type: "bigint", nullable: false),
                    proxima_consulta_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    bloqueado_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sync_lock_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sync_lock_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consulta_pontual_janela_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consulta_pontual_quantidade = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sefaz_distribution_state", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sefaz_distribution_state_sync_lock_ate",
                table: "sefaz_distribution_state",
                column: "sync_lock_ate");

            migrationBuilder.CreateIndex(
                name: "ux_sefaz_distribution_state_cnpj_ambiente",
                table: "sefaz_distribution_state",
                columns: new[] { "cnpj", "ambiente" },
                unique: true);

            // Preserva o NSU já consumido. O cooldown inicial é deliberadamente
            // conservador: o deploy não sabe se o CNPJ recebeu 137/656 antes da
            // atualização, então aguarda uma janela segura antes do primeiro ciclo.
            migrationBuilder.Sql("""
                INSERT INTO sefaz_distribution_state
                    (id, cnpj, ambiente, ultimo_nsu, proxima_consulta_em,
                     consulta_pontual_quantidade, created_at, updated_at)
                SELECT
                    id,
                    regexp_replace(upper(cnpj), '[^A-Z0-9]', '', 'g'),
                    ambiente,
                    dist_ultimo_nsu,
                    NOW() + INTERVAL '65 minutes',
                    0,
                    NOW(),
                    NOW()
                FROM fiscal_config
                WHERE nullif(trim(cnpj), '') IS NOT NULL
                ON CONFLICT (cnpj, ambiente) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sefaz_distribution_state");
        }
    }
}
