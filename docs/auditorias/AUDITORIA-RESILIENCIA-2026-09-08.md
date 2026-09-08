# Auditoria de resiliência e autenticação — 2026-09-08

> **Método:** leitura de código na `main` em `2bfb896`. Build e lint **não** foram
> reexecutados nesta rodada; a suíte unitária **não** rodou porque o Docker local
> está indisponível (o Postgres de teste sobe por `tests/docker-compose.yml`).
> Nenhum cenário de concorrência foi reproduzido em execução — todos os achados
> abaixo vêm de leitura, e cada um traz o arquivo e a linha para conferência.
>
> Origem: revisão cruzada entre duas varreduras independentes (uma focada em
> autenticação, outra em resiliência transacional), reconciliadas contra o código.

## Como ler os estados

- `CONFIRMADO` — reproduzi a leitura, o arquivo e a linha batem com a descrição.
- `CONFIRMADO COM RESSALVA` — o problema existe, mas o diagnóstico original
  estava impreciso em algum detalhe que **muda a correção**. A ressalva está no
  texto.
- `NÃO VERIFICADO` — levantado, mas não confirmei. Não usar como base de decisão
  sem checar antes.

---

## P0 — dinheiro e configuração

### RES-001 — Venda avulsa não é idempotente

- **Estado:** `CONFIRMADO`
- **Onde:** [`VendaAvulsaService.cs:85`](../../CardGameStore/Services/Implementations/VendaAvulsaService.cs)
  (`strategy.ExecuteAsync`), commit da transação na linha 412.
- **O problema:** a requisição de venda não carrega chave de idempotência, e a
  entidade `VendaAvulsa` é criada **dentro** do bloco que pode repetir. Se o
  Postgres confirmar o commit e a conexão cair antes do ACK, não há como
  distinguir "não gravou" de "gravou e perdi a resposta".
- **Agravante medido:** `EnableRetryOnFailure(maxRetryCount: 5)` está **ligado**
  em [`Program.cs:143`](../../CardGameStore/Program.cs) e
  [`Program.cs:166`](../../CardGameStore/Program.cs). Não é só o cliente que pode
  repetir por duplo clique ou retry de proxy — **o próprio EF re-executa o bloco
  inteiro** quando classifica a falha como transiente. O resultado de uma
  repetição é estoque baixado duas vezes, receita registrada duas vezes e
  crediário acumulado duas vezes.
- **O que facilita a correção:** o padrão **já existe no repositório**.
  `PagamentosCrediario` tem `IdempotencyKey` com índice único parcial
  ([`AppDbContext.cs:438`](../../CardGameStore/Data/AppDbContext.cs)) e
  `NotasFiscais` também ([`AppDbContext.cs:244`](../../CardGameStore/Data/AppDbContext.cs)).
  A venda avulsa simplesmente nunca ganhou. Não é arquitetura nova.
- **Concluído quando:** cada tentativa de venda carrega chave persistente e
  única; a repetição devolve a venda existente em vez de criar outra; a intenção
  de emissão fiscal é gravada na mesma transação.
- **Referência externa:** [resiliência de conexão no EF Core](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency).

### RES-002 — Acúmulo do crediário perde atualização concorrente

- **Estado:** `CONFIRMADO COM RESSALVA`
- **Onde:** [`VendaAvulsaService.cs:229`](../../CardGameStore/Services/Implementations/VendaAvulsaService.cs).
- **O problema:** o serviço lê o crediário aberto, soma `ValorEmCentavos` em
  memória e grava. O `ItensJson` é concatenado do mesmo jeito. Dois caixas leem
  R$ 100, um grava 150, o outro grava 180 calculado em cima do 100 velho —
  some a diferença, e itens somem junto.
- **Ressalva que muda a correção:** a escrita **já está dentro da transação**
  (commit na linha 412). Isso não resolve nada — em READ COMMITTED, ler-somar-gravar
  perde update mesmo dentro de transação — mas significa que a correção **não** é
  "coloque numa transação". É `SELECT ... FOR UPDATE` na linha do crediário,
  ou token de concorrência, ou incremento atômico via `ExecuteUpdate`.
- **Verificado à parte:** não existe **nenhum** `IsConcurrencyToken` no
  `AppDbContext` inteiro, e `ix_crediarios_user_status`
  ([`AppDbContext.cs:403`](../../CardGameStore/Data/AppDbContext.cs)) **não é
  único**, apesar da regra de negócio de um crediário aberto por cliente.
- **Concluído quando:** duas vendas simultâneas no mesmo cliente somam os dois
  valores, e o banco rejeita um segundo crediário aberto para o mesmo cliente.

