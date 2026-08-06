# Plano de pagamentos multi-tenant — Mercado Pago OAuth

**Período proposto:** 03 a 07 de agosto de 2026

**Objetivo da semana:** entregar em sandbox um fluxo completo de conexão e cobrança Pix no qual cada lojista recebe diretamente na própria conta Mercado Pago.

**Decisão recomendada:** usar uma única aplicação Mercado Pago pertencente ao Octus, conectar cada vendedor por OAuth e criar pagamentos com o token do próprio vendedor. O Octus não recebe, não guarda e não repassa o dinheiro da venda.

## 1. Resultado esperado

Ao final do MVP:

1. O dono do tenant abre **Admin → Integrações → Recebimentos**.
2. Clica em **Conectar Mercado Pago**.
3. Autoriza o Octus na conta Mercado Pago da própria empresa.
4. O Octus salva a autorização criptografada e mostra a conta realmente conectada.
5. Ao cobrar uma comanda ou crediário, o sistema gera um Pix na conta daquele lojista.
6. O pagamento é confirmado por webhook e também pode ser conferido manualmente.
7. A comanda ou parcela é baixada uma única vez, mesmo com webhook repetido ou duas verificações simultâneas.
8. A entrada aparece no financeiro e na DRE do tenant certo.

O dinheiro segue este caminho:

```text
Cliente pagador
      ↓
Mercado Pago do lojista
      ↓
Conta bancária escolhida pelo lojista, inclusive Conta PJ Contabilizei

Octus: cria a cobrança, recebe o status e concilia; não movimenta o saldo.
```

## 2. O que existe hoje

O projeto não começa do zero. Já existem:

- Pix dinâmico pelo Banco Inter para comanda e crediário.
- QR Code, Pix Copia e Cola e modal compartilhado no frontend.
- Polling de status e baixa automática da comanda/crediário.
- Credenciais e certificados do Inter criptografados por tenant.
- `ExternalTransaction` com deduplicação e grupo de DRE.
- Isolamento por schema PostgreSQL e helper para executar código no contexto correto de cada tenant.
- Tela de integrações com cards de Inter, Mercado Pago, SEFAZ e OFX.

### Problemas encontrados

1. **Mercado Pago ainda não está funcional.** O card apenas grava `ClientId` e `ClientSecret`; não há troca OAuth, criação de pagamento ou webhook.
2. **O formulário atual usa o modelo errado para multi-tenant.** Ele pede credenciais de aplicação para cada lojista. No modelo recomendado, as credenciais da aplicação são do Octus e o lojista só autoriza a própria conta.
3. **“Conectado” não significa conexão validada.** Hoje qualquer linha ativa em `integration_configs` aparece como conectada, mesmo sem token válido.
4. **O endpoint que salva tokens é acessível ao admin.** Apesar do comentário dizer “uso interno”, `POST /api/contas-receber/integracoes/{source}/token` herda apenas a autorização de admin. O fluxo OAuth deve ser o único gravador de tokens do Mercado Pago.
5. **A cobrança está acoplada ao Inter.** Controllers conhecem `InterSyncService`, status `CONCLUIDA` e mensagens específicas do banco.
6. **Há risco de dupla baixa.** O próprio relatório de auditoria do repositório já registra corrida na reconciliação Pix; webhooks tornam obrigatório corrigir isso antes de produção.
7. **A tela mistura funções diferentes.** SEFAZ é fiscal; Inter/OFX são conciliação; Mercado Pago é recebimento. A organização atual aumenta a dúvida sobre configurar SEFAZ duas vezes.
8. **A comunicação comercial está imprecisa.** “Todas gratuitas” e “sem taxa extra” não devem ser exibidos para Mercado Pago: não há custo adicional criado pelo Octus no MVP, mas as tarifas do provedor continuam existindo.

## 3. Arquitetura recomendada

### 3.1 Separar três responsabilidades

| Camada | Responsabilidade | Onde fica |
|---|---|---|
| Aplicação Mercado Pago | `ClientId`, `ClientSecret`, redirect URI e segredo de webhook do Octus | Variáveis de ambiente da API |
| Conexão do vendedor | Token, refresh token, conta externa, validade e status | Catálogo global, criptografado e ligado ao `TenantId` |
| Cobrança e resultado financeiro | Valor, origem, status, IDs externos e baixa | Schema individual do tenant |

