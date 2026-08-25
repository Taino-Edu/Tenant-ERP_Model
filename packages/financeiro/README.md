# Pacote Financeiro

Bundle de fontes do Financeiro inteligente do Tenant-ERP. Ele preserva backend,
frontend, migracoes, testes e os arquivos compartilhados necessarios para entender
os pontos de integracao.

## Superficies principais

- `GET /api/analytics/financeiro`: DRE e indicadores do periodo.
- `GET /api/analytics/financeiro/capital-giro`: necessidade de capital de giro.
- `GET /api/analytics/financeiro/agenda-caixa`: projecao de entradas e saidas.
- `GET /api/analytics/financeiro/estoque-inteligente`: capital parado e cobertura.
- `GET|PUT /api/financial-config`: metas, custos fixos e parametros gerenciais.
- `/api/contas-receber`: lancamentos, conciliacao e integracoes financeiras.
- `POST /api/integrations/services/financeiro/analisar`: analise stateless de um
  resumo agregado, com margem, markup, ponto de equilibrio e recomendacoes.

O pacote inclui autenticacao servidor-a-servidor por `client_credentials`. Consulte
`docs/arquitetura/INTEGRACAO-API-MULTITENANT.md` para criar credenciais por loja e
limitar o acesso com `financeiro.read` e `financeiro.write`.

O endpoint integrado nao persiste o resumo recebido. O sistema de origem continua
como dono de vendas, clientes e estoque; somente totais do periodo e produtos
agregados sao processados durante a requisicao.

## Como gerar

Na raiz do repositorio:

```powershell
.\packages\export-module.ps1 -Module financeiro
```

O ZIP sera criado em `output/packages/TenantERP-financeiro-source.zip` com um
inventario SHA-256.

## Integracao em outro projeto

Este e um pacote de fontes, nao um NuGet independente. `Program.cs`,
`AppDbContext.cs`, `frontend/lib/api.ts` e `manualContent.ts` entram como referencias
de integracao: compare e registre servicos, entidades, configuracoes e rotas no
projeto de destino em vez de substituir esses arquivos inteiros.

O modulo espera PostgreSQL, EF Core 8, valores monetarios em centavos, tenant
resolvido no request e os modelos de venda, estoque, crediario e transacoes externas.
Rode as migrations e os testes incluidos antes de habilitar as telas.
