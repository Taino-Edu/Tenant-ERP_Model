# Plano de autenticação — MFA/TOTP e login federado (OIDC)

**Status:** proposta, não iniciado.

**Objetivo:** fechar o buraco de segundo fator nas contas cross-tenant (`PlatformOwner` e `Contador`) sem trocar o núcleo de autenticação, e deixar preparado — mas não construído — o caminho de login federado para quando um cliente exigir.

**Decisão recomendada:** implementar TOTP dentro do `AuthService` que já existe. **Não** adotar Keycloak ou outro IdP externo neste momento (justificativa na seção 7).

---

## 1. O que existe hoje

A base de autenticação é sólida e não precisa ser reescrita:

- JWT próprio emitido por `AuthService.GenerateJwt`, entregue em cookie `HttpOnly` (`AuthController.SetAuthCookies:76`).
- Senha em BCrypt; refresh token guardado só como hash SHA-256 (`GenerateAuthResponseAsync`).
- Revogação imediata por `SessionVersion` — `SessionVersionMiddleware` relê o banco a cada requisição e não confia na claim.
- Rate limit `auth` de 15 req/min por tenant+IP (`RequestRateLimits.cs:39`).
- `EncryptionService` com AES-256-GCM já disponível para segredo em repouso.
- `QRCoder` no backend e `qrcode` no frontend — geração de QR já é capacidade instalada.
- Padrão de ticket curto já existente e testado: `LoginRedirectTicket` e `PlatformImpersonationTicket`.

### O buraco

**Não existe segundo fator em lugar nenhum.** Uma busca por `TwoFactor|Totp|MFA` no backend não retorna nada.

O risco não é uniforme entre os perfis:

| Perfil | Onde vive | Alcance se a senha vazar |
|---|---|---|
| `PlatformOwner` | `users` no schema `public` | **toda a plataforma** — todos os tenants, impersonação, dados de clientes de todas as lojas |
| `Contador` | `contador_accounts` (catálogo) | **todas as lojas vinculadas** àquele contador |
| `Admin` | `users` do tenant | uma loja inteira, incluindo financeiro e crediário |
| `Operator` | `users` do tenant | recorte da loja conforme perfil |
| `Customer` | `users` do tenant | a própria comanda/pontos |

As duas primeiras linhas são contas cross-tenant. São elas que justificam a obra.

---

## 2. Princípio que guia o desenho

> Todo caminho que chega numa conta deve passar pela mesma política de MFA daquela conta.

Isso vale para os caminhos que **já existem**, não só para os novos. Hoje chegam em conta autenticada:

- `POST /api/auth/login` — senha
- `POST /api/auth/client-login` — senha do cliente
- `POST /api/auth/setup-account` — primeiro acesso por convite
- `POST /api/auth/quick-login` — CPF + WhatsApp + token de mesa
- `GET  /api/auth/redeem-login` — ticket de redirecionamento
- `POST /api/auth/refresh` — refresh token
- Redeem de impersonação da plataforma

A política precisa ser aplicada **em um ponto só**, não espalhada por sete endpoints — ver seção 3.4.

---

## 3. Fase 1 — TOTP (RFC 6238)

### 3.1 Por que TOTP e não SMS

- Sem custo por mensagem e sem dependência de gateway.
- Imune a SIM swap, que é ataque real e barato no Brasil.
- Funciona offline, no Google Authenticator / Authy / 1Password / Bitwarden que o cliente já tem.
- Implementação é `HMACSHA1(segredo, unixtime / 30)` — a `System.Security.Cryptography` da BCL entrega isso.

**Dependência:** avaliar `Otp.NET` versus implementar à mão. A implementação manual são ~80 linhas e evita mais um pacote; a biblioteca evita erros sutis de Base32 e janela de tolerância. Recomendo a biblioteca para a verificação e o encoding, dado que erro aqui é silencioso e caro. Decisão fica para quem implementar.

### 3.2 Modelo de dados

Duas migrations, porque são dois `DbContext`:

**`AppDbContext`** (afeta `users` em todos os schemas de tenant, e no `public` para o `PlatformOwner`):

