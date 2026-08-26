# Plano do MVP — Pedidos Online do Octus

> **Estado:** proposta pronta para implementação  
> **Data:** 2026-08-26  
> **Nome comercial provisório:** Pedidos Online  
> **Chave técnica do módulo:** `pedidos_online`

> **Documento único (consolidado em 2026-08-26).** Este plano e o item RB-03 de
> `REBUILD-ESCOPO-2026-08.md` descreviam a mesma funcionalidade em paralelo. Os
> dois foram fundidos aqui; o RB-03 virou ponteiro e não deve mais ser lido como
> especificação. Toda decisão sobre Pedidos Online mora neste arquivo.
>
> **Divergência resolvida — modelo de dados.** O RB-03 propunha estender
> `Comanda` com um campo `Canal` em vez de criar `PedidoOnline`, para herdar
> NFC-e e `PixCobranca`. **Prevaleceu a entidade própria**, por dois motivos:
>
> 1. O reaproveitamento não dependia disso. O padrão de reuso desta base é a
>    **origem múltipla** — `NotaFiscalEmitida` e `PixCobranca` já a usam com FK
>    opcional. Acrescentar `PedidoOnline` ao enum de origem (seção 9.2) entrega
>    o mesmo reuso sem sobrecarregar nada.
> 2. `Comanda` não tem endereço, taxa de entrega, status de pagamento separado
>    do operacional, chave de idempotência nem histórico de transição. Enfiar
>    tudo isso lá pioraria a comanda no trabalho que ela já faz bem.

## 1. Decisão de produto

O Octus oferecerá a cada comércio um canal próprio de pedidos, integrado ao ERP,
sem reter comissão sobre produtos, frete ou gorjeta. O estabelecimento continuará
responsável pela entrega e poderá usar funcionário, motoboy próprio ou prestador
contratado fora da plataforma.

O MVP não é um marketplace e não fornece uma frota. Ele é a infraestrutura para o
comércio vender pelo próprio link, domínio ou QR Code.

### Proposta de valor

> Receba pedidos pelo seu próprio endereço, acompanhe a operação dentro do Octus e
> preserve o valor da venda. O Octus cobra a mensalidade do sistema, não uma fatia
> de cada pedido.

### Nome do recurso

Usar **Pedidos Online** na interface. “Delivery” descreve apenas uma das formas de
atendimento, pois o cliente também poderá escolher retirada. O nome continua útil
para varejo, lojas de jogos, restaurantes, mercados e outros segmentos.

### 1.1 Por que sem comissão — o que o mercado mostra

A decisão de não reter percentual não é modéstia comercial, é a única posição
sustentável para esta operação. O mercado brasileiro está partido em dois grupos
com estruturas incompatíveis (pesquisa de 2026-08-26):

**Grupo A — marketplaces, que retêm o dinheiro:**

| | Comissão | Pagamento online | Infraestrutura |
|---|---|---|---|
| iFood | 12–27% | — | **Instituição de pagamento própria** (iFood Pago, autorizada pelo BACEN em 31/10/2023) |
| Aiqfome | ~12% | +2,99% | Repasse semanal, infra do grupo Magalu |
| Delivery Much | 12–21% | +3,5% | Saldo retido na plataforma para saque; franquia regional |

Todos são recebedores de registro, e todos vendem **demanda**: o comércio paga
pedágio sobre um cliente que a plataforma trouxe. É isso que sustenta 12% a 27%,
e o preço de entrada é uma instituição financeira regulada.

**Grupo B — canal próprio, que não toca no dinheiro:** Goomer, Anota AI,
Cardápio Web. Mensalidade, zero comissão sobre pedido, e o argumento comercial
já validado no mercado: *"pare de pagar 12% ao iFood"*.

**O Octus é grupo B**, e a consequência técnica é o que torna este MVP viável
para uma equipe pequena: sem retenção não há subconta, não há KYC por lojista,
não há chargeback como prejuízo nosso, e a plataforma fica fora do arranjo de
múltiplos recebedores (Circular BACEN 3.815/2016) e da obrigação de PLD/FT por
recebedor (Circular 3.978/20). Ver o descarte registrado no RB-02 de
`REBUILD-ESCOPO-2026-08.md`.

