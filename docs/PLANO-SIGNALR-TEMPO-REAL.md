# Plano — tempo real (SignalR) não entrega eventos fora da tenant-zero

**Status:** **causa CONFIRMADA em produção** (evidência abaixo). Correção não
implementada.
**Sintoma:** no painel de comandas o cliente abre comanda pelo QR code ou altera
itens e nada aparece para o admin sem F5 ou sem forçar pelo botão.

Este documento é só o plano. Nada aqui foi alterado no código.

---

## A causa

Os grupos do SignalR são nomeados por tenant:

```
Tenant_{tenantId}_Admin
Tenant_{tenantId}_User_{userId}
Tenant_{tenantId}_Comanda_{comandaId}
```

Quem **entra** no grupo é o `ComandaHub`, que lê o `tenantId` do `ITenantContext`
injetado no construtor. Quem **emite** é o `ComandaService`, dentro de uma
requisição HTTP normal, que lê o `tenantId` do `ITenantContext` daquela requisição.

`ITenantContext` é *scoped*, e o **SignalR cria um escopo de DI próprio por
invocação de hub** — não herda o escopo da requisição HTTP. O
`TenantResolutionMiddleware` roda no handshake, mas o escopo dele morre ali.
Dentro do hub, o contexto fica no valor padrão, que é a tenant-zero
(`Guid.Empty` / schema `public`), como está documentado no próprio
`ITenantContext.cs`.

Ou seja: o admin de `loja.dominio` entra em
`Tenant_00000000000000000000000000000000_Admin`, enquanto os eventos daquela loja
são enviados para `Tenant_{idRealDaLoja}_Admin`. **Os grupos nunca se encontram.**

### Dois sintomas, um só defeito

| Quem conecta | O que acontece |
| --- | --- |
| **Admin / Operator** | `OnConnectedAsync` só chama `AddToGroupAsync`, não toca no banco. A conexão sobe, o badge mostra "Conectado", e ele entra no grupo errado. Conecta e fica surdo, sem nenhum erro. |
| **Cliente (QR code)** | `OnConnectedAsync` chama `GetActiveComandaIdByUserAsync`, que abre conexão com o banco. Nesse escopo o `Set()` nunca foi chamado, e o `TenantConnectionInterceptor` tem fail-fast pra isso — lança `InvalidOperationException` e o SignalR aborta a conexão. |

### Por que passou batido

Em desenvolvimento tudo roda na tenant-zero, onde `Guid.Empty` casa com
`Guid.Empty` e o tempo real funciona normalmente. O defeito só aparece em
subdomínio de loja real.

E isto **funcionava antes**: o comentário no próprio hub conta que o grupo era uma
constante única (`"AdminDashboard"`) compartilhada por todos os tenants. Ela foi
escopada por tenant para estancar um vazamento cross-tenant — correção certa, que
passou a depender de um `tenantId` que o hub não tem como saber.

O único teste existente, `ComandaHubTenantGroupTests`, verifica apenas que a
função de montar o nome do grupo gera strings diferentes para tenants diferentes.
Nunca exercita o hub nem checa em qual grupo alguém realmente entrou.

---

## Confirmação em produção

Um cliente abriu comanda pelo QR code numa loja real. O log da API:

```
info: CardGameStore.Hubs.ComandaHub[0]
      Usuário 924f3b9d-… (Customer) conectado ao ComandaHub
System.InvalidOperationException: ITenantContext.Set(...) nunca foi chamado neste
escopo antes de abrir uma conexão — provável bug de propagação de tenant
(CreateScope() sem Set() antes de resolver o AppDbContext).
   at CardGameStore.Hubs.ComandaHub.OnConnectedAsync() in /src/Hubs/ComandaHub.cs:line 69
```

A linha 69 é exatamente o `GetActiveComandaIdByUserAsync` do ramo do Customer —
o primeiro ponto do `OnConnectedAsync` que toca o banco. **Toda conexão de cliente
estoura**, de forma reproduzível: o padrão se repete em todas as conexões de
Customer da janela, sempre no mesmo lugar.

