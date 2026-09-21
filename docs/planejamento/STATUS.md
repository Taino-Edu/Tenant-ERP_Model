# Status atual — Tenant-ERP

> **Atualizado em 2026-09-21**, contra a `main` em `ecd10f7`. Este é o ponto de
> partida para decisões novas. A fila detalhada está em [`BACKLOG.md`](BACKLOG.md),
> o escopo `RB-01` a `RB-06` em
> [`REBUILD-ESCOPO-2026-08.md`](REBUILD-ESCOPO-2026-08.md) e a auditoria que
> originou as correções de resiliência em
> [`AUDITORIA-RESILIENCIA-2026-09-08.md`](../auditorias/AUDITORIA-RESILIENCIA-2026-09-08.md).
>
> Documentos de auditoria preservam a fotografia e as linhas da data em que
> foram escritos. Consulte este arquivo e o código antes de tratar um achado
> histórico como pendência atual.

## Onde cada documento manda

| Documento | Governa |
|---|---|
| [`STATUS.md`](STATUS.md) | Fotografia executiva verificada, entregas recentes, riscos e direção atual. |
| [`BACKLOG.md`](BACKLOG.md) | Fila operacional contínua: produto, CRM, dados, QA, UX e infraestrutura. |
| [`REBUILD-ESCOPO-2026-08.md`](REBUILD-ESCOPO-2026-08.md) | `RB-01` a `RB-06`: pagamentos, pedidos online, multi-CNPJ, comandas e aplicativos móveis. |
| [`AUDITORIA-RESILIENCIA-2026-09-08.md`](../auditorias/AUDITORIA-RESILIENCIA-2026-09-08.md) | Evidência original dos achados `RES-00x`/`AUTH-00x`; o estado de execução está resumido abaixo. |

## Entregue desde 2026-09-08

- **Resiliência (`RES-001` a `RES-007`) e revogação de sessão (`AUTH-001`).**
  Venda avulsa idempotente, crediário serializado com unicidade no banco,
  guardrails de produção com fail-fast, prontidão de migration por tenant,
  refresh tolerante a falha transitória, logout/reset que invalidam JWT, backup
  de uploads e deploy serializado por SHA. A implementação e a validação estão
  registradas em
  [`CORRECOES-RESILIENCIA-2026-09-08.md`](../auditorias/CORRECOES-RESILIENCIA-2026-09-08.md).
- **Cadastro autônomo de lojas.** O lojista solicita a loja no site, confirma o
  e-mail e define a senha somente na confirmação; pedidos não confirmados não
  provisionam schema nem administrador. O fluxo tem reserva atômica, token
  hasheado, rate limit e limites diários.
- **Condições comerciais e cobrança pelo painel.** Mensalidade, implantação,
  descontos, primeira cobrança, emissão manual no Asaas e avisos de cobrança
  passaram a ser controlados por tenant. Loja suspensa ainda consegue entrar e
  pagar a assinatura.
- **Deploy por artefato aprovado.** Backend e frontend são compilados no GitHub
  Actions, publicados no GHCR com a tag do commit e baixados pela VPS. Backup,
  lock, health check e rollback de imagens permanecem no `update.sh`.
- **.NET 10 LTS.** API, testes, EF Core, imagens e CI saíram do .NET 8. O smoke
  pós-deploy agora valida também o JSON do Swagger, que antes podia falhar com a
  página da UI ainda verde.
- **Manutenção de dependências.** Entraram atualização das Actions, Autoprefixer,
  Microsoft.NET.Test.Sdk 18.10.1, xUnit runner 4, FluentAssertions 7.2.2 e
  react-hot-toast 2.6.1, além de coverlet collector/msbuild 10.0.1. Majors que
  exigem migração real foram retiradas da fila de merge automático e viraram
  trabalho técnico explícito.
- **Acessibilidade e cobertura de interface.** Error boundaries por área, smoke
  Playwright público no build de produção e correções automáticas de
  acessibilidade do site institucional estão na `main`.

