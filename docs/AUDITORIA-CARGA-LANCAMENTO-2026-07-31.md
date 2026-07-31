# Auditoria de carga para lançamento — 31/07/2026

## Escopo e segurança

A auditoria foi executada exclusivamente no PostgreSQL de QA local
(`qa_erp_pg`, banco `qa_erp`) e no tenant `santuario-nerd`. Nenhum dado foi
enviado a serviços externos e nenhum banco de produção foi acessado.

Ambiente usado para os limites deste relatório:

- Intel Pentium Gold G6400, 2 núcleos / 4 processadores lógicos;
- 15,8 GB de RAM compartilhados com os demais processos do notebook;
- PostgreSQL 16 em Docker, `shared_buffers=128MB`, `max_connections=100`;
- API ASP.NET Core 8 executada localmente.

## Massa sintética

O script idempotente `tests/performance/seed-load.sql` criou no schema isolado:

| Entidade | Quantidade |
| --- | ---: |
| Clientes | 10.000 |
| Produtos | 20.000 |
| Comandas | 50.000 |
| Itens de comanda | 200.000 |
| Vendas avulsas | 50.000 |

Tempo de carga: **59,85 s**. O banco cresceu de 17 MB para 109 MB.

Um segundo estágio ampliou a mesma massa, sem duplicar registros existentes:

| Entidade | Quantidade ampliada |
| --- | ---: |
| Clientes | 25.000 |
| Produtos | 50.000 |
| Comandas | 150.000 |
| Itens de comanda | 600.000 |
| Vendas avulsas | 150.000 |

O delta entrou em **89,09 s** e o banco chegou a 311 MB. Nessa escala, comandas
fechadas em 30 dias levaram 15,29 ms, o top de produtos corrigido sobre 600 mil
itens levou 438,30 ms e as primeiras 50 linhas do catálogo ordenado levaram
0,06 ms com cache aquecido. A matriz `pgbench` continuou com zero falhas:
leitura chegou a 786 TPS em c=16; o dashboard corrigido atingiu 7,74 TPS em c=4
e passou a saturar a CPU acima desse ponto.

## Banco de dados

As consultas críticas foram medidas com `EXPLAIN (ANALYZE, BUFFERS)` e com
`pgbench`, antes e depois dos índices desta alteração.

| Cenário | Antes | Depois | Resultado |
| --- | ---: | ---: | ---: |
| Comandas fechadas, 30 dias | 27,8 ms | 6,27 ms | 4,4x mais rápida |
| Top produtos, 200 mil itens (consulta anterior) | 230 ms | 112–113 ms | ~2x mais rápida |
| Catálogo, `pgbench` c=8 | 66,72 TPS | 916,69 TPS | 13,7x mais throughput |
| Dashboard final corrigido, `pgbench` c=4 | 6,28 TPS | 7,74 TPS | 1,23x mais throughput |
| Dashboard final corrigido, `pgbench` c=4 | 637,44 ms | 516,66 ms | 19% menos latência |

Todos os degraus de `pgbench` concluíram com **zero transações falhas**.
Foram adicionados índices compostos para os filtros reais de clientes,
catálogo e comandas, além do intervalo temporal dos itens de comanda.

A revisão final corrigiu a janela do top de produtos: ela agora considera
somente comandas com status `Fechada` e usa `closed_at`, em vez de inferir a
venda pela data de inclusão do item. Por isso, os números finais dessa consulta
não são diretamente comparáveis com a medição anterior, que processava outra
semântica.

## API

### Problema confirmado

O dashboard materializava 60 dias de vendas completas, incluindo o JSON de
itens, para calcular totais simples em memória. Sob concorrência, o working set
da API chegou a aproximadamente **673 MB**. O endpoint de produtos também
devolvia 20 mil registros em uma resposta JSON de **10,65 MB** sem compressão.

### Correção

