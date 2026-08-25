# Pacote Fiscal API

Bundle de fontes do motor fiscal REST do Tenant-ERP: configuracao tributaria,
certificado A1, NFC-e, contingencia, cancelamento, inutilizacao, IBPT, conciliacao,
alertas, apuracao e portal do contador.

## APIs existentes

- `/api/fiscal`: operacao fiscal da loja e emissao de NFC-e.
- `/api/platform/ibpt`: importacao da tabela IBPT compartilhada.
- `/api/contador-portal`: acesso cross-tenant autorizado para o contador.
- `GET /api/integrations/services/fiscal/ibpt/{ncm}?uf=SP&importado=false`:
  consulta autenticada ao catalogo IBPT global, sem copiar a tabela para cada loja.

O Swagger da aplicacao e os atributos dos controllers sao a fonte do contrato
HTTP completo. Segredos, certificados e tokens nunca devem ser incluidos no ZIP.
Sistemas externos usam `client_credentials` por loja, com os escopos `fiscal.read`
e `fiscal.write`, conforme `docs/arquitetura/INTEGRACAO-API-MULTITENANT.md`.

## Como gerar

Na raiz do repositorio:

```powershell
.\packages\export-module.ps1 -Module fiscal
```

O ZIP sera criado em `output/packages/TenantERP-fiscal-source.zip` com inventario
SHA-256.

## Integracao em outro projeto

Este pacote permite copiar o modulo para outro ASP.NET Core 8, mas nao substitui
arquivos compartilhados inteiros. Use `Program.cs` e `AppDbContext.cs` como mapa
para registrar servicos, entidades, interceptores de tenant e jobs de fundo.

Dependencias centrais: PostgreSQL, EF Core 8, Zeus.Net.NFe.NFCe, certificado A1,
schemas XSD versionados, contexto de tenant e criptografia de configuracoes. O
projeto de destino precisa manter idempotencia de emissao, numeracao atomica,
consulta de resultado incerto e validacao XSD antes de transmitir.
