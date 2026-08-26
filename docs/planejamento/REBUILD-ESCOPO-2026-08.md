# Escopo do rebuild — decisões de 2026-08-26

> Registro das decisões tomadas em 26/08/2026 sobre pagamentos, entrega,
> multi-CNPJ e a separação Comandas × Restaurante. Segue o padrão do
> `BACKLOG.md`: ID, estado, evidência e critério de conclusão. Documento de
> escopo — nada aqui está implementado até o item mudar de estado.

## RB-01 — Cobrança da mensalidade da plataforma (Asaas)

**Estado:** `VALIDAR` (implementado em `codex/atalhos-manual-inteligente`, falta
smoke test com credencial real) · **Prioridade:** alta · **Executado em:** 2026-08-26

Hoje o Super-admin suspende na mão a loja que não pagou (`PlatformBillingService`
gera a mensalidade, a baixa é manual). Decisão: **Asaas** como gateway da
cobrança B2B — assinatura recorrente com Pix, boleto e cartão numa API só,
régua de cobrança e webhook de confirmação.

Não confundir com RB-02: aqui **quem recebe é a plataforma**. Stripe perde por
preço percentual sobre tudo e por Pix limitado; Mercado Pago não tem régua de
assinatura equivalente.

**Critério de conclusão:** webhook de pagamento confirmado dá baixa em
`TenantCharge` e reativa/suspende o tenant sem intervenção manual.

### Execução — 2026-08-26

Construído **atrás de `IPlatformPaymentGateway`**, e isso foi decisão, não
capricho: a dúvida do 1,99% sobre assinatura em Pix continua aberta, e no plano
Mar ela vale R$ 9,69 contra R$ 1,99 por cobrança. Se a resposta do Asaas for
ruim, trocar por Woovi ou Efí é escrever uma implementação da interface — o
webhook, a baixa idempotente e a régua não mudam uma linha.

Peças entregues:

- **`IPlatformPaymentGateway`** + `AsaasPlatformGateway` (cliente + cobrança,
  `access_token`, sandbox por padrão).
- **`TenantCharge`**: `Gateway`, `ExternalChargeId`, `PaymentUrl`, com índice
  único filtrado `(gateway, external_charge_id)`.
- **`Tenant`**: `BillingCnpj`, `BillingEmail`, `BillingCustomerId`. O catálogo
  não tinha CNPJ nenhum — sem isso o gateway não cria o cliente.
- **`POST /api/webhooks/billing`** — anônimo por necessidade (o gateway não
  carrega JWT nosso), autenticado pelo segredo no header `asaas-access-token`.
- **`AplicarReguaDeCobrancaAsync`** — suspende vencido além da carência
  (7 dias, configurável) e reativa quem quitou.
- **`PlatformBillingBackgroundService`** — roda de 12 em 12 horas.

Decisões que valem registro:

- **`PAYMENT_RECEIVED` e `PAYMENT_CONFIRMED` os dois dão baixa.** Segurar a
  reativação até a liquidação do cartão deixaria cliente adimplente com a loja
  suspensa por semanas.
- **A régua só reativa quem ela mesma suspendeu**, identificado por
  `PaymentStatus.Atrasado`. Sem isso ela reabriria loja desligada à mão por fim
  de contrato ou abuso.
- **Sem gateway configurado, a régua continua rodando.** Suspender inadimplente
  não depende de emitir cobrança automática, e é metade do trabalho manual.
- **`SaveChanges` por cobrança, não em lote.** A chamada ao gateway é
  irreversível: um save único perderia o id externo das cobranças já emitidas se
  a rodada estourasse no meio, e a execução seguinte cobraria tudo de novo.

Evidência: **893 testes passando, zero falhas** (24 novos), build limpo.
Migration `AddPlatformBillingGateway` — toda aditiva e nullable.

### Taxa — resolvido em 2026-08-26

**O 1,99% é de cartão, não de Pix.** A tabela do Asaas separa:

- **Pix e boleto:** R$ 1,99 fixo por cobrança recebida (R$ 0,99 nos 3 primeiros
  meses). **Assinatura em Pix não tem percentual nenhum.**
- **Cartão:** R$ 0,49 + 2,99% à vista, e é aí que entra o **1,99% adicional
  sobre o total** em parcelamento ou assinatura.
- Conta gratuita, sem mensalidade nem taxa de adesão.

