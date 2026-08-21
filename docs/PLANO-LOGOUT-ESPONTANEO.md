# Plano — logout espontâneo da sessão

**Status:** investigação concluída, correção não implementada.
**Origem:** relato de usuários sendo deslogados sozinhos do painel.

Este documento é só o plano. Nada aqui foi alterado no código ainda.

---

## Resumo

O sistema guarda **um único refresh token por usuário**, numa coluna da própria
linha (`users.refresh_token`). Todo login e todo refresh sobrescrevem esse valor.
Não existe janela de tolerância para o token anterior, e quando um refresh falha
o servidor **apaga os cookies de autenticação do navegador**.

Dessas três decisões juntas saem três caminhos distintos que derrubam a sessão de
alguém que não fez nada de errado. O mais provável no uso diário é a corrida
entre abas.

**Não vem do sistema de permissões.** O `OperatorPermissionMiddleware` devolve
**403**, e o interceptor do frontend só reage a **401**
([frontend/lib/api.ts:60](../frontend/lib/api.ts)). Falta de permissão mostra
erro na tela e para por aí — não desloga. Essa hipótese foi levantada e
descartada.

---

## O mecanismo

O access token vive 60 minutos em produção, e o cookie que o carrega tem
`MaxAge` igual a isso — passado o prazo o próprio navegador o descarta. A partir
daí toda requisição sai sem credencial, toma 401, e o interceptor tenta renovar.
É o fluxo desenhado e funciona. O problema é o que acontece nas bordas dele.

### Caminho 1 — corrida entre abas (mais provável)

O frontend serializa refreshes concorrentes com um mutex, mas ele é uma variável
de módulo: vale **dentro de uma aba**, não entre abas. Cookies são compartilhados
por todas elas.

1. Passa a hora. As abas A e B disparam requisições, ambas tomam 401.
2. As duas chamam `POST /api/auth/refresh` com o mesmo token `R1`.
3. A requisição de A chega primeiro, é aceita e **rotaciona** `R1` → `R2`.
4. A requisição de B chega com `R1`, que já não existe mais no banco → 401.
5. `AuthController.Refresh` trata a falha chamando `ClearAuthCookies()`, que
   manda o navegador **apagar `accessToken` e `refreshToken`**.
6. Esse `Set-Cookie` de exclusão apaga o `R2` que a aba A tinha acabado de
   receber.

Resultado: **as duas abas caem**. A aba B redireciona para `/login`
imediatamente; a aba A morre na requisição seguinte. É por isso que o sintoma
parece "me deslogou de tudo ao mesmo tempo" e não "uma aba bugou".

Isso reproduz de forma determinística com duas abas do painel abertas por mais
de uma hora.

### Caminho 2 — segundo login derruba o primeiro

Como é uma coluna só por usuário, entrar em outro dispositivo sobrescreve o
refresh token do primeiro. O aparelho antigo continua funcionando até o access
token expirar e então é mandado para o login.

Padrão do relato: "entrei no celular e o computador me deslogou". Também explica
o efeito cascata depois do Caminho 1 — a pessoa loga de novo, gera `R3`, e a
outra aba que ainda tinha `R2` cai na sequência.

### Caminho 3 — rate limit compartilhado por NAT

`POST /api/auth/refresh` usa a política `auth`, de 15 requisições por minuto
**por IP** ([CardGameStore/Program.cs](../CardGameStore/Program.cs)), sem fila
(`QueueLimit = 0`), e divide esse balde com o login.

Numa loja, todos os terminais saem pelo mesmo IP público. Vários operadores com
abas abertas renovando perto do mesmo horário passam de 15/min com facilidade. O
429 cai no mesmo `catch` do interceptor que trata falha de refresh — e o usuário
é deslogado.

Este caminho é o mais fácil de descartar ou confirmar: ou aparece no log, ou não.

---

## Evidência no código