**A vantagem defensável sobre o grupo B:** Goomer e Anota AI são cardápio
digital que *integra* com PDV, estoque e fiscal de terceiros. O Octus **é** o
PDV, o estoque, o fiscal e o financeiro. O pedido cair direto no financeiro e
virar NFC-e sem integração nenhuma é o que eles estruturalmente não entregam —
e é aí que este MVP deve apostar, não em superá-los em recursos de cardápio.

## 2. Objetivo do MVP

Permitir que um cliente:

1. acesse a página pública da loja;
2. adicione produtos ao carrinho;
3. escolha retirada ou entrega;
4. informe ou confirme seus dados;
5. escolha a forma de pagamento;
6. envie o pedido;
7. acompanhe o andamento em tempo real.

Permitir que o comércio:

1. configure quando e onde atende;
2. receba novos pedidos em tempo real;
3. aceite ou recuse cada pedido;
4. conduza o pedido até a conclusão;
5. mantenha estoque, financeiro, Pix e histórico coerentes;
6. veja indicadores básicos da operação.

## 3. Fora do MVP

Os itens abaixo ficam deliberadamente para fases posteriores:

- aplicativo nativo do motoboy;
- rastreamento contínuo por GPS;
- mapa operacional e geocodificação automática;
- cálculo de rota e previsão baseada em trânsito;
- distribuição automática de corridas;
- agrupamento de entregas próximas;
- frota ou rede de entregadores fornecida pelo Octus;
- marketplace regional com descoberta de estabelecimentos;
- chat em tempo real entre cliente e entregador;
- avaliações públicas e gorjetas;
- integração com maquininhas na entrega.

O modelo de dados deve permitir essas evoluções, mas o MVP não deve carregar o
custo nem a complexidade delas antes da validação com uma loja piloto.

## 4. O que já existe e será reaproveitado

| Capacidade | Base atual | Uso no MVP |
|---|---|---|
| Multi-tenant | schema PostgreSQL por loja | isolamento completo dos pedidos |
| Planos e módulos | `Tenant.EnabledModules` e `RequireModule` | liberar `pedidos_online` por assinatura |
| Catálogo público | `ProductController` e `ProductPublicDto` | vitrine e carrinho |
| Produto e variante | `Product` e `ProductVariant` | seleção e baixa de estoque |
| Cliente | `User` e login rápido existente | identidade e acesso ao próprio pedido |
| Comandas | serviços, snapshots e transações | referência para regras de estoque e fechamento |
| Restaurante | áreas e estados de produção | integrar itens à cozinha quando aplicável |
| Pix Inter | `PixCobranca` | cobrança do pedido após extensão da origem |
| SignalR | `ComandaHub` e grupos por tenant | padrão para um hub próprio de pedidos |
| Push web | `PushSubscription` e `PushService` | avisos de mudança de status |
| Financeiro e fiscal | vendas, NFC-e e relatórios | registrar a conclusão sem duplicar receita |
| Site e PWA | identidade visual, domínio e manifest | experiência instalável sem app nativo |
| Auditoria | `AuditService` | mudanças de status e ações administrativas |

## 5. Fluxos principais

### 5.1 Cliente — entrega

```text
Vitrine -> Carrinho -> Identificação -> Endereço -> Pagamento
        -> Revisão -> Pedido enviado -> Acompanhamento
```

1. O cliente abre `/pedidos` no domínio ou subdomínio da loja.
2. O catálogo exibe apenas produtos ativos, públicos e com disponibilidade.
3. O carrinho fica local até a confirmação; o servidor recalcula tudo no checkout.
4. O cliente usa o login rápido existente ou confirma seus dados.
5. Escolhe “Entrega”, informa endereço, referência e, quando necessário, troco.
6. O backend identifica a zona atendida e calcula a taxa.
7. O cliente escolhe Pix, dinheiro ou cartão na entrega.
8. O pedido recebe um número curto e entra como `Novo`.
9. A loja aceita ou recusa. Em caso de aceite, informa a estimativa inicial.
10. O cliente acompanha as mudanças de status por SignalR, com fallback por
    atualização periódica.

### 5.2 Cliente — retirada

O fluxo é igual, mas não exige endereço nem taxa de entrega. O pedido passa por
`Novo -> Confirmado -> EmPreparo -> ProntoParaRetirada -> Concluido`.

