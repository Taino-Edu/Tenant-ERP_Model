# Motor fiscal central para ERPs externos

## Responsabilidades

O ERP externo continua sendo a fonte de verdade de venda, cliente, estoque, caixa e financeiro. O Tenant ERP recebe um snapshot fiscal imutavel e fica responsavel por configuracao do emitente, certificado A1 criptografado, CSC, numeracao, XML, contingencia, retry, cancelamento e comunicacao com a SEFAZ.

Cada tenant `ExternalIntegrated` possui um schema isolado com residencia hibrida. O schema nao recebe vendas nem estoque do ERP externo; guarda somente dados e documentos necessarios ao motor fiscal.

## Autenticacao e seguranca

- OAuth2 `client_credentials`, associado a um unico tenant.
- `fiscal.read` permite consultar configuracao, saude, nota e DANFE.
- `fiscal.write` permite configurar, enviar A1, emitir, reprocessar e cancelar.
- O segredo do cliente e a senha do A1 nunca devem chegar ao navegador.
- O certificado e o CSC ficam criptografados no schema do tenant.
- O reverse proxy publica somente HTTPS; o backend valida tenant, modulo e escopo novamente.

## Fluxo de emissao

1. O ERP envia `POST /api/integrations/services/fiscal/nfce` com itens, tributos, pagamento e tres identificadores: `source`, `externalDocumentId` e `idempotencyKey`.
2. O Tenant ERP grava o snapshot antes de transmitir.
3. Indices unicos impedem duas notas para a mesma origem ou chave idempotente.
4. Retry reutiliza o snapshot persistido. O ERP externo nao e consultado novamente.
5. Cancelamento fiscal nao altera estoque ou caixa central; o ERP de origem aplica seus efeitos operacionais.

## Endpoints v1

- `GET|PUT /api/integrations/services/fiscal/config`
- `POST /api/integrations/services/fiscal/certificate`
- `GET /api/integrations/services/fiscal/health`
- `POST /api/integrations/services/fiscal/nfce`
- `GET /api/integrations/services/fiscal/nfce/{id}`
- `GET /api/integrations/services/fiscal/nfce?source=&externalDocumentId=`
- `POST /api/integrations/services/fiscal/nfce/{id}/retry`
- `POST /api/integrations/services/fiscal/nfce/{id}/cancel`
- `GET /api/integrations/services/fiscal/nfce/{id}/receipt`

## Ativacao segura

1. Fazer deploy do Tenant ERP para criar/migrar o schema externo.
2. Conceder `fiscal.write` ao cliente da integracao.
3. Configurar emitente, CSC e A1 em Homologacao.
4. Testar uma venda controlada e repetir a mesma requisicao para provar idempotencia.
5. Validar DANFE, retry e cancelamento em Homologacao.
6. Ativar o adaptador no ERP externo somente depois dos testes.
7. Mudar para Producao apenas com validacao do contador e do certificado titular.