As credenciais da aplicação **não** devem ser repetidas em cada schema. São segredos operacionais da plataforma. Já os registros financeiros continuam isolados por tenant.

### 3.2 Por que a conexão precisa ser roteável pelo catálogo

O callback OAuth usa uma URL estática da plataforma e o webhook chega sem sessão de usuário e sem subdomínio confiável. Portanto, antes de abrir o schema da loja, o backend precisa descobrir o tenant por um identificador global e validado.

O catálogo `public` já cumpre essa função para tenants e contador. A conexão deve conter:

- `TenantId`.
- Provedor (`mercadopago`).
- ID da conta/vendedor no provedor.
- Access token e refresh token criptografados.
- Expiração, scopes, ambiente e status.
- Datas de conexão, renovação, revogação e último erro sanitizado.

Índices únicos obrigatórios:

- `(TenantId, Provider)` — uma conexão ativa do provedor por tenant.
- `(Provider, ExternalAccountId)` — uma conta externa não pode ser ligada silenciosamente a duas lojas.

### 3.3 OAuth seguro

Fluxo proposto:

1. Admin autenticado solicita a conexão.
2. Backend cria `state` aleatório de uso único e PKCE S256.
3. Banco guarda somente o hash do `state`, o `TenantId`, a expiração, o retorno permitido e o `code_verifier` criptografado.
4. Frontend redireciona para o Mercado Pago.
5. Callback público valida `state`, validade, uso único e PKCE.
6. Backend troca o código por tokens usando as credenciais globais do Octus.
7. Backend grava a conexão criptografada e invalida o `state` na mesma transação.
8. Usuário volta para `/admin/integracoes?mercadopago=connected` no subdomínio original, previamente validado.

Não colocar `TenantId`, slug, token ou URL arbitrária diretamente no `state` sem registro e validação no servidor.

### 3.4 Cobrança

Criar uma abstração pequena, sem tentar generalizar todos os meios de pagamento:

```csharp
public interface IPixPaymentGateway
{
    string Provider { get; }
    Task<CreatePixResult> CreateAsync(CreatePixCommand command, CancellationToken ct);
    Task<PaymentStatusResult> GetStatusAsync(string externalPaymentId, CancellationToken ct);
}
```

O `PaymentOrchestrator` escolhe o provedor ativo do tenant, cria a chave de idempotência e traduz os status externos para:

- `pending`
- `approved`
- `expired`
- `cancelled`
- `refunded`
- `failed`

Os controllers de comanda e crediário deixam de conhecer Inter ou Mercado Pago. Eles pedem ao orquestrador “crie um Pix para esta origem”.

### 3.5 Estratégia de banco para esta semana

Para reduzir o risco de quebrar fluxos já publicados, **não renomear a tabela `pix_cobrancas` no MVP**. Expandir o modelo atual com:

- `provider` (`inter` ou `mercadopago`).
- `external_payment_id`.
- `external_reference` gerada pelo Octus.
- `idempotency_key` única.
- `provider_status`.
- `approved_amount_in_cents`.
- `refunded_amount_in_cents`.
- `updated_at`.
- `last_checked_at`.

`TxId` continua como identificador público de compatibilidade. No Mercado Pago pode receber o ID interno da cobrança do Octus, sem forçar o ID externo ao limite de 35 caracteres legado do Inter.

**Trade-off:** o nome da tabela permanece específico de Pix, mas evitamos uma migração grande, alteração simultânea de todos os DTOs e risco sobre cobranças Inter existentes. A renomeação para `payment_charges` fica para a fase de cartão/Checkout Pro.

### 3.6 Webhook e reconciliação

Endpoint global:

```text
POST /api/webhooks/mercadopago
```

Pipeline:

1. Aplicar limite de tamanho e rate limit próprio.
2. Validar `x-signature` e tolerância de timestamp quando o produto suportar assinatura.
3. Persistir uma entrada idempotente de webhook no catálogo.
4. Responder `200` rapidamente.
5. Worker busca os dados completos do pagamento na API do Mercado Pago com o token do vendedor.
6. Confere vendedor, moeda, valor, referência externa e ambiente.
7. Abre um novo scope, configura `ITenantContext` e somente então resolve `AppDbContext`.
8. Bloqueia a cobrança para atualização e executa a baixa numa transação.
9. Marca o evento como processado; falhas transitórias entram em retry com backoff.