### 5.3 Comércio

```text
Novo -> Confirmado -> Em preparo -> Pronto -> Saiu para entrega -> Concluído
   \-> Recusado          \-> Cancelado
```

1. Um aviso sonoro, visual e push informa a chegada do pedido.
2. O operador abre o cartão, confere pagamento, endereço, itens e observações.
3. Ao aceitar, informa uma previsão simples em minutos.
4. Itens com área de produção aparecem também na fila do restaurante.
5. O operador avança os estados conforme a operação.
6. Em “Saiu para entrega”, pode registrar apenas o nome do responsável no MVP.
7. Em “Concluído”, o sistema consolida efeitos financeiros e fiscais uma única vez.

### 5.4 Recusa e cancelamento

- A recusa exige motivo visível ao cliente.
- O cancelamento exige motivo e registra quem realizou a ação.
- Estoque reservado ou baixado deve ser devolvido de forma atômica.
- Pix pago não pode ser tratado como simples cancelamento; deve entrar numa fila
  explícita de reembolso ou resolução manual enquanto não houver estorno automático.
- Um pedido concluído não volta de estado sem uma operação administrativa auditada.

## 6. Máquina de estados

### 6.1 Estado operacional

```csharp
public enum PedidoOnlineStatus
{
    Novo,
    Confirmado,
    EmPreparo,
    ProntoParaRetirada,
    ProntoParaEntrega,
    SaiuParaEntrega,
    Concluido,
    Recusado,
    Cancelado
}
```

Transições permitidas:

| Origem | Destinos permitidos |
|---|---|
| `Novo` | `Confirmado`, `Recusado`, `Cancelado` |
| `Confirmado` | `EmPreparo`, `ProntoParaRetirada`, `ProntoParaEntrega`, `Cancelado` |
| `EmPreparo` | `ProntoParaRetirada`, `ProntoParaEntrega`, `Cancelado` |
| `ProntoParaRetirada` | `Concluido`, `Cancelado` |
| `ProntoParaEntrega` | `SaiuParaEntrega`, `Cancelado` |
| `SaiuParaEntrega` | `Concluido`, `Cancelado` |
| Estados finais | nenhuma transição comum |

O serviço de domínio, e não o frontend, é a fonte de verdade das transições.

### 6.2 Estado de pagamento

Manter separado do estado operacional:

```csharp
public enum PedidoOnlinePagamentoStatus
{
    Pendente,
    AguardandoPix,
    Pago,
    NaEntrega,
    Expirado,
    ReembolsoPendente,
    Reembolsado
}
```

Essa separação permite, por exemplo, um pedido `Confirmado` com pagamento
`NaEntrega`, sem inventar estados combinados difíceis de manter.

## 7. Modelo de dados proposto

### 7.1 `PedidoOnline`

Campos principais:

| Campo | Tipo | Observação |
|---|---|---|
| `Id` | `Guid` | chave interna |
| `Numero` | `long` | número curto sequencial por tenant |
| `UserId` | `Guid` | cliente autenticado pelo fluxo rápido |
| `TipoAtendimento` | enum | `Entrega` ou `Retirada` |
| `Status` | enum | estado operacional |
| `PagamentoStatus` | enum | estado financeiro do pagamento |
| `PaymentMethod` | string | padrão já usado pelo ERP |
| `SubtotalInCents` | int | soma dos itens |
| `DeliveryFeeInCents` | int | taxa congelada no checkout |
| `DiscountInCents` | int | desconto aplicado |
| `TotalInCents` | int | total final calculado no servidor |
| `CustomerNotes` | string? | observação pública do cliente |
| `InternalNotes` | string? | observação restrita à operação |
| `EstimatedMinutes` | int? | previsão informada no aceite |
| `DeliveryResponsibleName` | string? | responsável manual no MVP |
| `CreatedAt` | `DateTime` | UTC |
| `AcceptedAt` | `DateTime?` | UTC |
| `ReadyAt` | `DateTime?` | UTC |
| `DispatchedAt` | `DateTime?` | UTC |
| `CompletedAt` | `DateTime?` | UTC |
| `CancelledAt` | `DateTime?` | UTC |
| `CancellationReason` | string? | obrigatório em recusa/cancelamento |
| `FiscalEffectsCapturedAt` | `DateTime?` | proteção contra efeito duplicado |
| `IdempotencyKey` | string | evita pedido duplicado no checkout |
| `RowVersion` | controle otimista | impede avanço concorrente de status |

