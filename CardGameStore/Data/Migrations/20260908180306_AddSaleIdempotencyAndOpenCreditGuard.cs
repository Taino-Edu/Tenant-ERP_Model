using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleIdempotencyAndOpenCreditGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_crediarios_user_status",
                table: "crediarios");

            migrationBuilder.AddColumn<string>(
                name: "request_fingerprint",
                table: "vendas_avulsas",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Versões anteriores permitiam mais de um crediário aberto por cliente.
            // Consolida esses registros antes de criar a restrição: a conta mais antiga
            // é mantida, valores/pagamentos/referências são somados ou redirecionados e
            // os snapshots de itens das comandas também são preservados.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE _crediario_merge ON COMMIT DROP AS
                SELECT id AS source_id,
                       FIRST_VALUE(id) OVER (
                           PARTITION BY user_id
                           ORDER BY data_abertura, id) AS keeper_id,
                       COUNT(*) OVER (PARTITION BY user_id) AS account_count
                FROM crediarios
                WHERE status = 'Aberto';

                DELETE FROM _crediario_merge WHERE account_count = 1;

                WITH source_items AS (
                    SELECT m.keeper_id,
                           CASE
                               WHEN NULLIF(BTRIM(c.itens_json), '') IS NOT NULL
                                   THEN c.itens_json::jsonb
                               WHEN c.comanda_id IS NOT NULL THEN COALESCE((
                                   SELECT jsonb_agg(jsonb_build_object(
                                       'ItemName', ci.item_name_snapshot,
                                       'Quantity', ci.quantity,
                                       'UnitPriceInReais', ci.unit_price_in_cents / 100.0,
                                       'SubtotalInReais', ci.subtotal_in_cents / 100.0)
                                       ORDER BY ci.added_at)
                                   FROM comanda_items ci
                                   WHERE ci.comanda_id = c.comanda_id
                               ), '[]'::jsonb)
                               ELSE '[]'::jsonb
                           END AS items
                    FROM _crediario_merge m
                    JOIN crediarios c ON c.id = m.source_id
                ), merged_items AS (
                    SELECT keeper_id,
                           COALESCE(jsonb_agg(item) FILTER (WHERE item IS NOT NULL), '[]'::jsonb) AS nested_items
                    FROM source_items s
                    LEFT JOIN LATERAL jsonb_array_elements(s.items) item ON TRUE
                    GROUP BY keeper_id
                ), totals AS (
                    SELECT m.keeper_id,
                           SUM(c.valor_em_centavos)::integer AS total_value,
                           SUM(c.valor_pago_em_centavos)::integer AS paid_value,
                           MIN(c.data_abertura) AS opened_at,
                           MAX(c.data_vencimento) AS due_at,
                           STRING_AGG(NULLIF(BTRIM(c.observacao), ''), ' | '
                               ORDER BY c.data_abertura) AS notes
                    FROM _crediario_merge m
                    JOIN crediarios c ON c.id = m.source_id
                    GROUP BY m.keeper_id
                )
                UPDATE crediarios keeper
                SET valor_em_centavos = totals.total_value,
                    valor_pago_em_centavos = totals.paid_value,
                    data_abertura = totals.opened_at,
                    data_vencimento = totals.due_at,
                    observacao = LEFT(totals.notes, 500),
                    itens_json = merged_items.nested_items::text
                FROM totals
                JOIN merged_items ON merged_items.keeper_id = totals.keeper_id
                WHERE keeper.id = totals.keeper_id;

                UPDATE pagamentos_crediario p
                SET crediario_id = m.keeper_id
                FROM _crediario_merge m
                WHERE p.crediario_id = m.source_id
                  AND m.source_id <> m.keeper_id;

                UPDATE pix_cobrancas p
                SET crediario_id = m.keeper_id
                FROM _crediario_merge m
                WHERE p.crediario_id = m.source_id
                  AND m.source_id <> m.keeper_id;

                UPDATE comandas c
                SET crediario_id_at_sale = m.keeper_id
                FROM _crediario_merge m
                WHERE c.crediario_id_at_sale = m.source_id
                  AND m.source_id <> m.keeper_id;

                UPDATE vendas_avulsas v
                SET crediario_id_at_sale = m.keeper_id
                FROM _crediario_merge m
                WHERE v.crediario_id_at_sale = m.source_id
                  AND m.source_id <> m.keeper_id;

                DELETE FROM crediario_email_outbox o
                USING _crediario_merge m
                WHERE o."Id" = m.source_id
                  AND m.source_id <> m.keeper_id;

                UPDATE crediario_email_outbox o
                SET "Valor" = c.valor_em_centavos / 100.0,
                    "Vencimento" = c.data_vencimento
                FROM crediarios c
                WHERE o."Id" = c.id
                  AND EXISTS (
                      SELECT 1 FROM _crediario_merge m WHERE m.keeper_id = c.id);

                DELETE FROM crediarios c
                USING _crediario_merge m
                WHERE c.id = m.source_id
                  AND m.source_id <> m.keeper_id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_crediarios_user_status",
                table: "crediarios",
                columns: new[] { "user_id", "status" },
                unique: true,
                filter: "status = 'Aberto'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_crediarios_user_status",
                table: "crediarios");

            migrationBuilder.DropColumn(
                name: "request_fingerprint",
                table: "vendas_avulsas");

            migrationBuilder.CreateIndex(
                name: "ix_crediarios_user_status",
                table: "crediarios",
                columns: new[] { "user_id", "status" });
        }
    }
}
