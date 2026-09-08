# Escopo do rebuild — decisões de 2026-08-26

> Registro das decisões tomadas em 26/08/2026 sobre pagamentos, entrega,
> multi-CNPJ e a separação Comandas × Restaurante. Segue o padrão do
> `BACKLOG.md`: ID, estado, evidência e critério de conclusão. Documento de
> escopo — nada aqui está implementado até o item mudar de estado.
>
> **Acrescentado em 2026-09-08:** `RB-06`, aplicativos móveis (consumidor,
> motoboy e a entrega como produto próprio). Este documento **manda sobre o
> `BACKLOG.md`** nos temas `RB-01` a `RB-06`; ver a divisão em
> [`STATUS.md`](STATUS.md).

## RB-01 — Cobrança da mensalidade da plataforma (Asaas)

**Estado:** `CONCLUÍDO` — validado ponta a ponta em sandbox no dia 2026-08-31.
Cobrança `pay_5i2z4pbwyan1gvvf` emitida pelo job, paga no Asaas, e baixada
sozinha pelo webhook (`Cobrança 5c89a8db-… baixada por webhook do asaas`).
· **Prioridade:** alta

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

### Validação em sandbox — 2026-08-31

Cinco obstáculos apareceram, e **nenhum deles era detectável pelos testes** —
todos vivem na fronteira com o serviço real. Ficam registrados porque vão se
repetir em produção:

1. **User-Agent obrigatório.** O Asaas responde 400 `user_agent_not_informed`,
   e o `HttpClient` do .NET não manda nenhum por padrão. Corrigido em PR #96.
2. **`$` do início da chave interpolado pelo Docker Compose.** A chave do Asaas
   começa com `$`, e o Compose trata isso como referência de variável — mesmo
   dentro de aspas simples. **Aspas não protegem; o escape é `$$`.** O sintoma
   era `Nenhum gateway de cobrança configurado` com a chave presente no `.env`.
3. **O `.env` canônico é `/opt/tenant-erp/.env`, não `deploy/.env`.** O
   `update.sh` copia o primeiro por cima do segundo (linha 63), então qualquer
   edição feita na cópia some no deploy seguinte, em silêncio.
4. **Piso de R$ 5,00 por cobrança.** Passou a ser checado localmente.
5. **Cliente duplicado a cada retentativa.** Bug de arquitetura: criar o cliente
   dentro da emissão descartava o id quando a cobrança falhava. Corrigido em
   PR #98.

**Configuração de produção (checklist):**

1. `Billing:Asaas:ApiKey` e `WebhookToken` no `/opt/tenant-erp/.env`, com a
   chave escapada como `$$aact_...`. **Só a plataforma tem conta** — o lojista
   recebe uma cobrança e paga, sem criar cadastro em lugar nenhum.
2. Webhook no painel do Asaas com o mesmo segredo, envio **sequencial**, e só os
   seis eventos que o `InterpretarWebhook` trata.
3. `BillingCnpj` e `BillingEmail` de cada tenant — sem isso a loja cai na lista
   de pendências do job em vez de ser cobrada.
4. `BillingType=PIX`. O padrão `UNDEFINED` deixa o lojista escolher cartão, e aí
   a assinatura custa 2,99% + 1,99% em vez da tarifa fixa de R$ 1,99.

**Atenção:** falha repetida de entrega faz o Asaas **penalizar e pausar a fila**
do webhook (~15 tentativas). Depois de corrigir a causa, é preciso religar a
"Fila de sincronização" no painel — ele não retoma sozinho.

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

## RB-03 — Pedidos Online (ex-"módulo de entrega")

**Estado:** `PRONTO PARA FAZER` · **Prioridade:** média
**Especificação:** [`PLANO-MVP-PEDIDOS-ONLINE.md`](PLANO-MVP-PEDIDOS-ONLINE.md)

Consolidado em 2026-08-26. Este item e o `PLANO-MVP-PEDIDOS-ONLINE.md` estavam
descrevendo a mesma funcionalidade em paralelo — **a especificação agora vive lá
e só lá.** Este bloco existe para manter a numeração do rebuild e registrar o
que mudou na fusão:

- **Nome:** "módulo de entrega" → **Pedidos Online**. Melhor porque cobre
  retirada, que é metade do fluxo; "delivery" descreve só uma das formas de
  atendimento. Chave técnica do módulo: `pedidos_online`.
- **Modelo de dados:** a proposta deste RB-03 era estender `Comanda` com um
  campo `Canal`. **Foi descartada** em favor de uma entidade `PedidoOnline`
  própria. O reuso de NFC-e e Pix — que era o motivo de querer a `Comanda` — se
  resolve pelo padrão de **origem múltipla** que `NotaFiscalEmitida` e
  `PixCobranca` já usam, sem sobrecarregar a comanda com endereço, taxa,
  idempotência e histórico de transição.
- **Mantido da versão original:** sem comissão sobre a venda, sem cadastro de
  motorista, GMV gravado desde o primeiro pedido, e a ressalva fiscal do frete
  na NFC-e (seções 1.1, 14.1 e 14.3 do plano consolidado).

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

