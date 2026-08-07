# Plano — o que sobrou depois do endurecimento de permissões (#43)

**Status:** verificação concluída, correção não implementada.
**Contexto:** conferência dos problemas de grupos de acesso levantados antes do
[#43](https://github.com/Taino-Edu/Tenant-ERP_Model/pull/43), para saber quais
foram fechados e quais não.

Este documento é só o plano. Nada aqui foi alterado no código.

---

## O #43 resolveu quase tudo

Cinco dos seis problemas apontados foram fechados, e a abordagem é melhor que a
que eu tinha proposto:

| Problema | Situação |
| --- | --- |
| Rotas fora do mapa davam 403 para Operator (`/api/upload`, `/api/fiscal`, `/api/contas-receber`, `/api/eventos`, `/api/reservations`, `/api/timers`, `/api/support`, `/api/export`, `/api/import`, `/api/admin/mensageria`, `/api/notifications`, `/api/site-config`) | **Resolvido** — todas classificadas por atributo |
| `dashboard` liberava `analytics/financeiro` por casamento de prefixo | **Resolvido** — o atributo no método sobrepõe o da classe |
| Prefixos mortos no mapa (`/api/qrcode`, `/api/relatorios/dashboard`, `/api/relatorios/pdv`) | **Resolvido** — o `RotasPrefixo` deixou de existir |
| Revogar permissão só valia no próximo login (claim dentro do JWT) | **Resolvido** — o middleware lê o perfil do banco a cada requisição |
| Nenhum teste cobria o middleware | **Resolvido** — 9 testes, incluindo validador de cobertura de todas as rotas |

O validador de cobertura (`All_mapped_controller_routes_have_an_operator_classification`)
é o ponto mais importante: era exatamente o que faltava para o mapa não envelhecer
de novo. Rota autenticada sem classificação agora quebra a suíte.

---

## O que ficou

### 1. A interface continua com a permissão antiga até o próximo login

Este é o item que vale corrigir, e ele **nasceu do próprio #43** — não existia antes.

O backend parou de confiar no claim do JWT e passou a ler o perfil do banco a cada
requisição, o que faz a revogação valer na hora. Mas o frontend continua decidindo
o que mostrar a partir do cookie `userPermissions`, que é gravado **só no login**,
com validade de 30 dias (`frontend/lib/auth.ts`, `saveAuth`). Os dois lados agora
discordam:

- **Permissão removida:** o item continua no menu e a página continua abrindo; o
  operador clica e leva 403 sem entender por quê.
- **Permissão concedida:** o item não aparece no menu, e o operador continua
  achando que não tem acesso — mesmo já tendo.

A correção é pequena e o dado já está disponível: `POST /api/auth/refresh` devolve
`Permissions` no corpo, mas `doRefresh` (`frontend/lib/api.ts`) descarta a resposta
inteira. Basta persistir as permissões devolvidas no refresh — que acontece pelo
menos a cada hora, quando o access token expira. Para fechar de vez, atualizar
também na carga de `/api/user/me`.

### 2. `qrcodes` não tem proteção no servidor

A permissão existe, aparece na tela de perfis como "QR Codes" e controla o item da
Sidebar e a guarda da página `/admin/qrcodes` — tudo no cliente. Nenhum endpoint a
exige, porque a tela não chama API própria (os QR são gerados no navegador).

Não é vulnerabilidade: não há dado do servidor atrás dela. Mas é bom decidir e
registrar, porque hoje ela parece uma permissão como as outras:

- se a tela for mesmo só cliente, deixar um comentário explícito dizendo que a
  proteção é de navegação, não de acesso a dado; ou
- se algum dia a geração virar endpoint, classificar com `Permissao.QrCodes`.

Todas as outras 17 permissões protegem pelo menos um endpoint.

### 3. Uma consulta a mais por requisição de Operator

Ler o perfil do banco a cada requisição é o que torna a revogação instantânea —
está certo, e é a troca deliberada que o #43 fez. Só vale registrar o custo: toda
requisição de Operator ganhou um `SELECT` extra com join em `perfis`.

Num PDV movimentado isso é uma consulta por clique. Se aparecer nos números, um
cache de 5 a 10 segundos por usuário resolve sem perder o essencial: a revogação
continua praticamente imediata na percepção de quem usa. **Não mexer antes de
medir** — a auditoria de carga (#37) já deixou o ferramental pronto em
`tests/performance/`.

---

## Ordem sugerida

1. **Item 1** — pequeno, corrige incoerência visível para o usuário, sem risco.
2. **Item 2** — decisão e comentário, minutos.
3. **Item 3** — só se a medição pedir.

Nenhum deles bloqueia lançamento.