O corpo do webhook nunca é a prova final de pagamento. A prova é a consulta autenticada ao provedor e a comparação com a cobrança local.

Para produtos em que a assinatura não esteja disponível, a consulta autenticada, a referência imprevisível e a comparação de conta/valor tornam-se obrigatórias. O polling manual permanece como contingência.

### 3.7 Regra contábil: pagamento não é uma segunda venda

A DRE atual calcula receita a partir de comandas fechadas e vendas avulsas. Portanto, a aprovação do Mercado Pago é a **liquidação** daquela venda, não uma nova receita.

- Pagamento de comanda: fecha a comanda; não cria outra receita em `ExternalTransaction`.
- Pagamento de crediário: cria `PagamentoCrediario` e reduz o saldo; não replica o valor na receita da DRE.
- Tarifa do Mercado Pago: quando houver valor confirmado pelo provedor, pode virar despesa no grupo financeiro.
- Reembolso: cria reversão auditável; nunca apaga a venda original.
- Extrato bancário/OFX posterior: deve ser conciliado com a liquidação existente, não importado novamente como receita sem vínculo.

Essa regra evita que venda, confirmação do gateway e transferência para a Conta Contabilizei apareçam como três entradas diferentes.

## 4. Experiência de uso recomendada

### Nova organização de `/admin/integracoes`

#### Recebimentos

**Mercado Pago**

- Desconectado: botão primário **Conectar Mercado Pago**.
- Conectando: explicação curta de que o lojista será redirecionado.
- Conectado: nome/ID mascarado da conta, ambiente, última confirmação e botão **Desconectar**.
- Atenção: autorização perto de expirar.
- Erro: “Reconectar conta” com explicação compreensível.

**Banco Inter**

- Mantém configuração avançada atual.
- Indicar que exige credenciais e certificado da conta do próprio lojista.
- Continua disponível como alternativa de Pix e conciliação.

#### Conciliação bancária

- Banco Inter — sincronização automática.
- OFX — importação manual, funciona também com Conta PJ Contabilizei.
- Open Finance — etapa futura, porque adiciona custo e parceiro regulado.

#### Fiscal

- Card SEFAZ apenas mostra status.
- CTA: **Abrir configuração fiscal**.
- CNPJ, UF, ambiente e certificado continuam com uma única fonte em `/admin/fiscal`.
- O botão desta página ativa/desativa somente a sincronização de NF-e, não duplica campos.

### Ajustes no modal Pix

- Trocar “Gerando cobrança no Inter” por “Gerando cobrança Pix”.
- Mostrar o provedor em texto secundário: “Processado por Mercado Pago” ou “Banco Inter”.
- Usar estados normalizados, sem comparar diretamente `CONCLUIDA` no frontend.
- Manter QR Code, Copia e Cola e verificação manual já existentes.
- Após webhook, atualizar via SignalR; polling a cada 5 segundos continua como fallback temporário.

## 5. Arquivos que mudariam

### Backend — novos

| Arquivo proposto | Função |
|---|---|
| `CardGameStore/Multitenancy/TenantPaymentConnection.cs` | Conexão OAuth global por tenant |
| `CardGameStore/Multitenancy/PaymentOAuthAttempt.cs` | `state`, PKCE, expiração e uso único |
| `CardGameStore/Multitenancy/PaymentWebhookInbox.cs` | Inbox idempotente e roteamento de eventos |
| `CardGameStore/DTOs/PaymentDtos.cs` | Contratos públicos normalizados |
| `CardGameStore/Services/Payments/IPixPaymentGateway.cs` | Contrato comum de Pix |
| `CardGameStore/Services/Payments/PaymentOrchestrator.cs` | Escolha de provedor, idempotência e regras comuns |
| `CardGameStore/Services/Payments/MercadoPagoGateway.cs` | OAuth token, Pix e consulta de pagamento |
| `CardGameStore/Services/Payments/InterPixGateway.cs` | Adaptador sobre o `InterSyncService` existente |
| `CardGameStore/Services/Payments/PaymentReconciliationService.cs` | Baixa transacional de comanda/crediário |
| `CardGameStore/Services/Payments/PaymentWebhookBackgroundService.cs` | Retry e processamento assíncrono |
| `CardGameStore/Controllers/PaymentIntegrationsController.cs` | Status, conectar, callback e desconectar |
| `CardGameStore/Controllers/PaymentWebhooksController.cs` | Entrada pública validada de webhook |
| `tests/unit/CardGameStore.Tests/Payments/*` | Testes de OAuth, gateway, webhook e reconciliação |

