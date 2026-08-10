using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApuracaoTributariaEFechamentoFiscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "aliquota_icms_percentual",
                table: "fiscal_config",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "aliquota_iss_percentual",
                table: "fiscal_config",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "anexo_simples",
                table: "fiscal_config",
                type: "text",
                nullable: false,
                // Anexo I (comércio) é o perfil de quase toda loja já provisionada;
                // "" (default do EF) não é enum válido e quebraria a leitura.
                defaultValue: "I");

            migrationBuilder.AddColumn<long>(
                name: "folha_pagamento12m_em_centavos",
                table: "fiscal_config",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "folha_pagamento_mensal_em_centavos",
                table: "fiscal_config",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<decimal>(
                name: "percentual_presuncao_csll",
                table: "fiscal_config",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 12m);

            migrationBuilder.AddColumn<decimal>(
                name: "percentual_presuncao_irpj",
                table: "fiscal_config",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 8m);

            migrationBuilder.CreateTable(
                name: "fechamentos_fiscais_mensais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    periodo_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    periodo_fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    receita_bruta = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    deducoes = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    impostos_sobre_vendas = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    receita_liquida = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    custo_mercadoria_vendida = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    despesas_operacionais = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    resultado_operacional = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    resultado_liquido = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    notas_autorizadas = table.Column<int>(type: "integer", nullable: false),
                    notas_canceladas = table.Column<int>(type: "integer", nullable: false),
                    valor_notas_autorizadas = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    notas_entrada = table.Column<int>(type: "integer", nullable: false),
                    valor_notas_entrada = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    regime_apurado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    imposto_apurado = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    aliquota_efetiva = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    fechado_por_contador_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fechado_por_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fechado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fechamentos_fiscais_mensais", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fechamentos_fiscais_competencia",
                table: "fechamentos_fiscais_mensais",
                columns: new[] { "ano", "mes" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fechamentos_fiscais_mensais");

            migrationBuilder.DropColumn(
                name: "aliquota_icms_percentual",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "aliquota_iss_percentual",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "anexo_simples",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "folha_pagamento12m_em_centavos",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "folha_pagamento_mensal_em_centavos",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "percentual_presuncao_csll",
                table: "fiscal_config");

            migrationBuilder.DropColumn(
                name: "percentual_presuncao_irpj",
                table: "fiscal_config");
        }
    }
}