### 7.2 Snapshot do endereço

O pedido deve guardar uma cópia imutável do endereço usado, mesmo que o cliente
edite seus dados depois:

- destinatário;
- telefone;
- CEP;
- logradouro;
- número;
- complemento;
- bairro;
- cidade;
- UF;
- referência;
- latitude e longitude opcionais e inicialmente nulas.

Para o MVP esses campos podem ficar no próprio `PedidoOnline`. Uma entidade
`CustomerAddress` reutilizável pode ser adicionada depois sem remover o snapshot.

### 7.3 `PedidoOnlineItem`

Seguir o padrão seguro de `ComandaItem`:

- `PedidoOnlineId`;
- `ProductId` e `VariantId` opcionais;
- nome, preço e custo congelados;
- quantidade e subtotal;
- observação do item;
- área e nome de produção congelados;
- estado de produção quando aplicável.

O preço enviado pelo navegador nunca é aceito como fonte de verdade.

### 7.4 `PedidoOnlineStatusHistorico`

Registrar todas as transições:

- pedido;
- estado anterior e novo;
- data UTC;
- usuário responsável, quando autenticado;
- origem (`Cliente`, `Operador`, `Sistema`);
- motivo ou observação.

Esse histórico será usado pelo acompanhamento do cliente, auditoria, suporte e
métricas de tempo por etapa.

### 7.5 `PedidosOnlineConfig`

Configuração singleton por schema:

- módulo ligado operacionalmente pelo comércio;
- aceita retirada;
- aceita entrega;
- pedido mínimo;
- prazo padrão para retirada;
- prazo padrão para entrega;
- formas de pagamento habilitadas;
- instruções de retirada;
- mensagem fora do horário;
- antecedência máxima para pedidos agendados, deixada desabilitada no MVP;
- tolerância de expiração do Pix;
- horários por dia da semana.

Essa configuração deve ser separada de `SiteConfig`: identidade visual não deve
virar depósito de regras operacionais.

### 7.6 `PedidoOnlineZonaEntrega`

O primeiro cálculo de frete será determinístico, sem mapas externos:

- nome da zona ou bairro;
- CEP inicial e final opcionais;
- bairro normalizado opcional;
- taxa em centavos;
- pedido mínimo opcional;
- prazo adicional em minutos;
- ativo/inativo;
- prioridade para desempate.

No checkout, o backend resolve exatamente uma zona. Se nenhuma zona atender, a
entrega é recusada antes da criação do pedido. A loja sempre pode oferecer retirada.

## 8. Estoque e concorrência

O carrinho do navegador não reserva estoque. No checkout:

1. o backend recarrega produtos e variantes;
2. recalcula preço, promoção, custo, taxa e total;
3. valida disponibilidade;
4. cria pedido e itens;
5. baixa o estoque dentro da mesma transação;
6. registra o primeiro histórico;
7. confirma a transação;
8. só então publica eventos em tempo real.

Recusa, cancelamento válido ou expiração automática devolvem o estoque uma única
vez, também em transação. O pedido precisa de um marcador como
`StockRestoredAt` para impedir devolução duplicada.

Essa decisão segue o padrão transacional já corrigido em `ComandaService` e evita
vender a mesma última unidade em dois pedidos simultâneos.

## 9. Pagamentos

### 9.1 MVP

- Pix antes ou durante a confirmação;
- dinheiro na entrega, com campo “troco para”;
- cartão na entrega, sem capturar dados do cartão no Octus;
- pagamento na retirada.

### 9.2 Extensão do Pix existente

`PixCobrancaOrigem` hoje aceita `Crediario`, `Comanda` e `VendaAvulsa`. Adicionar:

```csharp
PedidoOnline
```

Também adicionar `PedidoOnlineId` e sua FK opcional. As validações devem garantir
que somente a FK correspondente à origem esteja preenchida.

O fluxo precisa ser idempotente: atualizar duas vezes uma cobrança concluída não
pode concluir o pedido nem registrar receita duas vezes.

### 9.3 Regras mínimas