### Backend — modificados

| Arquivo | Mudança |
|---|---|
| `CardGameStore/Program.cs` | `HttpClient`, opções, serviços, worker, rate limit e health checks |
| `CardGameStore/Multitenancy/CatalogDbContext.cs` | DbSets, índices e relacionamentos globais |
| `CardGameStore/Data/AppDbContext.cs` | Campos e índices idempotentes da cobrança |
| `CardGameStore/Models/PostgreSQL/PixCobranca.cs` | Provedor, IDs externos e estados normalizados |
| `CardGameStore/Controllers/ComandaController.cs` | Substituir uso direto do Inter pelo orquestrador |
| `CardGameStore/Controllers/CrediariosController.cs` | Substituir uso direto e corrigir corrida de baixa |
| `CardGameStore/Controllers/ContasReceberController.cs` | Remover Mercado Pago fictício e endpoint manual de token |
| `CardGameStore/Services/Implementations/InterSyncService.cs` | Expor somente operações necessárias ao adaptador |
| `CardGameStore/appsettings.json` | Apenas estrutura sem segredos e timeouts seguros |

Serão necessárias uma migration do `AppDbContext` e uma migration do `CatalogDbContext`.

### Frontend

| Arquivo | Mudança |
|---|---|
| `frontend/app/admin/integracoes/page.tsx` | Seções, OAuth por botão, estados reais e desconexão |
| `frontend/components/admin/CobrancaPixModal.tsx` | Texto neutro, provedor e estados normalizados |
| `frontend/lib/api.ts` | DTOs e chamadas de integração/pagamento |
| `frontend/components/admin/comanda/ComandaCard.tsx` | Consumir resposta normalizada, sem impacto visual grande |
| `frontend/app/admin/crediario/page.tsx` | Consumir resposta normalizada |
| `frontend/lib/manualContent.ts` | Passo a passo simples para conectar e cobrar |

## 6. Endpoints propostos

| Método e rota | Autorização | Finalidade |
|---|---|---|
| `GET /api/payment-integrations` | Admin + Financeiro | Estado real das conexões |
| `POST /api/payment-integrations/mercadopago/connect` | Admin + Financeiro | Cria tentativa OAuth e devolve URL |
| `GET /api/payment-integrations/mercadopago/callback` | Anônimo + state/PKCE | Finaliza conexão |
| `DELETE /api/payment-integrations/mercadopago` | Admin + Financeiro | Desconecta localmente e registra auditoria |
| `POST /api/comanda/{id}/pix` | Mantido | Cria no provedor escolhido |
| `GET /api/comanda/{id}/pix/{id}/status` | Mantido | Consulta normalizada/fallback |
| `POST /api/crediarios/{id}/pix` | Mantido | Cria no provedor escolhido |
| `GET /api/crediarios/{id}/pix/{id}/status` | Mantido | Consulta normalizada/fallback |
| `POST /api/webhooks/mercadopago` | Assinatura + consulta ao provedor | Recebe eventos globais |

Manter as rotas atuais de comanda/crediário reduz o impacto no frontend e permite rollback do provedor sem rollback de tela.

## 7. Segurança obrigatória antes da produção

- OAuth Authorization Code com PKCE S256 e `state` de uso único.
- Redirect URI estática e lista fechada de URLs internas para retorno.
- Tokens e PKCE verifier criptografados com o `EncryptionService` existente.
- Client secret e webhook secret somente em secret manager/variável de ambiente.
- Nenhum token em log, query string própria, resposta ao frontend ou auditoria.
- Validação de webhook, timestamp e comparação em tempo constante.
- Consulta autenticada do pagamento após a notificação.
- Verificação de `ExternalAccountId`, moeda, valor e referência.
- Índices únicos para conexão, tentativa OAuth, evento e idempotência de cobrança.
- Transação e bloqueio pessimista/atualização condicional na baixa.
- Timeout curto, retry somente em erros transitórios e circuit breaker no provedor.
- Auditoria de conectar, reconectar e desconectar, sem dados secretos.
- Checkout hospedado/tokenização do Mercado Pago para cartão; o Octus não recebe número de cartão ou CVV.

