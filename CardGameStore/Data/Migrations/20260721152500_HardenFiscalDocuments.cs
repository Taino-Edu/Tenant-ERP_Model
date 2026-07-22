using CardGameStore.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260721152500_HardenFiscalDocuments")]
public partial class HardenFiscalDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "inutilizacoes_fiscais",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                ano = table.Column<int>(type: "integer", nullable: false),
                serie = table.Column<int>(type: "integer", nullable: false),
                numero_inicial = table.Column<int>(type: "integer", nullable: false),
                numero_final = table.Column<int>(type: "integer", nullable: false),
                justificativa = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                protocolo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                xml_retorno = table.Column<string>(type: "text", nullable: true),
                inutilizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("pk_inutilizacoes_fiscais", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_inutilizacoes_fiscais_faixa",
            table: "inutilizacoes_fiscais",
            columns: new[] { "ano", "serie", "numero_inicial", "numero_final" },
            unique: true);

        migrationBuilder.AddColumn<DateTime>(name: "erp_estornado_em", table: "notas_fiscais_emitidas", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "erp_estorno_erro", table: "notas_fiscais_emitidas", type: "text", nullable: true);

        migrationBuilder.AddColumn<DateTime>(name: "fiscal_effects_captured_at", table: "comandas", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<int>(name: "points_debited_at_sale", table: "comandas", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "cashback_debited_at_sale", table: "comandas", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "points_awarded_at_sale", table: "comandas", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<Guid>(name: "crediario_id_at_sale", table: "comandas", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<int>(name: "crediario_amount_at_sale", table: "comandas", type: "integer", nullable: false, defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(name: "fiscal_effects_captured_at", table: "vendas_avulsas", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<int>(name: "points_debited_at_sale", table: "vendas_avulsas", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "cashback_debited_at_sale", table: "vendas_avulsas", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "points_awarded_at_sale", table: "vendas_avulsas", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<Guid>(name: "crediario_id_at_sale", table: "vendas_avulsas", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<int>(name: "crediario_amount_at_sale", table: "vendas_avulsas", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTime>(name: "cancelado_em", table: "vendas_avulsas", type: "timestamp with time zone", nullable: true);

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "inutilizacoes_fiscais");
        migrationBuilder.DropColumn(name: "erp_estornado_em", table: "notas_fiscais_emitidas");
        migrationBuilder.DropColumn(name: "erp_estorno_erro", table: "notas_fiscais_emitidas");

        migrationBuilder.DropColumn(name: "fiscal_effects_captured_at", table: "comandas");
        migrationBuilder.DropColumn(name: "points_debited_at_sale", table: "comandas");
        migrationBuilder.DropColumn(name: "cashback_debited_at_sale", table: "comandas");
        migrationBuilder.DropColumn(name: "points_awarded_at_sale", table: "comandas");
        migrationBuilder.DropColumn(name: "crediario_id_at_sale", table: "comandas");
        migrationBuilder.DropColumn(name: "crediario_amount_at_sale", table: "comandas");

        migrationBuilder.DropColumn(name: "fiscal_effects_captured_at", table: "vendas_avulsas");
        migrationBuilder.DropColumn(name: "points_debited_at_sale", table: "vendas_avulsas");
        migrationBuilder.DropColumn(name: "cashback_debited_at_sale", table: "vendas_avulsas");
        migrationBuilder.DropColumn(name: "points_awarded_at_sale", table: "vendas_avulsas");
        migrationBuilder.DropColumn(name: "crediario_id_at_sale", table: "vendas_avulsas");
        migrationBuilder.DropColumn(name: "crediario_amount_at_sale", table: "vendas_avulsas");
        migrationBuilder.DropColumn(name: "cancelado_em", table: "vendas_avulsas");

    }
}
