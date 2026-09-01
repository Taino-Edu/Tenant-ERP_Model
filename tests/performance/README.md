# Auditoria de carga do banco

Este diretório contém a massa sintética e os workloads reproduzíveis usados na
auditoria de lançamento. Eles devem rodar **somente** no container/banco de QA.

Massa inicial recomendada para notebook de 2 núcleos:

- 10.000 usuários;
- 20.000 produtos;
- 50.000 comandas;
- 4 itens por comanda (200.000 itens);
- 50.000 vendas avulsas.

Segundo estágio usado para validar crescimento de volume no mesmo notebook:

- 25.000 usuários;
- 50.000 produtos;
- 150.000 comandas;
- 4 itens por comanda (600.000 itens);
- 150.000 vendas avulsas.

O `seed-load.sql` é idempotente e identifica tudo por `LOAD_` e UUIDs derivados
de `load-*`. Os workloads cobrem catálogo, histórico por cliente e as consultas
mais caras do dashboard administrativo.

Exemplo de execução no container `qa_erp_pg`:

```powershell
docker cp tests/performance/seed-load.sql qa_erp_pg:/tmp/seed-load.sql
docker exec qa_erp_pg psql -U qa -d qa_erp `
  -v schema=tenant_santuario_nerd -v users=10000 -v products=20000 `
  -v orders=50000 -v items_per_order=4 -v sales=50000 `
  -f /tmp/seed-load.sql
```

Para ampliar uma massa existente, repita o comando com os valores do segundo
estágio. Os UUIDs determinísticos e `ON CONFLICT DO NOTHING` fazem o script
inserir somente o delta.

Os relatórios gerados pela auditoria ficam fora do Git quando contêm logs brutos;
o resumo reproduzível e os achados confirmados devem ser documentados no PR.

## Frontend público, cache e metadados

O runner abaixo mede home, `robots.txt` e `sitemap.xml` sem autenticação. Por
padrão ele recusa destinos remotos para evitar carga acidental em produção:

```powershell
.\tests\performance\public-web-load.ps1 `
  -BaseUrl http://exemplosvisual.localhost:3000 `
  -Concurrency 4 -Iterations 3
```

Além de status, bytes e requisições por segundo, o resultado mostra
`Cache-Control`, `Content-Type`, erros e latência p50/p95/p99 por requisição
(inclui leitura do corpo; percentis pelo método nearest-rank). Requer PowerShell 7.
Caminhos fora da origem são recusados e redirecionamentos não são seguidos;
um 3xx aparece no resultado, não representa sucesso da página de destino.
Falhas de conexão/timeout entram na contagem de erros sem ocultar o restante
do lote. Avalie o campo `errors`, não apenas o exit code do script.
Poucas amostras servem para smoke, não para estimar capacidade máxima.
Para um ambiente remoto autorizado, use
`-AllowRemote`, concorrência baixa e uma janela combinada com a operação.

Para medir a API sem gravar credenciais no repositório, defina a senha somente
no processo atual e use o runner HTTP. O teste faz login, dispara lotes realmente
concorrentes e resume throughput, status e tamanho médio das respostas:

```powershell
$securePassword = Read-Host "Senha do usuário QA" -AsSecureString
$credential = New-Object System.Management.Automation.PSCredential('load-test', $securePassword)
$env:LOAD_TEST_PASSWORD = $credential.GetNetworkCredential().Password
.\tests\performance\http-load.ps1 -Concurrency 8 -Iterations 3 -Compressed
Remove-Item Env:LOAD_TEST_PASSWORD
```