- O pedido mostra claramente se o pagamento ainda está pendente.
- Pix expirado encerra a cobrança e aplica a política configurada ao pedido.
- O comércio não pode marcar como pago sem permissão e auditoria.
- Cancelamento de Pix pago gera `ReembolsoPendente`; não promete estorno automático
  até essa integração existir.
- Nenhuma página pública recebe segredo, certificado ou credencial bancária.

## 10. Financeiro e fiscal

O pedido online deve ser uma origem de venda reconhecida pelo financeiro, não uma
comanda artificial. Os relatórios precisarão distinguir:

- produtos;
- taxa de entrega;
- descontos;
- forma de pagamento;
- origem `PedidoOnline`;
- tipo `Entrega` ou `Retirada`.

A conclusão captura os efeitos financeiros uma única vez. Se houver emissão de
NFC-e, a integração deve receber os itens e descontos conforme as mesmas regras de
comanda e venda avulsa. A taxa de entrega exige definição fiscal explícita antes
do go-live com emissão: não presumir tratamento tributário sem validação contábil.

## 11. APIs propostas

Todos os endpoints operacionais usam `[RequireModule("pedidos_online")]`. Endpoints
públicos continuam resolvidos no tenant correto pelo domínio ou subdomínio.

### 11.1 Cliente/público

| Método | Rota | Uso |
|---|---|---|
| `GET` | `/api/pedidos-online/config-publica` | disponibilidade, horários e meios de pagamento |
| `POST` | `/api/pedidos-online/calcular-entrega` | valida endereço e retorna taxa/prazo |
| `POST` | `/api/pedidos-online` | checkout idempotente |
| `GET` | `/api/pedidos-online/me` | lista resumida do próprio cliente |
| `GET` | `/api/pedidos-online/{id}/me` | detalhe do próprio pedido |
| `POST` | `/api/pedidos-online/{id}/pix` | cria ou recupera cobrança ativa |
| `GET` | `/api/pedidos-online/{id}/pix` | consulta pagamento do próprio pedido |
| `POST` | `/api/pedidos-online/{id}/cancelar` | cancelamento pelo cliente quando permitido |

O `id` nunca concede acesso por si só: a API confirma que o pedido pertence ao
usuário autenticado.

### 11.2 Administração

| Método | Rota | Uso |
|---|---|---|
| `GET` | `/api/pedidos-online/admin` | quadro com filtros e paginação |
| `GET` | `/api/pedidos-online/admin/{id}` | detalhe operacional |
| `PUT` | `/api/pedidos-online/admin/{id}/status` | transição validada |
| `PUT` | `/api/pedidos-online/admin/{id}/estimativa` | prazo prometido |
| `POST` | `/api/pedidos-online/admin/{id}/confirmar-pagamento` | confirmação auditada |
| `POST` | `/api/pedidos-online/admin/{id}/cancelar` | cancelamento administrativo |
| `GET` | `/api/pedidos-online/config` | configuração completa |
| `PUT` | `/api/pedidos-online/config` | atualização da operação |
| `GET` | `/api/pedidos-online/zonas` | zonas de entrega |
| `POST` | `/api/pedidos-online/zonas` | criar zona |
| `PUT` | `/api/pedidos-online/zonas/{id}` | editar zona |
| `DELETE` | `/api/pedidos-online/zonas/{id}` | desativar sem apagar histórico |

## 12. Tempo real e notificações

Criar `PedidoOnlineHub` separado de `ComandaHub`. Grupos:

- `pedidos-online:tenant:{tenantId}:admin`;
- `pedidos-online:tenant:{tenantId}:user:{userId}`;
- `pedidos-online:tenant:{tenantId}:pedido:{pedidoId}`.

Eventos mínimos:

- `PedidoOnlineCriado`;
- `PedidoOnlineAtualizado`;
- `PedidoOnlinePagamentoAtualizado`;
- `PedidoOnlineCancelado`.

O evento serve para avisar que houve mudança; a tela busca novamente o recurso e
não trata o payload do SignalR como banco de dados. Isso reduz divergência entre
abas e facilita evolução do contrato.

Push web deve avisar apenas mudanças relevantes. Não incluir endereço completo,
telefone ou outros dados sensíveis na notificação exibida na tela bloqueada.

## 13. Telas do MVP

### 13.1 Cliente