## 8. Trade-offs e decisão

| Opção | Vantagem | Custo/risco | Decisão |
|---|---|---|---|
| Mercado Pago OAuth | Lojista usa conta própria; bom onboarding; Pix e cartão no mesmo ecossistema | Dependência do provedor, tokens e aprovação da aplicação | **Escolhida para MVP** |
| Banco Inter por tenant | Já funciona; dinheiro direto no banco do tenant | Setup técnico com certificado; onboarding pesado; acoplamento atual | Manter como alternativa |
| Asaas subcontas | API desenhada para plataforma e subcontas | Onboarding/KYC e modelo comercial próprios; mais uma dependência | Segunda integração |
| Credenciais digitadas por tenant | Implementação inicial simples | Suporte difícil, maior exposição de segredos e experiência ruim | Não usar para Mercado Pago |
| Conta central + repasse | Controle total pela plataforma | Conciliação, fiscal, chargeback, compliance e risco de custodiar valores | Rejeitada |
| Open Finance/Belvo | Boa conciliação da Conta Contabilizei | Não cria cobrança; custo e parceiro adicional | Fase futura |
| Checkout Pro | Menor escopo PCI e entrega rápida de cartão | Redireciona o comprador | Recomendado para cartão na fase seguinte |
| Checkout transparente | Experiência dentro do Octus | Mais frontend, antifraude, testes e superfície de segurança | Não cabe no MVP da semana |
| Cobrar `marketplace_fee` | Receita por transação | Contrato, impostos, suporte a estorno e percepção do lojista | **Zero no MVP**; assinatura do Octus continua separada |

## 9. Escopo fechado desta semana

### Segunda-feira — 03/08

- Fechar ADR/arquitetura e contratos.
- Criar entidades e migrations de catálogo e tenant.
- Configurar opções, `HttpClient` e abstração de gateway.
- Testar constraints de isolamento e idempotência.

**Saída:** banco e fundação compilando, sem alterar comportamento do Inter.

### Terça-feira — 04/08

- Implementar início OAuth, `state`, PKCE e callback.
- Criptografar tokens e obter dados básicos da conta conectada.
- Trocar modal de credenciais por **Conectar Mercado Pago**.
- Implementar estados conectado, erro, expiração e reconexão.

**Saída:** tenant de teste conecta/desconecta conta sandbox com segurança.

### Quarta-feira — 05/08

- Implementar criação de Pix com token do vendedor.
- Introduzir `PaymentOrchestrator` e adaptador do Inter.
- Integrar comanda e crediário mantendo as rotas atuais.
- Atualizar modal Pix e DTOs.

**Saída:** dois tenants geram Pix em contas de teste diferentes.

### Quinta-feira — 06/08

- Implementar webhook inbox, assinatura, worker e retry.
- Corrigir corrida e garantir baixa idempotente.
- Preservar a regra contábil: baixa não cria uma segunda receita; tarifa e reembolso entram separadamente.
- Cobrir pagamento aprovado, expirado, cancelado, reembolsado e evento fora de ordem.

**Saída:** pagamento confirmado fecha/baixa exatamente uma vez e no tenant correto.

### Sexta-feira — 07/08

- Teste end-to-end em sandbox com pelo menos dois tenants.
- Testes de isolamento, segurança, falha de provedor e rollback.
- Build frontend/backend e suite completa.
- Feature flag inicialmente habilitada só para tenants de teste.
- Runbook de credenciais, webhook, reconexão e suporte.

**Saída:** MVP sandbox pronto para piloto controlado.

## 10. O que não entra nesta semana

- Checkout transparente com cartão dentro do Octus.
- TEF/maquininha física e Mercado Pago Point.
- Assinatura dos planos do Octus pelo mesmo fluxo.
- Comissão por transação.
- Open Finance automático da Conta Contabilizei.
- Asaas como segundo provedor.
- Migração/renomeação definitiva de `pix_cobrancas` para `payment_charges`.
- Reembolso iniciado pelo Octus; no MVP ele apenas reconhece e concilia o reembolso feito no provedor.

Separar esses itens é o que torna realista concluir o núcleo com segurança em cinco dias.

## 11. Critérios de aceite

### Multi-tenant