Ou seja, a mensalidade do plano Mar cobrada em Pix custa **R$ 1,99, não
R$ 9,69** — o risco que motivou a interface não existe nessa forma de
pagamento. **Asaas fica.** A Woovi sairia ~R$ 1,19 mais barata por cobrança
(R$ 0,80), o que em 100 lojas dá R$ 119/mês — não paga a migração nem a perda
da régua de cobrança e do painel de assinatura.

A interface `IPlatformPaymentGateway` **permanece**: custou pouco, e a única
coisa que ela deixou de ser é urgente.

**Falta pra sair de VALIDAR (só você resolve):**

1. Abrir a conta Asaas e preencher `Billing:Asaas:ApiKey` e `WebhookToken`.
   **Só a plataforma tem conta** — o lojista recebe uma cobrança e paga, sem
   criar cadastro em lugar nenhum.
2. Cadastrar a URL do webhook no painel do Asaas com o mesmo segredo.
3. Preencher `BillingCnpj` e `BillingEmail` dos tenants existentes — sem isso a
   loja cai na lista de pendências do job em vez de ser cobrada.
4. Configurar `Billing:Asaas:BillingType` (o padrão `UNDEFINED` deixa o lojista
   escolher; fixar `PIX` garante a tarifa fixa e evita a de cartão).

## RB-02 — Recebimento das vendas do lojista (multi-PSP)

**Estado:** `PRONTO PARA FAZER` · **Prioridade:** alta

O padrão já decidido está correto e **não muda**: credenciais por tenant em
`IntegrationConfig` (schema do tenant), o dinheiro da venda cai direto na conta
do lojista, a plataforma cria a cobrança e concilia mas não movimenta saldo.

Decisão: tratar o PSP como pluggable pelo campo `Source`, porque o gargalo é
onboarding, não tecnologia:

| Source | Para quem | Fricção de entrada |
|---|---|---|
| `inter` | já implementado | alta — lojista gera `.crt`/`.key` no Inter PJ e sobe |
| `mercadopago` | lojista que já tem conta MP | baixa — OAuth, conta que ele já usa |

O `PLANO-PAGAMENTOS-MULTITENANT-MERCADO-PAGO.md` continua válido e é o próximo
passo natural: aproveita conta existente e não obriga ninguém a abrir PSP novo.

### Descartado nesta rodada: subconta, split e retenção de comissão

Foram avaliados e **ficam fora do escopo**, com motivo registrado para não
serem reabertos por engano:

- **Subconta white label (Asaas):** período regulatório inicial limita a 10
  subcontas de titulares diferentes, R$ 2.000 em cobranças por subconta e até
  60 dias corridos. Inviável como caminho padrão de onboarding.
- **Recebedores (Pagar.me/Stone):** exige `register_information` + `kyc_details`
  com prova de vida biométrica, conduzida por QR Code dentro do nosso painel.
  Pior ainda, o recebedor transaciona antes mas **só saca depois do
  credenciamento ativo** — lojista com dinheiro preso vira fila de suporte.
- **Split de pagamento com retenção de comissão na venda:** tecnicamente
  funciona (Asaas divide na liquidação, sem repasse manual), mas cria uma
  operação financeira permanente — estorno reverte o split, receita passa a ser
  reversível, e "cadê meu dinheiro" vira o ticket nº 1.

**Motivo determinante:** a operação é de uma pessoa só. O custo não é escrever
o código, é sustentar o suporte financeiro que ele gera. Nada aqui é rejeição
técnica — é decisão de capacidade, e deve ser reavaliada quando houver equipe.

Consequência: sem comissão sobre a venda, a plataforma **não é recebedora de
nada** e fica fora do arranjo de múltiplos recebedores (Circular BACEN
3.815/2016) e da obrigação de PLD/FT por recebedor (Circular 3.978/20).

**Critério de conclusão:** um lojista conecta recebimento por Inter ou Mercado
Pago e emite cobrança Pix sem passo técnico fora do painel.

## RB-03 — Módulo de entrega ("estilo iFood", sem motorista)

**Estado:** `PRONTO PARA FAZER` · **Prioridade:** média

A plataforma **não cobra pela entrega e não terá cadastro de motorista**. A taxa
de entrega é do lojista, integral. Consequência de arquitetura: não há split de
três pernas, não há repasse a entregador e não é preciso marketplace de
pagamento — um recebedor por pedido, que é o cenário mais simples possível.

Decisão de modelagem: **estender `Comanda` com um campo `Canal`
(Mesa | Balcao | Entrega | Retirada)** e um objeto 1:1 com endereço, taxa e
status de entrega — em vez de criar um pipeline `Pedido` paralelo.