### RES-003 — Guardrails de produção que só avisam em vez de derrubar

- **Estado:** `CONFIRMADO` — três ocorrências da mesma classe de defeito.
- **Onde:**
  - [`Program.cs:729`](../../CardGameStore/Program.cs) — o seed do admin cai em
    `?? "SenhaForte@123"` e só emite `LogWarning`.
  - [`Program.cs:756`](../../CardGameStore/Program.cs) — o seed do dono da
    plataforma faz exatamente o mesmo, para a conta mais poderosa do sistema.
  - [`appsettings.json:19`](../../CardGameStore/appsettings.json) — o segredo JWT
    de exemplo é `SUBSTITUA_ESTA_CHAVE_...` e **nada** no boot verifica se ele
    continua lá. Com o segredo do repositório público, qualquer um forja um JWT
    de `PlatformOwner`.
- **Atenuante real:** o `deploy/setup.sh` gera segredo aleatório (linha 99) e
  grava `COOKIE_SECURE=true` (linha 121). Ou seja, o deploy pelo caminho oficial
  nasce correto. O buraco é para quem subir fora do script ou apagar a variável
  depois — e nesse caso o sistema **sobe e atende** em vez de recusar.
- **Concluído quando:** valor ausente, vazio ou igual ao padrão conhecido
  **derruba o boot** em produção, do mesmo jeito que a connection string já faz.

---

## P1 — isolamento, sessão e recuperação

### RES-004 — Loja com migration quebrada continua atendendo

- **Estado:** `CONFIRMADO`
- **Onde:** [`Program.cs:705`](../../CardGameStore/Program.cs) — o `catch` do loop
  registra o tenant que falhou e segue; o comentário no código assume a escolha de
  propósito, para não travar o boot dos outros. E
  [`DbHealthCheck.cs`](../../CardGameStore/HealthChecks/DbHealthCheck.cs) executa
  `SELECT 1` no tenant-zero.
- **O problema:** a loja afetada roda em schema desatualizado, e o health check
  responde saudável enquanto os endpoints novos quebram só para ela.
- **Concluído quando:** existe versão e estado de migração **por tenant**, e a
  loja afetada entra em manutenção controlada com resposta clara e alerta, em vez
  de atender errado.

### RES-005 — Falha temporária expulsa o usuário da sessão

- **Estado:** `CONFIRMADO`
- **Onde:** [`api.ts:65`](../../frontend/lib/api.ts) — o `catch` do interceptor
  dispara em **qualquer** falha do refresh, inclusive rede caída e HTTP 500, e
  chama `clearAuth()` + redireciona. O `axios.create` (linha 15) também não define
  `timeout`.
- **Concluído quando:** sessão inválida é distinguida de serviço indisponível, o
  trabalho preenchido na tela sobrevive, e a recuperação não repete escrita
  automaticamente.

### AUTH-001 — Logout não invalida o access token

- **Estado:** `CONFIRMADO`
- **Onde:** `AuthService.LogoutAsync`
  ([`AuthService.cs:224`](../../CardGameStore/Services/Implementations/AuthService.cs))
  limpa apenas o refresh token. O JWT já emitido continua válido até expirar
  (60 min em produção, por `JwtSettings__AccessTokenExpirationMinutes`).
- **Mesmo buraco ao desativar um usuário:** o
  [`OperatorPermissionMiddleware.cs:84`](../../CardGameStore/Middleware/OperatorPermissionMiddleware.cs)
  relê o banco e checa `IsActive`, mas **só para `Operator`**. Desativar um
  `Admin` não derruba a sessão viva dele.
- **Existe branch parada sobre isso:** `claude/plano-logout-sessao` (ver `REP-001`
  no backlog).

### AUTH-002 — Sem trava de conta e sem 2FA

- **Estado:** `CONFIRMADO`
- **O que existe:** rate limit por IP+tenant no login — 15/min na política `auth`
  ([`RequestRateLimits.cs:38`](../../CardGameStore/Security/RequestRateLimits.cs)) —
  e 5/hora no `locate-account`.
- **O que não existe:** nenhum `LockoutEnd`/contador de tentativas por conta (grep
  sem resultado), então força bruta distribuída testa senha devagar em muitos IPs
  sem nunca travar a conta. E não há 2FA em lugar nenhum, nem para
  `PlatformOwner` — que enxerga a lista de clientes de todas as lojas.
- **Senha exige só 8 caracteres** ([`AuthDtos.cs:47`](../../CardGameStore/DTOs/AuthDtos.cs)),
  sem checagem contra senhas comuns ou vazadas.

---

## P2 — operação e deploy