| Coluna | Tipo | Nota |
|---|---|---|
| `totp_secret` | `text?` | Base32 do segredo, **criptografado com `EncryptionService`** antes de gravar |
| `totp_enabled_at` | `timestamptz?` | null = não ativado; a coluna dupla como flag e como auditoria |
| `totp_last_step` | `bigint?` | último passo de 30s consumido — anti-replay |
| `totp_failed_attempts` | `int` default 0 | contador de lockout |
| `totp_locked_until` | `timestamptz?` | bloqueio temporário |
| `recovery_codes_json` | `text?` | array de hashes BCrypt dos códigos de recuperação |

**`CatalogDbContext`** (`contador_accounts`): as mesmas seis colunas.

> Atenção ao provisionamento: a migration de `AppDbContext` roda por schema via `TenantDatabaseAdmin.MigrateAsync`. Loja nova nasce com as colunas; lojas existentes recebem no deploy. Nenhuma das colunas é `NOT NULL` sem default, então a migration não trava em base com dados.

### 3.3 Fluxos

**Ativação (usuário já logado):**

1. `POST /api/auth/mfa/setup` → gera 20 bytes aleatórios, devolve o segredo em Base32 e a URI `otpauth://totp/...`.
   - O `issuer` da URI deve identificar a loja: `3esysten (Nome da Loja)`. Sem isso, um contador com cinco lojas vê cinco entradas idênticas no aplicativo autenticador.
   - Nesta etapa o segredo é gravado **criptografado e ainda com `totp_enabled_at = null`**.
2. Frontend renderiza o QR com o `qrcode` que já está no `package.json`.
3. `POST /api/auth/mfa/confirm` com um código válido → só aqui `totp_enabled_at` é preenchido, os 10 códigos de recuperação são gerados e exibidos **uma única vez**, e `SessionVersion++`.

O passo 3 é o que impede alguém travar a própria conta ativando MFA com um segredo que não conseguiu escanear.

**Login com MFA ativo:**

1. `POST /api/auth/login` valida a senha. Se `totp_enabled_at != null`, **não** chama `SetAuthCookies` e **não** emite JWT. Devolve `202` com um `mfaTicket`.
2. O `mfaTicket` é uma linha curta em tabela própria (mesmo padrão de `LoginRedirectTicket`): opaco, hash no banco, validade de 5 minutos, uso único, amarrado ao `userId` e ao tenant. **Não é um JWT e não abre rota nenhuma.**
3. `POST /api/auth/mfa/verify` com `{ mfaTicket, code }`. Sucesso → aí sim `GenerateAuthResponseAsync` + `SetAuthCookies`, fluxo normal.

**Recuperação:** o mesmo endpoint aceita um código de recuperação no lugar dos 6 dígitos. Código consumido é apagado do array. Abaixo de 3 restantes, avisar na UI.

### 3.4 Onde a política mora

Criar `MfaPolicy` (estático, em `CardGameStore/Security/`), espelhando `PlatformAccessProfiles`:

```
PlatformOwner → obrigatório
Contador      → obrigatório
Admin         → opcional na fase 1, obrigatório na fase 3
Operator      → opcional; lojista pode exigir do time
Customer      → nunca
```

E **um único ponto de aplicação**: `GenerateAuthResponseAsync` é por onde todo login passa hoje, sem exceção. A checagem entra ali, não nos controllers. Isso é o que garante o princípio da seção 2 sem auditar sete endpoints.

Ressalvas que precisam de decisão explícita ao implementar:

- **`quick-login` e `client-login`:** `Customer` nunca cai na política, então o fluxo do salão não muda. Mas é preciso um teste travando que um `User` com `Role != Customer` jamais consegue entrar por `quick-login`. Hoje já existe essa checagem (`AuthService.cs:163`); ela passa a ser também controle de MFA.
- **`refresh`:** o refresh token foi emitido depois do MFA, então renovar não deve pedir o código de novo. Mas se o MFA for **ativado** durante uma sessão, o `SessionVersion++` derruba os refresh tokens antigos — comportamento correto e já garantido pelo middleware existente.
- **`redeem-login` e impersonação:** o ticket é emitido por quem já está autenticado, então o MFA já aconteceu antes. Não pedir duas vezes.

### 3.5 Defesas obrigatórias

Sem estes quatro itens o TOTP é teatro:

1. **Anti-replay.** Um código vale 30 segundos e pode ser reusado dentro da janela. Gravar `totp_last_step` e recusar passo menor ou igual ao último aceito.
2. **Janela de tolerância de ±1 passo.** Relógio de celular derrapa. Aceitar só o passo exato gera suporte; aceitar ±2 ou mais amplia a janela de ataque à toa.
3. **Lockout por conta.** O rate limit `auth` atual é por tenant+IP, 15/min — não protege contra tentativa distribuída contra uma conta específica. São 1.000.000 de combinações; a 15/min de vários IPs isso não é seguro o bastante. Regra: 5 falhas → bloqueio de 15 minutos naquela conta, contador zerado no acerto.
4. **Códigos de recuperação em BCrypt, nunca em claro.** Mesmo tratamento da senha.

Somar também: e-mail de aviso ao ativar, ao desativar e ao consumir código de recuperação. `IEmailService` já existe.

### 3.6 Frontend

- `/admin/perfil` (ou equivalente do perfil logado): card "Verificação em duas etapas" — ativar, mostrar QR, confirmar código, listar quantos códigos de recuperação restam, desativar (exigindo senha atual).
- Tela de login: passo 2 com campo de 6 dígitos, `inputMode="numeric"`, `autocomplete="one-time-code"`, e link "usar código de recuperação".
- `frontend/lib/auth.ts` não muda: os metadados só são gravados depois do MFA, porque o `202` não devolve `AuthResponse`.

### 3.7 Testes

Espelhando `AuthServiceTests.cs`:

- código válido entra; código do passo anterior é recusado **na segunda vez** (replay)
- código de passo ±1 entra; de ±2 não
- 5 falhas bloqueiam; acerto no meio zera o contador
- código de recuperação entra uma vez e não entra na segunda
- `mfaTicket` expirado, reusado ou de outro usuário é recusado
- **`PlatformOwner` e `Contador` sem MFA ativo não recebem cookie** — este é o teste que trava a política
- `Customer` por `quick-login` nunca é barrado por MFA

---

## 4. Fase 2 — OIDC (esboço, não priorizado)

### 4.1 Postura recomendada

O sistema **continua sendo o dono da identidade**. O provedor externo vira um *método de login adicional vinculado a uma conta que já existe*, nunca uma fonte de criação de conta. O JWT emitido no fim é o seu, do jeito que é hoje.

Isso preserva `SessionVersion`, permissões relidas do banco, `TenantClaimGuardMiddleware` e todo o modelo multi-tenant — nada disso sobrevive bem a uma migração de emissor.

### 4.2 O problema difícil: `redirect_uri` com wildcard

Google e Entra exigem `redirect_uri` **exata e pré-registrada**. Seus tenants vivem em `<slug>.3esysten.com.br` e em `Tenant.CustomDomain`. É impossível registrar todas.

**Solução, usando peça que já existe:** callback único no domínio raiz, devolvendo ao tenant pelo mecanismo de ticket.

```
1. Usuário em loja.3esysten.com.br clica "Entrar com Google"
2. → 3esysten.com.br/api/auth/oidc/google/start?tenant=loja
       grava state assinado {nonce, tenantSlug, returnUrl}, gera PKCE
3. → accounts.google.com  (redirect_uri = https://3esysten.com.br/api/auth/oidc/callback)
4. → /api/auth/oidc/callback: valida state, troca code por token, valida id_token
5. → emite um LoginRedirectTicket (a classe já existe!)
6. → redireciona para loja.3esysten.com.br/api/auth/redeem-login?ticket=...
7. → RedeemLogin faz o que já faz hoje: SetAuthCookies no host certo
```

O passo 5–7 é código que **já está escrito e em produção**. Este é o argumento mais forte a favor de fazer OIDC dentro do sistema em vez de delegar a um IdP.

### 4.3 Vinculação de conta — onde mora o risco de takeover

Regra dura: **nunca vincular por e-mail sozinho.**

Aceitável:
- Usuário **já logado** vai em "Perfil → Conectar Google" e vincula. Prova posse dos dois lados.
- Convite de operador enviado para `fulano@empresa.com`; o convite carrega o vínculo esperado e o Google devolve `email_verified: true` **e** o mesmo endereço.