- `/pedidos`: catálogo com busca, categorias, disponibilidade e carrinho;
- `/pedidos/checkout`: identificação, tipo, endereço, pagamento e revisão;
- `/pedidos/confirmacao/{id}`: número, resumo e instrução de pagamento;
- `/pedidos/acompanhar/{id}`: linha do tempo e previsão;
- `/cliente/pedidos`: histórico do cliente.

### 13.2 Comércio

- `/admin/pedidos-online`: quadro de pedidos com foco em uso diário;
- `/admin/pedidos-online/{id}`: detalhe, pagamento, cliente e histórico;
- `/admin/pedidos-online/configuracoes`: horários, atendimento e pagamentos;
- `/admin/pedidos-online/zonas`: bairros/CEPs, taxas e prazos.

No celular, “Pedidos” deve ocupar um dos atalhos principais quando o módulo estiver
habilitado. O quadro precisa funcionar em tela pequena, pois muitos comércios
operarão pelo próprio telefone.

## 14. Regras de acesso e assinatura

Adicionar:

- módulo de tenant: `pedidos_online`;
- permissão de operador: `pedidos_online`;
- item de navegação em **Vendas**;
- gate no backend e no frontend;
- testes que mantenham `KnownModules` e `TENANT_MODULES` sincronizados.

Recomendação comercial provisória:

- não incluir no Lagoa;
- incluir no Rio e no Mar;
- permitir habilitação manual em plano personalizado ou piloto;
- não depender do módulo `restaurante`.

Uma loja não-restaurante também pode vender para entrega. Quando os dois módulos
estiverem ativos, itens associados a uma área de produção entram na fila da cozinha.

### 14.1 Gravar o GMV desde o primeiro pedido

Cobrar percentual sobre o pedido está descartado **para esta fase**, pelo motivo
de capacidade operacional registrado no RB-02. Mas a porta deve ficar aberta sem
custo: com `PedidoOnline` separando `SubtotalInCents`, `DeliveryFeeInCents` e
`TipoAtendimento`, o GMV de pedidos online por tenant já fica apurável pelo
próprio sistema, mês a mês.

Isso importa porque o Octus é o sistema de registro da venda e emite a NFC-e —
ele conhece o faturamento real sem depender de ninguém declarar nada, que é algo
que nenhum concorrente do grupo B tem. Se um dia houver equipe para sustentar
cobrança variável, ela vira **mudança de precificação** (apurar e lançar em
`PlatformBillingService`, já automatizado no RB-01), não refatoração.

Regra prática: **não construir a cobrança agora, não perder o dado agora.**

### 14.2 Dependência não resolvida — qual CNPJ emite

Loja com dois CNPJs (salão e delivery, ou regimes diferentes) precisa definir
qual emite a nota do pedido online. Hoje `FiscalConfig` é singleton lógico, com
`FiscalConfig.SingletonId` em 59 pontos do código — é o RB-04, e está marcado
como sob demanda.

**Impacto neste MVP:** enquanto o RB-04 não existir, o pedido online usa o
emitente único do tenant. Isso é aceitável para o piloto, mas precisa estar
consciente: se o piloto for feito numa loja com dois CNPJs, a nota vai sair pelo
errado. **Critério de escolha do piloto: loja com um CNPJ só.**

## 15. Segurança, privacidade e confiabilidade

- Nunca confiar em preço, desconto, frete, total ou status enviados pelo cliente.
- Validar tamanho e conteúdo de todas as observações e campos de endereço.
- Aplicar rate limit específico em cálculo de entrega, checkout e consulta pública.
- Usar chave de idempotência única por tenant e cliente.
- Não expor pedidos por ID sem verificar dono ou permissão operacional.
- Registrar ações administrativas relevantes no audit log.
- Mascarar telefone e endereço em logs técnicos.
- Não incluir dados pessoais em eventos destinados ao grupo geral do tenant além
  do mínimo necessário à operação.
- Definir retenção e anonimização compatíveis com os fluxos LGPD já existentes.
- Evitar chamadas externas dentro da transação de estoque.
- Publicar SignalR/push somente depois do commit.
- Tratar a confirmação financeira e fiscal como operações idempotentes.

## 16. Observabilidade e métricas

Registrar, sem criar um sistema analítico paralelo:

- pedidos criados, aceitos, recusados, cancelados e concluídos;
- valor de produtos, frete e desconto;
- origem entrega/retirada;
- tempo até o aceite;
- tempo de preparo;
- tempo entre despacho e conclusão;
- motivo de recusa ou cancelamento;
- falhas de pagamento;
- falhas no envio de eventos e push.

Painel inicial:

- pedidos e faturamento no período;
- ticket médio;
- taxa de conclusão;
- tempo médio de aceite e preparo;
- estimativa de comissão evitada, usando uma porcentagem configurável apenas para
  simulação e sempre identificada como estimativa.

## 17. Plano de implementação

### Entrega 0 — decisões e contratos

- confirmar nome comercial e regra dos planos;
- confirmar formas de pagamento do piloto;
- definir o comércio piloto;
- validar tratamento fiscal da taxa de entrega;
- congelar os DTOs e a máquina de estados do primeiro ciclo.

**Concluída quando:** decisões registradas e critérios do piloto assinados.

### Entrega 1 — fundação do módulo

- registrar `pedidos_online` no backend e frontend;
- adicionar permissão e navegação;
- criar entidades, configurações, índices e migration;
- implementar máquina de estados e histórico;
- criar serviços com transação de estoque e idempotência.

**Concluída quando:** testes unitários criam, recusam e cancelam pedidos sem
duplicar baixa ou devolução de estoque.

### Entrega 2 — checkout público

- transformar a vitrine em catálogo comprável;
- criar carrinho;
- implementar entrega/retirada;
- calcular zona e taxa no servidor;
- criar confirmação e acompanhamento autenticado.

**Concluída quando:** um cliente consegue enviar um pedido real e atualizar a
página sem duplicá-lo.

### Entrega 3 — operação do comércio

- criar quadro responsivo;
- aceitar, recusar e avançar estados;
- integrar áreas de produção quando disponíveis;
- criar hub e notificações;
- permitir configuração de horários e zonas.

**Concluída quando:** dois painéis e a tela do cliente refletem a mesma mudança de
estado em tempo real.

### Entrega 4 — pagamentos

- estender `PixCobranca`;
- criar e consultar Pix pelo pedido;
- implementar dinheiro/cartão na entrega;
- tratar expiração e reembolso pendente;
- impedir efeitos duplicados.

**Concluída quando:** os cenários pago, expirado, cancelado e pagamento na entrega
possuem testes e estado consistente.

### Entrega 5 — financeiro, fiscal e relatórios

- integrar origem `PedidoOnline` aos cálculos financeiros;
- incluir a taxa sem misturá-la à receita de produtos;
- integrar NFC-e após validação fiscal;
- adicionar métricas operacionais básicas.

**Concluída quando:** pedido concluído aparece uma única vez nos relatórios e pode
ser conciliado com pagamento e documento fiscal.

### Entrega 6 — piloto controlado

- habilitar somente para uma loja;
- cadastrar zonas, horários e pagamentos;
- executar pedidos de ponta a ponta em celular e desktop;
- acompanhar incidentes e métricas por pelo menos uma semana operacional;
- decidir ajustes antes de liberar nos planos comerciais.

**Concluída quando:** a loja piloto consegue operar sem planilha paralela e os
problemas críticos identificados estão corrigidos ou explicitamente aceitos.

## 18. Estratégia de testes

### Backend

- transições válidas e inválidas;
- isolamento entre tenants;
- acesso apenas ao próprio pedido;
- cálculo de zona e taxa;
- preço recalculado no servidor;
- concorrência na última unidade;
- idempotência do checkout;
- cancelamento e devolução única de estoque;
- confirmação Pix repetida;
- efeito financeiro único;
- módulo e permissão obrigatórios.

### Frontend/Playwright

- carrinho e checkout de entrega;
- checkout de retirada;
- endereço fora da área;
- pedido duplicado por duplo clique;
- quadro recebendo novo pedido;
- avanço de status refletido para o cliente;
- recusa com motivo;
- Pix pendente e pago;
- navegação escondida sem módulo ou permissão;
- operação em viewport de celular;
- acessibilidade por teclado, foco e leitores de tela.

### Regressão

- comanda de mesa continua independente;
- baixa e devolução de estoque existentes não mudam;
- relatórios não contam o mesmo pedido como comanda;
- tenant sem o módulo mantém exatamente a experiência atual.

