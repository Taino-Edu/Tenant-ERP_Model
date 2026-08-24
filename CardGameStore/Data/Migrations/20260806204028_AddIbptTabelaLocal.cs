using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIbptTabelaLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O catálogo compartilhado é migrado antes do tenant-zero e pode já
            // ter criado public.ibpt_tabela. A migration precisa continuar criando
            // a tabela nos schemas dos demais tenants, mas ser segura em public.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS ibpt_tabela (
                    id uuid NOT NULL,
                    ncm character varying(8) NOT NULL,
                    uf character varying(2) NOT NULL,
                    importado boolean NOT NULL,
                    percentual_federal numeric NOT NULL,
                    percentual_estadual numeric NOT NULL,
                    percentual_municipal numeric NOT NULL,
                    fonte character varying(100),
                    versao character varying(30),
                    chave character varying(50),
                    vigencia_inicio timestamp with time zone,
                    vigencia_fim timestamp with time zone,
                    atualizado_em timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_ibpt_tabela" PRIMARY KEY (id)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_ibpt_tabela_ncm_uf_origem
                    ON ibpt_tabela (ncm, uf, importado);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ibpt_tabela");
        }
    }
}
