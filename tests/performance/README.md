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

Para medir a API sem gravar credenciais no repositório, defina a senha somente
no processo atual e use o runner HTTP. O teste faz login, dispara lotes realmente
concorrentes e resume throughput, status e tamanho médio das respostas:

```powershell
$env:LOAD_TEST_PASSWORD = Read-Host -MaskInput
.\tests\performance\http-load.ps1 -Concurrency 8 -Iterations 3 -Compressed
Remove-Item Env:LOAD_TEST_PASSWORD
```
