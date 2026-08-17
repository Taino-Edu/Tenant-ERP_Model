using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class ShareIbptTableAcrossTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // tenant-zero já pode ter criado public.ibpt_tabela pela migration
            // antiga do AppDbContext. O IF NOT EXISTS torna a promoção segura
            // tanto em instalações existentes quanto em bancos novos.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS public.ibpt_tabela (
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
                    ON public.ibpt_tabela (ncm, uf, importado);

                DO $migration$
                DECLARE origem record;
                BEGIN
                    FOR origem IN
                        SELECT table_schema
                        FROM information_schema.tables
                        WHERE table_name = 'ibpt_tabela'
                          AND table_schema <> 'public'
                          AND table_schema NOT LIKE 'pg_%'
                          AND table_schema <> 'information_schema'
                    LOOP
                        EXECUTE format(
                            'INSERT INTO public.ibpt_tabela
                                (id, ncm, uf, importado, percentual_federal,
                                 percentual_estadual, percentual_municipal, fonte,
                                 versao, chave, vigencia_inicio, vigencia_fim, atualizado_em)
                             SELECT id, ncm, uf, importado, percentual_federal,
                                    percentual_estadual, percentual_municipal, fonte,
                                    versao, chave, vigencia_inicio, vigencia_fim, atualizado_em
                             FROM %I.ibpt_tabela
                             ON CONFLICT (ncm, uf, importado) DO UPDATE SET
                                percentual_federal = EXCLUDED.percentual_federal,
                                percentual_estadual = EXCLUDED.percentual_estadual,
                                percentual_municipal = EXCLUDED.percentual_municipal,
                                fonte = EXCLUDED.fonte,
                                versao = EXCLUDED.versao,
                                chave = EXCLUDED.chave,
                                vigencia_inicio = EXCLUDED.vigencia_inicio,
                                vigencia_fim = EXCLUDED.vigencia_fim,
                                atualizado_em = EXCLUDED.atualizado_em
                             WHERE EXCLUDED.atualizado_em > public.ibpt_tabela.atualizado_em',
                            origem.table_schema);
                    END LOOP;
                END $migration$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não apaga dado fiscal compartilhado em rollback. A tabela já
            // existia em public antes desta migration em instalações antigas.
        }
    }
}