Motivo: `NotaFiscalEmitida` e `PixCobranca` já são multi-origem com FK opcional
para `ComandaId`, e `RestaurantProductionArea` já está ligada a comandas. Um
`Pedido` novo obrigaria a refazer emissão fiscal, cobrança Pix e produção.

Fluxo de status sem motorista:
`Recebido → Aceito → EmPreparo → Pronto → Despachado → Entregue | Cancelado`,
onde *Despachado* é o lojista registrando que saiu com o entregador dele. Campo
texto opcional de entregador; sem tabela de motorista.

Notificação de pedido novo reusa `PushService` + `PushSubscription`.

### Monetização do módulo

**Decisão: cobrar pelo módulo, não pela venda.** `"entrega"` entra como mais um
item de `Tenant.EnabledModules`, gateado por `[RequireModule("entrega")]` — o
mesmo mecanismo já usado por `fiscal`, `estoque`, `pontos` e `ia`. Precificação
via plano. **Zero infraestrutura nova de pagamento.**

Foi avaliado cobrar 5% sobre o pedido de entrega, retido na venda via split, e
está descartado pelo motivo registrado no RB-02 (operação de uma pessoa).

**Mas gravar o dado desde o primeiro dia:** com `Canal = Entrega` na `Comanda`,
o GMV de delivery por tenant fica apurável pelo próprio sistema. Isso mantém a
porta aberta — se um dia houver equipe para sustentar cobrança variável, ela
vira mudança de precificação (apura e joga na fatura do `PlatformBillingService`),
não refatoração. **Não construir a cobrança agora; não perder o dado agora.**

**Ponto aberto (verificar antes de modelar):** taxa de entrega na NFC-e não é
trivial — frete tem tratamento próprio no layout e afeta base de cálculo.
Decidir se entra como item ou como frete olhando `NfceEmissionService`.

**Critério de conclusão:** pedido de entrega entra pelo mesmo fechamento da
comanda, emite NFC-e correta e aparece no financeiro do tenant.

## RB-04 — Multi-CNPJ: qual empresa emite a nota

**Estado:** `PRONTO PARA FAZER` · **Prioridade:** alta · **Custo:** o maior dos quatro

Lojista com dois CNPJs (ex.: salão e delivery, ou regimes diferentes) precisa
configurar **qual CNPJ emite a nota de cada operação**, incluindo o CNPJ de
entrega.

O bloqueio é que `FiscalConfig` é hoje um *singleton lógico* — "uma única linha
representa a empresa emitente" — e `FiscalConfig.SingletonId` aparece em
**59 pontos** do código fora de migrações: `FiscalController`,
`ContadorPortalController`, `ContasReceberController`,
`IntegrationServicesController`, `AlertaFiscalService`,
`ApuracaoTributariaService`, `FiscalAlertBackgroundService`,
`FiscalConfigService` e `NfceEmissionService`.

Escopo mínimo:

1. `FiscalConfig` deixa de ser singleton e vira coleção de emitentes, com um
   marcado como padrão. Certificado, série e regime passam a ser por emitente.
2. `NotaFiscalEmitida` ganha FK para o emitente que assinou.
3. Toda operação emissora (comanda, venda avulsa, crediário, **canal de
   entrega** do RB-03) resolve o emitente por regra, não por singleton.
4. Apuração, fechamento fiscal, alertas de certificado e distribuição SEFAZ
   passam a ser segmentados por CNPJ.

**A favor:** `ReservarProximoNumeroNfceAsync(Guid fiscalConfigId)` **já recebe o
id da config** — a numeração por emitente sai de graça assim que parar de
receber `SingletonId` hardcoded. A numeração de NFC-e é por CNPJ + série, então
esse já era o ponto de maior risco e ele está estruturalmente pronto.

**Critério de conclusão:** um tenant com dois emitentes emite NFC-e por ambos,
com numeração e série independentes, e a apuração separa os dois.

## RB-05 — Comandas fora do gate do Restaurante

**Estado:** `VALIDAR` (implementado em `codex/atalhos-manual-inteligente`, fora da
`main`) · **Prioridade:** alta · **Custo:** baixo · **Executado em:** 2026-08-26

Comandas são item de plano base e **não podem sumir porque o módulo Restaurante
não está contratado**. Hoje somem: `ComandaController.cs:35` carrega
`[RequireModule("restaurante")]` na classe inteira, e o próprio comentário de
`Tenant.EnabledModules` conflita os dois ao descrever o módulo como
`"restaurante" (comandas)`.