| Ponto | Arquivo |
| --- | --- |
| Refresh token é coluna única, sobrescrita a cada emissão | `AuthService.GenerateAuthResponseAsync` |
| Rotação sem tolerância ao token anterior | `AuthService.RefreshTokenAsync` |
| Falha de refresh apaga os cookies do navegador | `AuthController.Refresh` → `ClearAuthCookies` |
| Mutex de refresh é por aba | `frontend/lib/api.ts` |
| `auth` = 15/min por IP, sem fila, dividido com login | `CardGameStore/Program.cs` |

---

## Como confirmar antes de corrigir

Cada falha de refresh já deixa rastro:

```bash
cd /opt/tenant-erp/deploy
docker compose -f docker-compose.prod.yml logs api --since 48h \
  | grep -c "Refresh token inválido ou expirado"
```

- **Contagem alta e espalhada pelo dia** → Caminhos 1 e 2 confirmados.
- **Rajadas em horários de pico** → reforça o Caminho 1 (várias abas
  expirando juntas).
- Para o Caminho 3, procurar 429 no `access.log` do nginx em `/api/auth/refresh`.

Reprodução direta do Caminho 1: abrir o painel em duas abas, esperar passar de 60
minutos, mexer nas duas. Se ambas caírem, está fechado.

**O que descartaria tudo isto:** logout acontecendo em poucos minutos, com uma
aba só e sem login em outro lugar. Nesse caso a investigação vai para cookie
(`COOKIE_SECURE` ligado sem HTTPS ponta a ponta) ou para o `TenantClaimGuard`,
que devolve 401 quando o `tenant_id` do token não bate com o host — cenário de
quem alterna entre subdomínios de lojas diferentes.

---

## Correção proposta

Em duas fases, porque a primeira já resolve o sintoma dominante com risco baixo,
e a segunda é a mudança estrutural.

### Fase 1 — parar de derrubar sessão válida

Três ajustes pequenos, independentes entre si:

1. **Janela de graça para o token anterior.** Guardar o hash do refresh token
   anterior e aceitá-lo por ~60 segundos após a rotação, devolvendo o token
   corrente em vez de erro. Mata a corrida entre abas sem afetar segurança de
   forma relevante: a janela é curta e o token continua de uso único na prática.
2. **Não apagar cookie em falha de refresh concorrente.** O `ClearAuthCookies()`
   faz sentido quando o refresh token realmente expirou; não faz quando ele só
   perdeu a corrida. Com a janela de graça acima, esse caso deixa de existir —
   mas vale distinguir os dois erros mesmo assim, para nunca mais uma resposta
   apagar credencial que outra requisição acabou de renovar.
3. **Tirar o refresh do balde do login.** Política de rate limit própria, com
   limite mais alto, e idealmente por usuário em vez de por IP — senão uma loja
   inteira atrás de um NAT compartilha o mesmo teto.

Custo estimado: uma migration (coluna do hash anterior + carimbo de rotação) e
alterações localizadas em `AuthService`, `AuthController` e `Program.cs`.

### Fase 2 — sessões por dispositivo

Trocar a coluna única por uma tabela de sessões (`user_id`, hash, expiração,
user-agent, último uso). Resolve o Caminho 2 de vez, permite "sair de todos os
dispositivos" e dá visibilidade de sessões ativas — coisa que hoje não existe.

Maior, e só faz sentido depois que a Fase 1 estabilizar o sintoma.

### Testes

Nenhum teste cobre esses caminhos hoje. O mínimo antes de mexer:

- refresh concorrente com o mesmo token → os dois lados continuam autenticados;
- refresh com token de verdade expirado → 401 **e** cookies limpos (o
  comportamento atual, que deve ser preservado);
- login em segunda sessão → primeira continua válida (só depois da Fase 2).

---

## Fora de escopo

O `OperatorPermissionMiddleware` também tem problemas reais — mapa de rotas
defasado, prefixo fazendo `dashboard` liberar `analytics/financeiro`, permissão
`qrcodes` apontando para rota inexistente. **Não entram aqui**: já há refatoração
em andamento migrando o mapa para atributos `[RequireOperatorPermission]` nos
controllers. Este plano trata só de sessão.