E o contraste fecha o outro lado do diagnóstico: nas mesmas linhas de log, as
conexões de **Admin aparecem sem exceção nenhuma**. É o previsto — o ramo do Admin
não toca o banco, então nada falha; ele entra calado no grupo da tenant-zero e
deixa de receber os eventos da própria loja.

Detalhe menor: a exceção aparece três vezes por conexão. É a política de retry do
Npgsql (`EnableRetryOnFailure`) tentando de novo antes de desistir — não são três
clientes distintos.

---

## Correção proposta

### Fase 1 — propagar o tenant para o escopo do hub

O `HubCallerContext.GetHttpContext()` continua acessível durante toda a vida da
conexão e devolve o `HttpContext` do handshake — que é onde o
`TenantResolutionMiddleware` já resolveu o tenant. O escopo de DI daquela
requisição morre, mas os dados não precisam morrer junto.

Desenho sugerido, em duas peças pequenas:

1. **O `TenantResolutionMiddleware` guarda o tenant resolvido em
   `HttpContext.Items`** (id, schema e módulos), além de popular o `ITenantContext`
   como já faz hoje. Custo zero para o caminho HTTP normal.
2. **Um `IHubFilter`** lê esses valores do `GetHttpContext()` e chama
   `ITenantContext.Set(...)` no escopo da invocação, antes de qualquer método do
   hub rodar. `IHubFilter` cobre `OnConnectedAsync` e `OnDisconnectedAsync` além
   das invocações normais, então o caminho do cliente (que toca o banco no
   connect) fica coberto também.

Por que não as alternativas:

- **Ler o `tenant_id` do JWT dentro do hub** resolveria o nome do grupo, mas a
  fonte da verdade do sistema é o Host, não o token — e tokens antigos, sem a
  claim, caem em tenant-zero silenciosamente. Ficaria uma segunda fonte de verdade
  divergindo da primeira.
- **Chamar `Set()` manualmente no início de cada método do hub** funciona, mas é
  disciplina que o próximo método novo esquece. O filtro é estrutural.

### Fase 2 — impedir a regressão

O teste que existe não pegaria esse bug nem se ele piorasse. Precisa de um que
exercite o hub de verdade com um tenant **diferente** de `Guid.Empty` e afirme:

- admin conectado de um tenant real entra em `Tenant_{idReal}_Admin`, não no grupo
  da tenant-zero;
- cliente conectado de um tenant real completa o `OnConnectedAsync` sem exceção;
- evento emitido pelo `ComandaService` no tenant A não chega em quem está
  conectado no tenant B (a proteção que motivou o escopo por tenant continua de pé).

O terceiro é o que garante que a correção não reabre o vazamento cross-tenant que
originou tudo isto.

### Fase 3 — higiene do ciclo de vida no cliente (menor)

Independente do acima, dois pontos merecem revisão em `frontend/lib/signalr.ts` e
na página de comandas:

- `stopHub()` é `async` mas o cleanup do `useEffect` não o aguarda; ele zera
  `connection` e um `startHub()` imediato pode montar conexão nova enquanto a
  anterior ainda está parando.
- O efeito de polling chama `getComandaHub().start()` por fora do efeito
  principal. Com `connection` já zerado, isso constrói uma segunda conexão sem
  ninguém registrar os handlers `hub.on(...)` nela.

Nenhum dos dois explica o sintoma principal, mas os dois produzem conexão viva
sem handler — exatamente o tipo de coisa que faz o problema parecer intermitente
e atrapalha o diagnóstico.

---

## Fora de escopo

O erro **"Erro ao salvar comentário"** visto no mesmo painel ainda não tem
diagnóstico. A rota existe (`PUT /api/comanda/{id}/notes`), então não é 404.
Falta o status da requisição (401, 403 ou 500) para separar sessão de
impersonação expirada, permissão, ou erro de servidor. Não entra neste plano até
haver esse dado.