**Estado:** `CONCLUÍDO` — na `main` desde 2026-08-26 (PR #95, merge `97b52af`).
· **Prioridade:** alta · **Custo:** baixo

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

## RB-06 — Aplicativos móveis: consumidor, motoboy e a entrega como produto

**Estado:** `PRONTO PARA FAZER` · **Prioridade:** média · **Registrado em
2026-09-08**

Vieram pedidos de aplicativo, principalmente para **os clientes dos clientes** e
para **motoboys**, com a ideia de que o app de entrega vire produto próprio
usando o sistema como base de despacho. Distribuição pretendida: **APK baixado
pelo próprio sistema**, porque ainda não há conta de Play Store.

A decisão registrada aqui é: **não é um app, são três públicos com economias
diferentes**, e cada um pede uma resposta técnica diferente.

### 6.1 — Consumidor final: PWA, não APK

**Decisão: o consumidor não recebe APK.** Sideload exige habilitar "fontes
desconhecidas" e atravessar o aviso do Play Protect — fricção alta justamente no
público de menor engajamento, que decide em segundos. E o modelo APK exclui o
iPhone inteiro. O PWA instala pela tela de início no iOS e recebe push (16.4+,
quando instalado).

**O PWA já existe, e a auditoria de 2026-09-08 mostra que ele foi feito para o
lojista.** O que está certo hoje:

- `frontend/app/manifest.ts` é **dinâmico por tenant** — lê o Host, resolve nome
  e ícone da loja, `display: standalone`, `theme_color`, `orientation`, `lang`.
- Fallback de ícone com `purpose: any` e `maskable`.
- iOS declarado em `layout.tsx` (`appleWebApp`, `apple-touch-fullscreen`).
- `beforeinstallprompt` capturado em `components/PWAInstallButton.tsx`, montado
  no layout raiz.
- Web Push VAPID ponta a ponta, e com a ordem certa: busca a chave **antes** de
  pedir permissão, para não queimar a permissão do usuário numa instalação sem
  push configurado.

**Defeitos confirmados para o caso do consumidor:**

1. **O manifest descreve o ERP, não a loja.** `description` é `"Sistema de
   gestão para lojas e varejo"` e os `shortcuts` apontam para
   `/admin/venda-avulsa` e `/admin/dashboard`. Um consumidor que instalar o app
   da padaria recebe atalho para a Frente de Caixa dela.
2. **`start_url: '/'`** — instalando pela área do cliente, o app abre na raiz.
3. **O service worker só trata `push` e `notificationclick`.** Não tem handler
   de `fetch`, não faz cache: abrir sem rede é tela de erro. Para app de pedido,
   isso é ruim.
4. **O SW só é registrado dentro do `NotificationBell`**, e o registro fica
   **depois** do `return` que ocorre quando o servidor não tem VAPID. Instalação
   sem push configurado nunca registra service worker nenhum.
5. **Ícone do iOS é SVG.** `icons.apple` cai em `/icon.svg`; o iOS ignora SVG em
   `apple-touch-icon` e precisa de PNG. Não há **nenhum** PNG de ícone em
   `frontend/public/` além do `logo-octus.png`. Na prática, no iPhone o app
   instala sem a marca.
6. **Nenhum tenant tem ícone de PWA configurado** — o próprio comentário do
   arquivo registra isso. Todo mundo instala com a identidade Octus, não com a
   da loja. É o `PROD-001` do backlog, que deixa de ser cosmético aqui.

**Implementação sugerida:** manifest por público, não por instalação. O manifest
já é rota dinâmica; basta `/cliente/*` apontar (via `generateMetadata`) para um
manifest próprio, com `start_url` na área do cliente, descrição da loja e
atalhos do consumidor, enquanto o admin mantém o atual.

**Critério de conclusão:** um consumidor instala pelo celular da loja X, o app
abre na área do cliente com o nome e o ícone da loja X, funciona sem rede o
suficiente para não mostrar tela de erro, e o iPhone recebe o ícone certo.

### 6.2 — Motoboy: aqui o app nativo se justifica

Localização em background, tela apagada, notificação confiável e sinal ruim. O
PWA faz isso mal no Android e não faz no iOS. E o perfil inverte a lógica do
sideload: são poucos usuários, é ferramenta de trabalho, instalam uma vez. **É
aqui que o APK faz sentido**, não no consumidor.

**Escopo: equipe própria da loja primeiro, não pool compartilhado.** Um app
único, multi-loja, onde o motoboy entra vinculado ao lojista que o cadastrou.
Isso valida a parte difícil — background location, notificação, offline,
bateria — sem abrir marketplace, e mantém o vínculo trabalhista onde ele já
está hoje: com o lojista.

**Conflito de escopo a resolver:** o `RB-03`/`PLANO-MVP-PEDIDOS-ONLINE.md`
registra explicitamente **"sem cadastro de motorista"**. O app de motoboy
contradiz essa decisão e depende de revê-la. Não tratar como "só fazer o app".

**Stack:** Expo/React Native — o time já escreve TypeScript e React, o EAS Build
gera APK sem conta de Play Store, e `expo-location`/`expo-notifications`
resolvem o que o PWA não resolve. Capacitor empacotaria o que existe mais
barato, mas entrega pior justamente no background location, que é o motivo de o
app existir.

**Dependências técnicas:**

- **Tenant:** nenhuma mudança de backend. O tenant é resolvido por Host
  (`TenantResolutionMiddleware`), então o app apontando para
  `https://<slug>.3esysten.com.br/api/...` é resolvido igual ao navegador. **Não
  criar header de tenant** — é exatamente o buraco fechado de propósito nas
  tools de IA.
- **Sessão:** app não usa cookie `HttpOnly` do mesmo jeito; precisa de token em
  armazenamento seguro (Keystore/Keychain). Isso agrava o `AUTH-001` da
  auditoria de resiliência — logout não invalida o access token — que hoje é
  incômodo e ali passa a ser problema.
- **Atualização:** APK não se atualiza sozinho. Precisa de checagem de versão
  contra a API e tela de "atualize", senão em poucos meses há várias versões em
  campo sem forma de saber qual.
- **A verificar antes de apostar em APK:** o Google vem apertando a exigência de
  verificação de desenvolvedor para apps sideloaded em dispositivos Android
  certificados. Confirmar o estado atual da regra **antes** de fazer do APK a
  única via de distribuição, e ter plano B desde o início.

**Critério de conclusão:** motoboy de uma loja piloto recebe corrida, navega,
atualiza status com o app em segundo plano e a tela apagada, e o lojista vê a
posição no painel — com o APK distribuído e atualizável pelo próprio sistema.

### 6.3 — Entrega como produto próprio: adiado, com porta aberta

A ideia de um app de entrega geral, usando o sistema como base de despacho, fica
**registrada e adiada** — não descartada. As razões são de tipo de negócio e de
momento, não da ideia:

- Vira **marketplace de dois lados**: aquisição, suporte e margem diferentes de
  vender licença de ERP. Dois negócios com um time.
- **Risco trabalhista:** vender ERP não expõe a discussão de vínculo com
  entregador; operar entrega expõe.
- **A infra não está pronta:** uma VPS com tudo junto, backup que não cobre
  uploads (`RES-006`), sem monitor externo (`OPS-001`) e sem HA (`INFRA-001`).
  Rastreamento ao vivo é escrita contínua de posição mais conexão persistente,
  no mesmo box do SignalR das comandas.
- **Custo de oportunidade:** `RB-02` e `RB-04` destravam receita em clientes que
  já pagam. E `RB-02` é dependência do app do consumidor — pedido sem pagamento
  online fica capenga.

**Gatilho para reabrir:** o app de motoboy (6.2) rodando em cliente real, com
volume que justifique compartilhar entregador entre lojas. Nessa altura, virar
pool compartilhado é incremento, não reescrita.

### Ordem interna do RB-06

1. **6.1 (PWA do consumidor)** — dias, reusa tudo o que já existe.
2. **6.2 (motoboy)** — depois do `RB-02`, e depois de revisar a decisão de "sem
   cadastro de motorista" do `RB-03`.
3. **6.3 (entrega como produto)** — só com o gatilho acima cumprido.

## Ordem sugerida

Critério de priorização para operação de uma pessoa: **primeiro o que devolve
tempo, depois o que gera receita, por último o que é caro e ainda especulativo.**

1. **RB-05** — remover um atributo. Destrava comandas no plano base hoje.
2. **RB-01** — mata a suspensão e a baixa manual. É o item que **compra tempo
   de volta**, e por isso vem antes de qualquer coisa nova.
3. **RB-03** — Pedidos Online, vendido por `EnabledModules`. Receita nova sem
   operação financeira nova. Ver `PLANO-MVP-PEDIDOS-ONLINE.md`, que já traz o
   faseamento próprio (Entregas 0 a 6).
4. **RB-04** — multi-CNPJ. **Sob demanda, não especulativo:** são 59 pontos de
   `SingletonId` para uma pessoa refatorar. Só começar quando houver um cliente
   real com dois CNPJs, e aí valendo dinheiro.
5. **RB-02** — recebimento das vendas do lojista (multi-PSP), conforme demanda.
6. **RB-06** — aplicativos móveis. **A parte 6.1 (PWA do consumidor) fura essa
   fila** e pode entrar a qualquer momento: é barata, reusa o que já existe e
   não depende de nenhum dos outros. As partes 6.2 e 6.3 seguem a ordem própria
   registrada no item.

Se RB-04 entrar antes de RB-03, o pedido online já nasce sabendo qual CNPJ
emite. Se entrar depois, ele usa o emitente único do tenant e ganha a escolha na
migração — aceitável, e provavelmente o caminho realista, **desde que o piloto
seja feito numa loja com um CNPJ só** (ver seção 14.3 do plano consolidado).