- totais, contagens, tickets e clientes ativos passaram a ser agregados no banco;
- apenas os campos escalares das vendas do dia são materializados;
- o top 5 de produtos é calculado em uma consulta PostgreSQL parametrizada;
- respostas JSON agora aceitam Brotli/Gzip no nível mais rápido;
- o catálogo público é projetado diretamente para `ProductPublicDto` no banco
  e serializado de forma assíncrona, sem criar uma lista de entidades completas;
- os indicadores do dashboard foram comparados campo a campo e permaneceram
  idênticos aos valores anteriores na mesma massa.

A consulta nova não interpola entrada do usuário: a data é enviada como
parâmetro pelo EF Core. As tabelas sem schema explícito continuam isoladas pelo
`search_path` validado do `TenantConnectionInterceptor`, portanto a consulta
permanece dentro do tenant resolvido para a requisição.

Resultados observados após a correção:

| Cenário | Resultado |
| --- | ---: |
| Dashboard aquecido, requisição única | 0,53–0,80 s |
| Dashboard anterior, requisição única | 1,77–3,14 s |
| Dashboard comprimido | 395 bytes (antes 1.094 bytes) |
| Catálogo comprimido | 1,04 MB (antes 10,65 MB) |
| Dashboard, 64 simultâneas | 64/64 HTTP 200; 6,25 req/s |
| Catálogo, 4 simultâneas | 4/4 HTTP 200; 1,90 req/s |
| Working set após a rampa | ~381 MB (antes ~673 MB) |

Na massa ampliada, o JSON bruto do catálogo chegou a 26,63 MB e foi reduzido a
2,60 MB por gzip. Antes do streaming, uma chamada já levou a API a ~413 MB e a
rampa foi cancelada antes de c=2 por falta de memória global. Depois da projeção
e streaming, c=1, c=2 e c=4 concluíram com 7/7 HTTP 200; em c=4 o working set
ficou em ~356 MB, mesmo servindo 50 mil produtos.

O throughput do dashboard começou a estabilizar entre 16 e 64 chamadas
simultâneas, coerente com a CPU de dois núcleos. A rampa foi interrompida em 64
quando a memória livre global chegou a 0,48 GB, antes de causar paginação
excessiva ou afetar os outros processos do notebook.

## Confiabilidade da suíte

Durante a auditoria, duas execuções simultâneas da suíte compartilharam o banco
de testes e usaram os mesmos nomes de schema. Uma execução podia executar
`DROP SCHEMA` enquanto a outra ainda o utilizava, gerando falhas intermitentes.
A fábrica de testes agora inclui um identificador único por processo em todos os
schemas, inclusive nos testes diretos do interceptor multi-tenant. Assim,
auditorias paralelas deixam de interferir entre si.

Gate final em PostgreSQL real: **413 aprovados, 0 falhas, 0 ignorados**, em
57 s. O teste de auditoria que dependia de uma tolerância fixa de cinco
segundos também passou a validar o timestamp entre o início e o fim da operação,
eliminando falso negativo quando o notebook está sob carga. Uma execução
isolada adicional confirmou que a limpeza automática não deixou novos schemas.

## Risco residual e próximas ações

O contrato atual dos endpoints de catálogo ainda retorna a coleção completa.
A compressão e o streaming reduziram rede e memória do servidor, mas o navegador
ainda precisa materializar todos os produtos. Paginação exige mudança coordenada
nas telas de loja, estoque, comanda, reservas e relatórios; deve ser entregue em alteração
separada e testada de ponta a ponta para não quebrar o fluxo de venda antes do
lançamento.

Antes do deploy:

1. aplicar a migração em staging e observar o tempo de criação dos índices;
2. executar smoke test de login, dashboard, catálogo, comanda e venda avulsa;
3. confirmar compressão no proxy/CDN com `Content-Encoding: br` ou `gzip`;
4. monitorar CPU, memória, conexões PostgreSQL e p95 nos primeiros tenants;
5. manter rollback da aplicação disponível; os novos índices são aditivos.