A camada de permissão **já está certa** e não precisa mexer:
`ComandaController` usa `[RequireOperatorPermission(Permissao.Comandas)]` e
`RestaurantController` usa `Permissao.Restaurante` — são permissões distintas.
O que conflita é só o gate de módulo.

Mudanças:

1. Remover `[RequireModule("restaurante")]` de `ComandaController`. O gate de
   método `[RequireModule("pontos")]` em `ComandaController.cs:257` permanece.
2. `RestaurantController` continua com `[RequireModule("restaurante")]` — o
   módulo passa a gatear só o que é específico de restaurante (áreas de
   produção / KDS).
3. Corrigir o comentário de `Tenant.EnabledModules` para não descrever
   `restaurante` como `(comandas)`.
4. Frontend: `admin/restaurante` vira subpágina de comandas —
   `admin/comanda/restaurante` — e o menu deixa de ter dois itens irmãos.

Sem migração de dados: tenants que hoje têm `"restaurante"` em `EnabledModules`
continuam com tudo; tenants sem o módulo ganham comandas, que é o objetivo.

**Critério de conclusão:** tenant sem `"restaurante"` em `EnabledModules` abre
e fecha comanda normalmente, e não enxerga áreas de produção.

### Execução — 2026-08-26

Dois gates existiam além dos previstos e foram encontrados só na implementação:

- **`ComandaHub.OnConnectedAsync`** recusava a conexão SignalR sem o módulo.
  Sem remover, a tela abriria e nunca atualizaria — pior que o bug original,
  porque o menu anuncia `LIVE`.
- **`AuthService.QuickLoginAsync`** (login do cliente pelo QR Code da mesa).
  Esse **permanece gateado** de propósito: é operação de salão. Só a mensagem
  de erro mudou, que falava em comandas.

Fronteira adotada: **comanda = conta aberta do cliente (plano base); mesa,
QR Code e produção = módulo restaurante.** Por isso `/admin/qrcodes` e
`/mesa/[mesa]` seguem gateados.

Arquivos: `ComandaController.cs`, `ComandaHub.cs`, `Tenant.cs`, `AuthService.cs`,
`RestaurantControllerTests.cs`, `AuthServiceTests.cs`, `comanda/page.tsx`,
`adminNav.ts`, `api.ts`, e `admin/restaurante/page.tsx` → `admin/comanda/restaurante/page.tsx`.

Evidência: build backend limpo, **869 testes passando, zero falhas**; frontend
com `tsc --noEmit` limpo, lint sem avisos e build gerando as rotas
`/admin/comanda` e `/admin/comanda/restaurante`.

**Pendência de negócio (não é código):** `frontend/lib/planos.ts:91` monta o
plano **Mar (R$ 487) excluindo `restaurante`** — era por isso que o cliente mais
caro ficava sem comandas enquanto o Lagoa de R$ 129 tinha. Com o gate removido
o sintoma sumiu, mas a definição do plano continua estranha e é decisão de
preço, não de engenharia. Idem a cópia do site (`institucional/page.tsx`,
`parceiros/page.tsx`), que ainda vende "módulo de restaurante (comandas e
mesas)" — a promessa mudou e o marketing precisa acompanhar.

## Ordem sugerida

Critério de priorização para operação de uma pessoa: **primeiro o que devolve
tempo, depois o que gera receita, por último o que é caro e ainda especulativo.**

1. **RB-05** — remover um atributo. Destrava comandas no plano base hoje.
2. **RB-01** — mata a suspensão e a baixa manual. É o item que **compra tempo
   de volta**, e por isso vem antes de qualquer coisa nova.
3. **RB-03** — módulo de entrega, vendido por `EnabledModules`. Receita nova
   sem operação financeira nova.
4. **RB-04** — multi-CNPJ. **Sob demanda, não especulativo:** são 59 pontos de
   `SingletonId` para uma pessoa refatorar. Só começar quando houver um cliente
   real com dois CNPJs, e aí valendo dinheiro.
5. **RB-02** — Mercado Pago OAuth conforme demanda de lojista.

Se RB-04 entrar antes de RB-03, o canal de entrega já nasce sabendo qual CNPJ
emite. Se entrar depois, o pedido de entrega usa o emitente padrão e ganha a
escolha na migração — aceitável, e provavelmente o caminho realista.