Inaceitável:
- Achar um `User` por `email` do `id_token` e logar. Se o provedor não verificou o e-mail, ou se um dia verificar de forma diferente, isso é tomada de conta.

Modelo: tabela `external_logins` (`user_id`, `provider`, `subject`, `linked_at`) — a chave é o `sub` do provedor, que é estável e imutável, **não** o e-mail, que muda.

### 4.4 Implementação

Manual, com `HttpClient` e validação do `id_token` via JWKS — não o handler `Microsoft.AspNetCore.Authentication.OpenIdConnect`. O handler quer o próprio esquema de cookie e a própria máquina de estado, e briga com a resolução de tenant por host. São ~150 linhas contra uma integração que exige contorno em cada ponto.

Obrigatórios: PKCE (S256), `state` assinado com expiração, validação de `iss`/`aud`/`exp`/`nonce`, e o `sub` como chave.

### 4.5 Interação com o MFA

Login externo **não** dispensa a política de MFA das contas obrigatórias. Manter simples: quem é `PlatformOwner` ou `Contador` passa pelo TOTP mesmo entrando pelo Google. Ler `amr` do `id_token` para dispensar é otimização prematura, e cada provedor preenche de um jeito.

### 4.6 Quando fazer

Gatilho, não calendário. Fazer quando ocorrer o primeiro de:

- cliente corporativo exigir SSO (aí pode ser SAML, não OIDC — reavaliar);
- surgir uma segunda aplicação própria que precise de sessão compartilhada;
- suporte medir que reset de senha é volume relevante de chamado.

Até lá, é funcionalidade sem demanda.

---

## 5. Fase 3 — endurecimento (posterior)

- Tornar MFA obrigatório para `Admin`, com prazo de adoção anunciado.
- Lojista poder exigir MFA do time em `/admin/perfis`.
- Listagem de sessões ativas com revogação individual (o `SessionVersion` hoje é tudo-ou-nada).
- `UAParser` já está no projeto: registrar dispositivo e IP hasheado (`Security__IpHashSalt` existe) no histórico de login.

---

## 6. Sequência sugerida

| # | Entrega | Depende de |
|---|---|---|
| 1 | Migrations (App + Catalog) e `MfaPolicy` | — |
| 2 | Geração, cifra e verificação do TOTP + testes de unidade | 1 |
| 3 | `mfaTicket` e o `202` no `login` | 2 |
| 4 | Códigos de recuperação e e-mails de aviso | 2 |
| 5 | Frontend: ativação e passo 2 do login | 3, 4 |
| 6 | Ligar obrigatoriedade para `PlatformOwner` e `Contador` | 5 |
| 7 | OIDC | sem data — ver 4.6 |

O item 6 é o que entrega o valor. De 1 a 5 é encanamento.

---

## 7. Por que não Keycloak

Registrado para não reabrir a discussão sem fato novo:

1. **O modelo de identidade não cabe.** Cinco tipos de sujeito, dos quais `MesaQrToken`, `ApiIntegrationClient`, `McpAccess` e o `Customer` sem senha não têm fluxo equivalente no Keycloak sem *authenticator* customizado em Java. O resultado seria dois sistemas de autenticação, não um.
2. **Provisionamento dinâmico é o pior caso.** Realm por loja acopla `TenantProvisioningService` a um segundo sistema, com falha parcial possível (schema criado, realm não).
3. **Conflita com decisão deliberada do projeto.** Autorização aqui é relida do banco a cada requisição; o Keycloak empurra claims no token válidas até expirar. Os dois middlewares continuariam existindo — só a autenticação sairia, que é justamente a parte que já funciona.
4. **Custo operacional real:** mais um serviço no caminho crítico do login (cai o Keycloak, ninguém entra em loja nenhuma), mais backup, mais TLS, e upgrades de major com histórico de quebra.
5. **A migração não é trocar o emissor:** 708 linhas de `AuthService` e 646 de `AuthController`, mais impersonação, `LoginRedirectTicket`, quick login e o fluxo do contador.

RAM não é o argumento — a VPS tem 32 GB e o stack usa ~7,5.

**Reabrir se:** aparecer exigência de SSO corporativo com SAML, ou três ou mais aplicações próprias precisando de sessão compartilhada.