- Conta Mercado Pago A só pode ser usada pelo tenant A.
- Webhook da conta A nunca abre nem altera o schema B.
- A mesma conta externa não pode ser conectada a dois tenants sem uma ação explícita de suporte e trilha de auditoria.
- Callback repetido, `state` vencido ou já utilizado é rejeitado.

### Pagamento

- Duplo clique cria uma cobrança, não duas.
- Webhook repetido gera uma baixa, não duas.
- Polling e webhook simultâneos geram uma baixa, não duas.
- Valor recebido diferente, moeda diferente ou referência desconhecida não fecha a venda e gera alerta.
- Evento de reembolso não apaga histórico; registra estorno/reversão.

### Operação

- Token expirado é renovado com rotação do refresh token e gravação atômica.
- Revogação muda o card para “Reconectar” sem derrubar outras telas.
- Mercado Pago indisponível devolve erro claro e não persiste cobrança fantasma.
- Inter continua funcionando para tenants já configurados.

### Qualidade

- Suite backend completa aprovada.
- Build de produção do frontend aprovado.
- Testes novos de OAuth, assinatura, idempotência e isolamento aprovados.
- Nenhum segredo aparece em logs ou respostas.
- Migration sobe e desce em banco descartável e provisiona corretamente um tenant novo.

## 12. Rollout e rollback

1. Criar flag `Payments:MercadoPago:Enabled` global e habilitação por tenant piloto.
2. Subir migrations sem mudar o provedor padrão.
3. Conectar duas contas de teste e rodar sandbox.
4. Habilitar um tenant interno/piloto.
5. Acompanhar taxa de criação, aprovação, tempo de confirmação, retries e erros de roteamento.
6. Expandir gradualmente.

Rollback operacional: desabilitar Mercado Pago para novas cobranças e manter leitura/processamento de webhooks das cobranças já criadas. Nunca desligar o consumidor de webhook antes de finalizar ou expirar as cobranças pendentes.

## 13. Observabilidade mínima

Métricas por provedor, sem IDs pessoais em labels:

- conexões ativas, expirando e com erro;
- cobranças criadas, aprovadas, expiradas e falhas;
- latência da API do provedor;
- webhooks recebidos, duplicados, inválidos e em retry;
- tempo entre criação e aprovação;
- divergências de valor/conta/referência;
- quantidade de baixas evitadas por idempotência.

Logs devem carregar `TenantId`, `Provider`, `ChargeId`, `ExternalPaymentId` mascarado quando necessário e `TraceId`, nunca tokens.

## 14. Dependências e riscos externos

### Necessário para começar

- Criar a aplicação Mercado Pago do Octus.
- Cadastrar redirect URI de teste e produção.
- Obter `ClientId`, `ClientSecret` e segredo do webhook.
- Criar duas contas/usuários de teste vendedores e um pagador.
- Definir a URL pública HTTPS para callback e webhook de sandbox.

### Risco de prazo

O código e o fluxo sandbox cabem na semana. A ativação produtiva pode depender de validação da aplicação, dados cadastrais/KYC, permissões e credenciais liberadas pelo Mercado Pago. Isso é externo ao repositório e não deve ser confundido com “código pronto”.

## 15. Decisões adotadas por padrão

Para não travar a execução:

- Provedor inicial: Mercado Pago OAuth.
- Meio de pagamento da semana: Pix dinâmico.
- Recebedor: conta do próprio tenant.
- Comissão do Octus por venda: zero.
- Receita do Octus: mensalidade existente, separada das vendas.
- Cartão futuro: Checkout Pro antes do transparente.
- Conta PJ Contabilizei: destino de saque/transferência e conciliação por OFX no MVP.
- Banco Inter: mantido e adaptado, não removido.
- SEFAZ: configuração fiscal única; a tela de integrações apenas exibe/aciona sincronização.

## 16. Próxima fase após o MVP

1. Checkout Pro para cartão e link de pagamento.
2. Venda avulsa e eventos usando o mesmo orquestrador.
3. Painel de conciliação com divergências e reembolsos.
4. Asaas como segundo provedor para tenants que não usam Mercado Pago.
5. Open Finance/Belvo se o volume justificar conciliação automática da Conta Contabilizei.
6. Renomear a persistência para `payment_charges` e suportar múltiplos meios de pagamento sem dívida de nomenclatura.
