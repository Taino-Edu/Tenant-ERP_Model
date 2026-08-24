# Status atual — Tenant-ERP

> Atualizado em 2026-08-11. O backlog vigente e priorizado está em
> [`BACKLOG.md`](BACKLOG.md). Este arquivo é apenas um resumo executivo.

## Estado da entrega

- `origin/main` inclui a entrega funcional `66c081b`.
- Multi-tenant por schema e catálogo central estão implementados.
- Billing da plataforma possui implantação, mensalidades, baixa, MRR e
  inadimplência.
- Leads, prospecção e conversão existem; o CRM completo ainda está em construção.
- Indicações e comissões de vendedores autônomos estão implementadas e aguardam
  validação de staging e definição das políticas comerciais finais.
- Restaurante/comandas possuem comentários, áreas de produção, fila e estados de
  preparo; falta validação E2E no ambiente implantado.
- Frontend e backend compilam. O lint passou em 2026-08-11.
- A Distribuição DF-e agora possui lock distribuído, cooldown e quota isolados
  por tenant/CNPJ/ambiente; `656` retorna 429 e concorrência retorna 409.
- Integrações exibe saúde da SEFAZ por UF/ambiente, latência, `cStat`, horário da
  medição e botão de reteste sem consumir a quota de Distribuição DF-e.
- Consentimento de cookies, documentos legais v2 e SEO técnico público estão na
  `main`.
- A prospecção atual usa OSM/Nominatim/Overpass, mas as pesquisas são efêmeras,
  limitadas a 60 resultados, ignoram relações OSM e cobrem poucas categorias.
- Testes focados de billing/comissões e a suíte completa passaram. Após corrigir
  a limpeza de schemas, a base passou com 750 testes; após a proteção SEFAZ,
  passaram 758/758 testes no PostgreSQL real após a inclusão do indicador SEFAZ.

## Próxima direção recomendada

1. Persistir pesquisas/candidatos e ampliar a cobertura OSM da prospecção.
2. Consolidar o modelo de CRM: contas, contatos, oportunidades, atividades,
   responsáveis, histórico e atribuição.
3. Enriquecer com Receita/CNPJ, IBGE e sites oficiais com proveniência.
4. Criar a camada analítica de aquisição, receita, churn e comissões.
5. Implementar cobrança recorrente real dos tenants.

## Bloqueios externos

- Homologação fiscal real depende de contador, certificado/CSC e SEFAZ.
- Cloudflare Full (Strict) depende de certificado de origem e alteração de infra.
- Dados externos de mercado dependem de fonte autorizada, orçamento e política LGPD.
- Pagamentos recorrentes dependem da escolha e credenciais do gateway.

## Trabalho paralelo a reconciliar

- Uma worktree do Claude ainda contém a versão original não commitada da
  sanitização de HTML; a correção já foi portada, ampliada e validada na branch atual.
- Worktrees limpas de segurança, VAPID, Swagger e auditoria de carga possuem
  commits fora da `main`; devem ser revisadas individualmente, não mescladas em lote.

Consulte os IDs e critérios completos no início de [`BACKLOG.md`](BACKLOG.md).