### RES-006 — Backup não cobre os uploads

- **Estado:** `CONFIRMADO`
- **Onde:** [`backup.sh`](../../deploy/backup.sh) cobre o Postgres do ERP e o banco
  da Evolution (WhatsApp). Não há **nenhuma** menção a upload no script.
- **O que fica de fora:** o volume nomeado `api_uploads`
  ([`docker-compose.prod.yml:371`](../../deploy/docker-compose.prod.yml)), montado
  em `/app/wwwroot/uploads` — logos e imagens de produto de todos os tenants,
  vivendo só no disco do VPS, que é justamente o disco do qual o off-site deveria
  proteger.
- **Concluído quando:** uploads entram no envio off-site, a restauração completa é
  testada de verdade, e existe número acordado de tempo parado e de dado perdido
  aceitáveis.

### RES-007 — Deploys concorrentes disputam imagens e rollback

- **Estado:** `CONFIRMADO`
- **Onde:** [`update.sh:62`](../../deploy/update.sh) faz `git pull origin main` sem
  SHA fixo, e não há `flock` nem exclusão mútua no script. Os workflows em
  `.github/workflows/` **não** declaram `concurrency:`.
- **Consequência:** duas execuções simultâneas disputam as mesmas imagens, e o
  deploy pode subir um commit diferente daquele que o CI aprovou.
- **Concluído quando:** deploys são serializados e a imagem publicada é
  identificada pelo commit aprovado.

### OPS — HTTP na origem

- **Estado:** `CONFIRMADO COM RESSALVA` — já rastreado como `OPS-002` no backlog.
- `COOKIE_SECURE: "${COOKIE_SECURE:-false}"`
  ([`docker-compose.prod.yml:164`](../../deploy/docker-compose.prod.yml)) tem
  default errado, **mas** o `setup.sh` grava `true` no `.env` gerado. Só morde
  quem subir fora do script.
- O trecho Cloudflare → origem sem TLS depende de certificado de origem no VPS —
  é a dependência que mantém `OPS-002` bloqueado, não uma descoberta nova.

---

## Não verificado

- **Caminhos de fidelidade inalcançáveis** após a rejeição inicial no serviço de
  vendas. Levantado numa das varreduras, não confirmei. Checar antes de agir.
- **Contagem atual da suíte.** O último número registrado é 893 testes
  (`REBUILD-ESCOPO-2026-08.md`, 2026-08-26). Não reexecutei — Docker indisponível.

---

## O que já está bem resolvido

Registrado para não ser reaberto como pendência numa próxima varredura:

- Isolamento por schema sem fallback silencioso para outro tenant; o
  `TenantConnectionInterceptor` falha rápido se a conexão abrir sem tenant.
- Baixa de estoque **atômica** via `ExecuteUpdateAsync` com guarda de quantidade
  ([`VendaAvulsaService.cs:105`](../../CardGameStore/Services/Implementations/VendaAvulsaService.cs)) —
  venda simultânea não fura estoque.
- Refresh token guardado **hasheado em SHA-256**, gerado com
  `RandomNumberGenerator` e rotacionado a cada uso; reset de senha invalida sessão.
- Access token só em cookie `HttpOnly` — XSS não lê token.
- JWT valida issuer, audience, lifetime e assinatura, com `ClockSkew = Zero`.
- Tickets de impersonação e de login-redirect são de uso único, expiram e são
  amarrados ao domínio — replay em outro tenant é bloqueado.
- Credencial de runtime do Postgres separada da administrativa, com fail-fast se
  forem iguais.
- Permissão de operador relida do banco a cada requisição — revogar vale já.
- Idempotência **já implementada** em pagamento de crediário e em nota fiscal.
- O fiscal distingue falha de conexão de resultado incerto.
- Headers de segurança e CSP na API e no nginx; erros com `traceId`.
- Testes preparados para PostgreSQL real, não SQLite.

## Ordem sugerida de ataque

1. `RES-003` — os três guardrails, num commit só. É o mais barato e fecha uma
   classe inteira.
2. `RES-001` — idempotência da venda, copiando o padrão de `PagamentosCrediario`.
3. `RES-002` — concorrência do crediário, mais índice único parcial.
4. `RES-004` — isolar tenant com migration falha.
5. `AUTH-001` e `AUTH-002` — invalidação de sessão, trava de conta, 2FA no
   `PlatformOwner`.
6. `RES-006`, `RES-007`, `RES-005` — backup dos uploads, trava de deploy,
   interceptor do frontend.

Reorganizar arquivos grandes (`TECH-001`/`TECH-002` no backlog) vem **depois** —
refatorar não deixa venda idempotente.