## Estado verificado em 2026-09-21

- **CI/CD verde:** backend contra PostgreSQL 16, frontend com lint + build,
  smoke Playwright em Chromium, publicação das duas imagens, deploy e smoke
  pós-deploy.
- **Suíte backend:** **1.090 testes aprovados, zero falhas**, novamente observados
  na CI após as atualizações de dependências desta rodada.
- **Frontend:** Next.js 15.5.25, React 18.3.1 e Tailwind CSS 3.4; existem **20
  specs Playwright** em `frontend/tests/`, das quais o lote público determinístico
  roda a cada PR. A revisão de dependências de 2026-09-21 terminou com
  `npm audit` sem vulnerabilidades conhecidas.
- **Backend:** ASP.NET Core/EF Core 10; o repositório contém 78 migrations de
  catálogo/tenant e o boot em banco vazio foi validado durante a migração.
- **Operação:** as imagens de produção são identificadas pelo SHA aprovado; o
  deploy não recompila na VPS no caminho normal, preserva rollback automático e
  os runners estão fixados em Ubuntu 24.04 para evitar a troca silenciosa de
  `ubuntu-latest` anunciada para outubro de 2026.
- **Repositório local:** uma única worktree registrada e nenhuma reconciliação
  pendente (`REP-001` concluído).

## Riscos e trabalhos abertos que importam agora

1. **`AUTH-002`: lockout e MFA.** Ainda não existe bloqueio por conta contra
   força bruta distribuída nem segundo fator para contas privilegiadas. Há plano
   de MFA/TOTP e login federado, mas implementação e política continuam abertas.
2. **Validar cadastro em ambiente real.** O fluxo de criação precisa de exercício
   ponta a ponta em homologação/produção com SMTP real, incluindo entrega do
   e-mail e provisionamento posterior à confirmação.
3. **Dívida pós-.NET 10.** A CI aponta `SYSLIB0057` no carregamento de PFX,
   `ASPDEPR005` nos forwarded headers e `CA2024` no leitor do Gemini. O PFX deve
   ser migrado com teste de certificados A1 legados; não é troca mecânica.
4. **Modernização do frontend.** Next 16 + React 19, Tailwind 4 e Lucide 1 são
   migrações de código separadas (`MOD-001` a `MOD-003` no backlog), não bumps de
   versão. A major do Next permanece visível no Dependabot; as demais ficam
   temporariamente ignoradas para não recriar PRs sabidamente quebrados.
5. **Proteção da `main`.** Não há regra de branch/ruleset impedindo merge com CI
   pendente. A disciplina atual é operacional; transformar os checks essenciais
   em obrigatórios reduz o risco de regressão por merge manual.

## Próxima direção recomendada

1. Fechar `AUTH-002`, começando por MFA das contas da plataforma e política de
   recuperação/lockout que não permita negação de serviço trivial.
2. Executar o teste real do cadastro com SMTP e registrar a evidência operacional.
3. Quitar os avisos pós-.NET 10 em PR pequeno, com teste dedicado ao certificado
   A1; qualquer futura troca do runner Ubuntu deve ser validada explicitamente.
4. Fazer `MOD-001` (Next 16 + React 19) com codemod, ESLint CLI, migração de
   middleware/proxy, correção do CSS no Turbopack e smoke completo. Tailwind 4 e
   Lucide 1 entram depois, cada um em PR próprio.
5. Retomar produto: `RB-06.1` (PWA do consumidor), `RB-02` (recebimento das
   vendas do lojista) e `RB-04` (multi-CNPJ), então consolidar CRM e analytics.

## Bloqueios externos

- **Homologação fiscal real** depende de contador, certificado/CSC e SEFAZ
  (`FIS-001`).
- **Cloudflare Full (Strict)** depende de certificado de origem no VPS
  (`OPS-002`).
- **Indexação no Google** (`MKT-001`) ainda depende da revisão externa do alerta
  de páginas enganosas.
- **Dados externos de mercado** dependem de fonte autorizada, orçamento e base
  legal.