## 19. Critérios de lançamento do MVP

O MVP pode ser liberado para piloto quando:

- nenhum pedido é criado duas vezes por repetição da requisição;
- estoque não fica negativo e é restaurado uma única vez;
- cliente nunca acessa pedido de outra pessoa;
- tenant nunca recebe evento ou dado de outro tenant;
- totais são calculados exclusivamente no backend;
- Pix e pagamento na entrega possuem estados compreensíveis;
- operador consegue concluir o fluxo inteiro pelo celular;
- cliente acompanha sem instalar aplicativo;
- cancelamentos pagos aparecem como pendência explícita;
- financeiro registra o pedido uma única vez;
- logs e notificações não vazam endereço ou telefone;
- a suíte de regressão atual continua aprovada.

## 20. Preparação para o aplicativo de motoboy

Depois de validar volume e operação, adicionar:

- cadastro de entregadores;
- disponibilidade online/offline;
- atribuição e aceite de corrida;
- localização atual com consentimento;
- histórico de posições com retenção curta;
- rota externa e navegação;
- prova de entrega por código, QR Code, foto ou assinatura;
- incidentes de entrega;
- proteção contra localização forjada;
- regras de privacidade e remoção dos dados de geolocalização.

O app deverá usar as mesmas APIs e máquina de estados do módulo. A PWA continuará
suficiente para cliente e comércio; para o motoboy, rastreamento confiável com a
tela bloqueada provavelmente exigirá aplicativo nativo ou híbrido com suporte real
a localização em segundo plano.

## 21. Decisões pendentes antes do primeiro código funcional

1. O módulo entra automaticamente no Rio e Mar ou será adicional mensal?
2. O primeiro piloto será restaurante, varejo ou ambos?
3. Pix deve ser obrigatório antes do aceite ou a loja pode aceitar enquanto aguarda?
4. Por quanto tempo a loja pode deixar um pedido em `Novo`?
5. O cliente poderá cancelar sozinho até qual estado?
6. A taxa de entrega será configurada por bairro, CEP ou ambos no piloto?
7. O piloto emitirá NFC-e para a taxa de entrega desde o primeiro dia?

Essas decisões não impedem a fundação técnica, mas precisam estar fechadas antes
de colocar o checkout diante de clientes reais.

### 21.1 Recomendações (consolidação de 2026-08-26)

São decisões de negócio — ficam aqui como proposta, para o dono da plataforma
confirmar ou trocar. Nenhuma delas bloqueia as Entregas 0 e 1.

**1. Adicional mensal no piloto, embutido depois.** Vender como adicional
permite oferecer à base existente e descobrir preço antes de fixar. Embutir em
Rio e Mar desde já converte zero receita nova de quem já é cliente. Depois de
validado, embutir e usar como argumento de upgrade do Lagoa.

**2. Piloto em restaurante.** É onde o fluxo é exercitado mais forte (área de
produção, pico de horário, cancelamento) e onde o argumento comercial é mais
afiado. Restrição herdada da seção 14.2: **loja com um CNPJ só.**

**3. Pix não obrigatório antes do aceite.** A loja aceita enquanto aguarda. Este
canal atende o cliente que já é da loja e já confia nela — exigir pré-pagamento
derruba conversão exatamente onde a plataforma não tem o poder de barganha de um
marketplace.

**4. Dez minutos em `Novo`,** depois cancelamento automático com devolução de
estoque. Configurável. Pedido parado sem resposta é pior que pedido recusado: o
cliente fica esperando e não pede em outro lugar.

**5. Cliente cancela sozinho até `Confirmado`.** A partir de `EmPreparo` o custo
já foi incorrido e o cancelamento passa a exigir a loja.

**6. Zona por bairro no piloto,** com faixa de CEP como desempate opcional.
Bairro é o que o lojista sabe de cabeça; faixa de CEP digitada à mão é fonte de
erro silencioso, e erro de zona vira frete errado cobrado do cliente.

**7. Não emitir NFC-e sobre a taxa de entrega no piloto.** Frete tem tratamento
próprio no layout e afeta base de cálculo. Emitir os produtos normalmente e
deixar a taxa fora até validação contábil explícita — reforça o que a seção 10
já determina.
