# Integracao REST multi-tenant

Esta arquitetura permite que sistemas externos, como o Soft Nerd, usem servicos
da plataforma sem usar login de uma pessoa. Cada credencial pertence a uma unica
loja e recebe apenas os escopos necessarios.

Tenants `Native` possuem schema e usuarios na plataforma. Tenants
`ExternalIntegrated` mantem vendas, estoque, financeiro e usuarios no sistema de
origem; o catalogo central guarda somente identidade, contratacao e credenciais.
Registrar um externo nao cria schema fisico nem copia dados operacionais.

## Fluxo de autenticacao

1. Em tenant nativo, um administrador acessa o dominio da loja e cria um cliente
   em `POST /api/integrations/clients`. Em tenant externo, o dono da plataforma
   usa `POST /api/platform/tenants/{tenantId}/integration-clients`.
2. A API devolve `client_id` e `client_secret`. O segredo aparece uma unica vez.
3. O integrador solicita um token no mesmo dominio da loja:

```http
POST /api/integrations/token
Content-Type: application/json

{
  "grant_type": "client_credentials",
  "client_id": "ti_...",
  "client_secret": "..."
}
```

4. A resposta fornece um JWT de curta duracao:

```json
{
  "access_token": "eyJ...",
  "token_type": "Bearer",
  "expires_in": 900,
  "scope": "financeiro.read fiscal.read"
}
```

5. O integrador envia `Authorization: Bearer <token>` nas chamadas seguintes,
   sempre usando o host da mesma loja.

O tenant e resolvido pelo host. O `tenant_id` do token precisa coincidir com o
tenant resolvido; portanto, uma credencial de uma loja nao funciona no dominio de
outra. Nao aceite `tenant_id` informado pelo cliente em query string ou payload.

## Escopos

| Escopo | Acesso |
| --- | --- |
| `financeiro.read` | DRE, capital de giro, agenda de caixa, estoque inteligente, configuracao e contas a receber |
| `financeiro.write` | Configuracao financeira e manutencao de contas a receber |
| `fiscal.read` | Saude fiscal, configuracao, IBPT, naturezas, notas, conciliacao, alertas, regras e XMLs |
| `fiscal.write` | Configuracao, certificado, sincronizacao IBPT, naturezas, emissao, cancelamento e reprocessamentos |

Escopos de escrita nao concedem leitura automaticamente. Rotas sem
`RequireIntegrationScope` recusam tokens tecnicos por padrao. Isso exclui gestao
de usuarios, clientes de integracao, convites do contador, operacoes globais da
plataforma e outras areas internas.

## Gestao das credenciais

- `GET /api/integrations/scopes`: lista escopos reconhecidos.
- `GET /api/integrations/clients`: lista clientes da loja sem expor segredos.
- `POST /api/integrations/clients`: cria cliente e mostra o segredo uma vez.
- `POST /api/integrations/clients/{id}/rotate`: troca o segredo e invalida tokens anteriores.
- `DELETE /api/integrations/clients/{id}`: revoga o cliente e seus tokens.

Somente administradores humanos podem administrar clientes. Segredos sao
armazenados como hash BCrypt e nunca entram em Git, logs, ZIPs ou releases.

O painel da plataforma tambem expoe as mesmas operacoes sob
`/api/platform/tenants/{tenantId}/integration-clients`, protegido pelas permissoes
de leitura/gestao de tenants. Esse e o caminho usado por sistemas externos, que
nao possuem admin ou painel local na 3ESysten.

## Verificacao de tenant externo

- `GET /api/integrations/capabilities/financeiro` valida `financeiro.read`.
- `GET /api/integrations/capabilities/fiscal` valida `fiscal.read`.

Essas rotas leem apenas o catalogo central. Elas confirmam tenant, tipo, modulos e
residencia dos dados sem consultar um schema vazio. Rotas operacionais da loja
continuam locais no sistema externo; servicos centrais especificos devem ganhar
contratos proprios antes de serem consumidos.

## Controles operacionais

- Tokens duram 15 minutos e nao possuem refresh token.
- O endpoint de token tem limite de 10 requisicoes por minuto por origem.
- Cada request confirma no catalogo que o cliente continua ativo e na versao atual.
- Rotacao ou revogacao invalida imediatamente tokens ainda nao expirados.
- Falha de credencial ou tenant retorna `401`; falta de escopo retorna `403`.
- Em producao, use apenas HTTPS e guarde o segredo em um cofre de credenciais.

Operacoes de escrita devem usar os identificadores idempotentes existentes no
dominio. A integracao com o Soft Nerd deve primeiro validar leitura em homologacao;
emissao, cancelamento e demais mutacoes fiscais entram depois, com casos de teste
de repeticao, timeout e reconciliacao.
