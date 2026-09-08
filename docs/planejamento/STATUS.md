# Status atual — Tenant-ERP

> **Atualizado em 2026-09-08**, contra a `main` em `2bfb896`. Resumo executivo:
> a fila priorizada está em [`BACKLOG.md`](BACKLOG.md), as decisões de escopo de
> agosto em [`REBUILD-ESCOPO-2026-08.md`](REBUILD-ESCOPO-2026-08.md) e os achados
> de resiliência em
> [`AUDITORIA-RESILIENCIA-2026-09-08.md`](../auditorias/AUDITORIA-RESILIENCIA-2026-09-08.md).
>
> **Aviso de leitura:** a revisão anterior deste arquivo era de 2026-08-11 e
> ficou quase um mês para trás de **170 commits**. Várias afirmações dela já
> eram falsas (prospecção limitada a 60 resultados, gateway de pagamento não
> escolhido, Next 14). Se este cabeçalho estiver com mais de um mês, desconfie
> do conteúdo antes de decidir qualquer coisa com ele.

## Onde os três documentos de planejamento se dividem

Existiam dois documentos concorrentes sem dizer qual mandava. A divisão a partir
de agora:

| Documento | Governa |
|---|---|
| [`BACKLOG.md`](BACKLOG.md) | Fila operacional contínua: CRM, prospecção, dados, QA, UX, infra. |
| [`REBUILD-ESCOPO-2026-08.md`](REBUILD-ESCOPO-2026-08.md) | Os cinco itens `RB-01` a `RB-05` decididos em 26/08: pagamentos, pedidos online, multi-CNPJ, comandas. **Manda sobre o backlog nesses cinco temas.** |
| [`AUDITORIA-RESILIENCIA-2026-09-08.md`](../auditorias/AUDITORIA-RESILIENCIA-2026-09-08.md) | Achados `RES-00x`/`AUTH-00x` de idempotência, concorrência, guardrails e sessão. |

## Entregue desde a revisão anterior (2026-08-11 → 2026-09-08)

- **Cobrança automática da plataforma (`RB-01`) — concluída.** Gateway decidido:
  **Asaas**, atrás da interface `IPlatformPaymentGateway`. Job emite, webhook dá
  baixa, régua suspende e reativa sozinha. Validado ponta a ponta em sandbox em
  2026-08-31. *Isto encerra o que o backlog antigo listava como `PAY-001` com
  "decisão pendente de gateway".*
- **Comandas fora do gate do Restaurante (`RB-05`)** — na `main` desde 2026-08-26.
- **Integração REST multi-tenant por escopos**, tenants externos integrados e
  motor fiscal hospedado para terceiros; módulos Financeiro e Fiscal empacotados
  em `packages/` com script de exportação.
- **Next.js 14.2.35 → 15.5.21.** *Isto encerra o `QA-004`, que o backlog ainda
  listava como bloqueado.*
- **Mensuração com consentimento:** GTM e Meta Pixel atrás do consentimento, com
  escopo comercial e eventos.
- **Navegação por área no admin**, com subpáginas dentro da área ativa.
- **Vitrine pública de tenants** com controle de visibilidade por loja.
- **Financeiro:** inteligência gerencial e lançamento/alteração manual de cobrança.
- **Rate limit** extraído para política própria e testável.
- **Correções de multi-tenancy:** tenant ausente não é cacheado, tenant
  indisponível é identificado explicitamente, e o guard deixou de falhar aberto.
- **Contato de segurança publicado** e smoke de deploy endurecido.

## Estado verificado hoje

- `main` limpa e alinhada com `origin/main` em `2bfb896`.
- **CI** roda: build + testes do backend contra Postgres real, lint + build do
  frontend, deploy no VPS e smoke pós-deploy. **Playwright não roda no CI.**
- **20 specs Playwright** em `frontend/tests/` — o backlog antigo dizia cinco.
- **Error boundaries** existem na raiz e em `/admin`. **Não existem** em
  `/plataforma`, `/cliente` e `/contador`.
- **Seis worktrees** registradas; apenas `.claude/worktrees/musing-solomon-133b2e`
  tem alteração não commitada (dois arquivos, os do `SEC-001` já portado). As
  outras cinco estão limpas, com commits fora da `main`.
- **Suíte de testes:** o último número registrado é **893 testes, zero falhas**
  (2026-08-26, entrega do `RB-01`). **Não reexecutei** — o Docker local está
  indisponível e o Postgres de teste sobe por `tests/docker-compose.yml`.

## Riscos abertos que valem mais que features novas

Detalhe e evidência em
[`AUDITORIA-RESILIENCIA-2026-09-08.md`](../auditorias/AUDITORIA-RESILIENCIA-2026-09-08.md).

1. **Venda avulsa não é idempotente** e o retry do EF está ligado — uma falha de
   rede no commit pode registrar a venda duas vezes, com estoque e receita
   dobrados (`RES-001`).
2. **Acúmulo do crediário perde atualização concorrente**; não há token de
   concorrência em lugar nenhum, e o índice de crediário aberto por cliente não é
   único (`RES-002`).
3. **Guardrails de produção que só avisam:** senha de seed padrão em duas contas
   privilegiadas e segredo JWT de exemplo sem validação de boot (`RES-003`).
4. **Loja com migration quebrada continua atendendo** e o health check diz que
   está tudo bem (`RES-004`).
5. **Backup não cobre os uploads** dos tenants (`RES-006`).
6. **Logout não invalida o access token** e não há trava de conta nem 2FA
   (`AUTH-001`, `AUTH-002`).

## Próxima direção recomendada

1. Fechar os itens de resiliência `RES-003`, `RES-001` e `RES-002` — nessa ordem.
2. `REP-001`: decidir uma a uma as seis worktrees e as branches fora da `main`.
3. `RB-02` (recebimento das vendas do lojista) e `RB-04` (multi-CNPJ), os dois de
   prioridade alta que sobraram do rebuild.
4. Consolidar CRM (contas, contatos, atribuição) e a camada analítica.
5. Prospecção: favoritos, filtros, seleção em lote e deduplicação secundária.

## Bloqueios externos

- **Homologação fiscal real** depende de contador, certificado/CSC e SEFAZ
  (`FIS-001`).
- **Cloudflare Full (Strict)** depende de certificado de origem no VPS
  (`OPS-002`).
- **Indexação no Google** (`MKT-001`): propriedade acessível e sitemap processado
  com 6 URLs; trava atual é o alerta "Páginas enganosas" sem URLs de amostra.
- **Dados externos de mercado** dependem de fonte autorizada, orçamento e base
  legal.

> Pagamentos recorrentes **saíram** desta lista: o gateway foi escolhido e o
> `RB-01` está concluído.
