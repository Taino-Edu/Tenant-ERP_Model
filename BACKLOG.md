# Backlog operacional — Tenant-ERP

> **Fonte de verdade a partir de 2026-08-11.** Esta parte do documento é o backlog
> vigente. O conteúdo anterior foi preservado no final como histórico e não deve
> ser usado sozinho para decidir o próximo trabalho.
>
> **Base funcional auditada:** commit `66c081b`, código local, migrações, testes,
> documentação, branches e worktrees registradas. Não confundir “há uma branch”
> com “a funcionalidade está pronta na main”.

## Como manter este backlog

- Todo item ativo tem ID, prioridade, estado, evidência e critério de conclusão.
- Estados: `PRONTO PARA FAZER`, `EM EXECUÇÃO`, `BLOQUEADO`, `VALIDAR` e `CONCLUÍDO`.
- Só pode haver uma frente principal `EM EXECUÇÃO` por equipe/agente.
- Código pronto em branch/worktree, mas fora da `main`, continua `VALIDAR`.
- Plano ou documento não significa implementação.
- Item concluído sai da fila ativa e entra no resumo de entregas; o histórico
  detalhado permanece abaixo para rastreabilidade.
- Atualizar a data e a evidência quando o estado mudar. Evitar expressões vagas
  como “melhorar CRM” sem um critério observável de aceite.

## Resumo executivo

### Situação confirmada

- A `main` remota e `codex/atalhos-manual-inteligente` incluem `66c081b`.
- A worktree principal estava limpa no início desta auditoria.
- Backend e frontend compilam; o lint do frontend passou sem avisos em 2026-08-11.
- Os 17 testes focados de billing/comissões passaram. Após a correção de
  `QA-001`, a suíte completa passou novamente em 2026-08-11 com 775 testes,
  zero falhas e zero ignorados.
- Multi-tenant, billing ciclo 1, leads, prospecção, diretório público, restaurante,
  comandas e indicações/comissões já têm implementação na `main`.
- Consentimento de cookies versionado, documentos legais, sitemap, robots,
  metadados sociais e bloqueio de indexação das áreas privadas estão na `main`.
- O primeiro ciclo da nova prospecção foi implementado e validado em 2026-08-11:
  cache PostgreSQL, histórico retomável, atualização explícita, estados dos
  candidatos e busca OSM por área administrativa sem limite fixo.
- O bot interno de captação entrou no segundo ciclo em 2026-08-11: campanhas
  agendadas, execuções auditáveis, pausa/retomada, fila de revisão humana e
  enriquecimento incremental persistente já estão implementados.

### Direção recomendada

1. Corrigir a fundação da prospecção (`PROS-001` e `PROS-002`): pesquisa
   persistente, cobertura OSM completa, histórico e deduplicação.
2. Consolidar o CRM operacional (`CRM-001` a `CRM-004`) e conectar a origem da
   prospecção às oportunidades e atividades.
3. Adicionar enriquecimento governado (`PROS-003`) e a camada analítica
   (`DATA-001` a `DATA-003`).
4. Implementar cobrança recorrente real da plataforma (`PAY-001`).
5. Automatizar pesquisa com bot (`PROS-004`) somente após proveniência,
   privacidade, quotas e revisão humana estarem prontas.

## P0 — segurança, integridade e liberação

### SEC-001 — Sanitizar HTML dos comprovantes

- **Estado:** `CONCLUÍDO` em 2026-08-11
- **Evidência:** há mudanças não commitadas na worktree
  `.claude/worktrees/musing-solomon-133b2e` em:
  - `frontend/app/admin/venda-avulsa/page.tsx`;
  - `frontend/components/admin/comanda/shared.ts`.
- **O que fazem:** escapam nome da loja, cliente, mesa e produtos antes de
  interpolá-los em HTML enviado a `document.write()`.
- **Risco atual:** conteúdo cadastrado pode entrar cru no template de impressão.
- **Entregue:** helper central `frontend/lib/html.ts` aplicado a todos os usos de
  `document.write()` encontrados: venda avulsa, relatório diário, comanda,
  crediário e impressão de QR Codes, incluindo loja, cliente, mesa, produto,
  forma de pagamento, URL e atributos HTML.
- **Validação:** lint e build de produção aprovados; 2 testes Playwright cobrem
  `<script>`, `<`, `>`, `&`, aspas, apóstrofo, acentos e valores formatados.
- **Observação:** a worktree original continua intacta até `REP-001`; a correção
  já foi portada para a branch atual e não depende mais dela.

### QA-001 — Descobrir por que a suíte unitária completa não termina

- **Estado:** `CONCLUÍDO` em 2026-08-11
- **Causa:** o timeout de 180 segundos era menor que o tempo antigo da suíte e
  havia 146–290 schemas órfãos sendo removidos individualmente antes do primeiro
  teste. Execuções paralelas ainda disputavam a mesma limpeza.
- **Correção:** lock consultivo no PostgreSQL, remoção em lotes, descarte de schema
  junto com cada `AppDbContext` de teste e `using` explícito nas classes que não
  liberavam o contexto.
- **Validação:** 750/750 testes aprovados em 1min52s, zero falhas, zero ignorados e
  zero schemas `test_%` restantes ao final. Orçamento local recomendado: 3 minutos;
  CI pode usar margem de 5 minutos para máquinas mais lentas.

### FIS-001 — Homologação fiscal real

- **Estado:** `BLOQUEADO`
- **Dependência externa:** contador, certificado/CSC e ambiente SEFAZ do
  estabelecimento.
- **Evidência:** `docs/STATUS-GO-LIVE-NFCE.md` e
  `docs/GO-LIVE-FISCAL-2026-07-25.md`.
- **Falta:** executar e registrar autorização, rejeição, contingência/reenvio,
  cancelamento, inutilização, impressão 58/80 mm, leitura do QR e abertura do XML
  pelo sistema do contador.
- **Concluído quando:** checklist real assinado, evidências anexadas e nenhuma
  pendência fiscal antiga no staging/produção.

### FIS-002 — Proteção contra consumo indevido na Distribuição DF-e

- **Estado:** `CONCLUÍDO` em 2026-08-11
- **Motivador:** `cStat 656` podia ser provocado por clique manual concorrendo com
  o job ou por duas instâncias da API; a resposta ainda chegava ao frontend como
  HTTP 200 e produzia toast verde com zero notas.
- **Entregue:** estado persistente por schema do tenant + CNPJ + ambiente, lease
  atômica no PostgreSQL, cooldown preventivo/`137`/`656` de 1h05, quota interna
  de 18 consultas pontuais por hora, NSU monotônico, `429 + Retry-After`, `409`,
  contagem regressiva e logs com CNPJ mascarado. O painel também mostra a saúde
  do serviço de autorização da UF e oferece “Testar SEFAZ”; essa medição usa
  `NfeStatusServico`, com cache curto, sem tocar na Distribuição DF-e.
- **Migration:** preserva o NSU existente e aplica cooldown inicial conservador
  aos tenants que já possuem configuração fiscal.
- **Validação:** migration idempotente gerada; 8 testes fiscais no PostgreSQL real;
  suíte completa 758/758; lint e build do frontend aprovados; 4 testes de helpers
  puros aprovados em configuração Playwright sem servidor.
- **Limite operacional:** outro software que consulte o mesmo CNPJ fora deste ERP
  não compartilha nossa lease/NSU; cada CNPJ deve ter um único controlador de
  Distribuição DF-e ou coordenação explícita entre os sistemas.

### REP-001 — Reconciliar worktrees e branches antigas

- **Estado:** `PRONTO PARA FAZER`
- **Não commitado:** somente a worktree citada em `SEC-001`.
- **Worktrees limpas com commits fora da main:**
  - `C:/tmp/octus-security-verify` — `fc72d29`, gestão/segurança da equipe;
  - `C:/tmp/Tenant-ERP-load-audit` — performance e auditoria de carga;
  - `C:/tmp/Tenant-ERP-pr38-reconcile` — correção VAPID;
  - `C:/tmp/Tenant-ERP-swagger` — Swagger em produção.
- **Branches locais não integralmente incorporadas:** planos técnicos, integrações,
  isolamento, VAPID/Swagger e auditoria de carga. Algumas divergem da `main` e não
  devem ser mescladas em lote.
- **Próxima ação:** revisar uma por vez contra a `main`, classificar como
  `incorporar`, `substituída` ou `arquivar`, e só depois remover worktrees.
- **Concluído quando:** nenhuma worktree tem alteração sem dono e toda branch não
  incorporada tem decisão registrada.

## P1 — CRM comercial de padrão de mercado

### CRM-001 — Modelo comercial canônico

- **Estado:** `PRONTO PARA FAZER`
- **Já existe:** lead, origem, status simples, score, presença digital,
  faturamento estimado, notas, prospecção e conversão para tenant.
- **Falta:**
  - separar `Conta/Empresa`, `Contato`, `Lead` e `Oportunidade`;
  - pipeline configurável com etapa, probabilidade, valor, previsão de fechamento
    e motivo de ganho/perda;
  - responsável comercial e fila sem responsável;
  - deduplicação por telefone, e-mail, documento e estabelecimento;
  - histórico imutável de mudanças de etapa/responsável;
  - consentimento, finalidade e origem do dado para LGPD.
- **Critério de conclusão:** um lead pode virar oportunidade, avançar pelo funil,
  ser ganho/perdido com histórico completo e converter em tenant sem perder a
  atribuição da origem.

### CRM-002 — Atividades, tarefas e linha do tempo

- **Estado:** `PRONTO PARA FAZER`
- **Falta:** ligações, mensagens, reuniões, comentários internos, tarefas,
  vencimento, lembrete, resultado do contato e próxima ação.
- **Regra:** notas livres não substituem eventos estruturados.
- **Critério de conclusão:** a tela do lead/oportunidade mostra uma linha do tempo
  única e o gestor consegue listar tarefas atrasadas, de hoje e futuras por vendedor.

### CRM-003 — Origem, campanhas e indicações antes da conversão

- **Estado:** `PRONTO PARA FAZER`
- **Já existe:** origem textual no lead e controle de vendedor/comissão ligado ao
  tenant; o vínculo aceita `SourceLeadId` no backend.
- **Falta:** selecionar o vendedor indicador ainda no lead, campanhas/UTM,
  primeiro/último toque, canal e regras de atribuição visíveis na UI.
- **Critério de conclusão:** a origem acompanha o contato até receita e comissão,
  sem preenchimento manual duplicado após a conversão.

### CRM-004 — Gestão de carteira e previsão comercial

- **Estado:** `PRONTO PARA FAZER`
- **Falta:** carteira por vendedor, forecast ponderado, aging por etapa, motivos de
  perda, metas, conversão por origem e tempo médio até fechamento.
- **Critério de conclusão:** o gestor enxerga pipeline, previsão e gargalos por
  período/vendedor/origem com definições de métricas documentadas.

## P1 — prospecção assistida e inteligência gratuita

### PROS-001 — Pesquisa persistente e espaço de trabalho

- **Estado:** `PRONTO PARA FAZER` (fundação entregue; complementos pendentes)
- **Problema confirmado:** a tela atual guarda categoria, cidade e resultados
  apenas no estado React. Ao sair, atualizar a página ou iniciar outra busca, o
  trabalho desaparece. O backend não possui cache de prospecção.
- **Entregar:**
  - `ProspectingSearch` persistida no catálogo com consulta normalizada,
    filtros, responsável, datas, fonte, versão e estado;
  - `ProspectCandidate` persistido com snapshot da fonte, `sourceId`, dados
    observados, dados estimados separados, `lastSeenAt` e validade;
  - cache em PostgreSQL, não apenas em memória, por fonte + cidade + categoria +
    filtros, com opção explícita de atualizar;
  - histórico, pesquisas favoritas e retomada da última sessão;
  - estados `novo`, `selecionado`, `descartado`, `já é lead`, `já é cliente` e
    `desatualizado`;
  - seleção em lote, ordenação, filtros e conversão em lead sem perder o
    contexto da pesquisa;
  - deduplicação por fonte/ID, CNPJ, domínio, telefone e nome+endereço.
- **Entregue neste ciclo:**
  - tabelas catalogadas `ProspectingSearch` e `ProspectCandidate`, com migração,
    chave normalizada única, validade de sete dias e proteção contra pesquisas
    concorrentes duplicadas;
  - histórico de pesquisas, retomada do snapshot, idade dos dados e botão de
    atualização forçada na interface;
  - estados persistentes `New`, `Selected`, `Discarded`, `Lead`, `Customer` e
    `Stale`, preservando conversões durante novas coletas;
  - conversão ligada ao candidato e reconciliação automática com leads antigos
    pelo ID da fonte.
- **Falta:** favoritos/responsável, filtros e seleção em lote, comandos de
  selecionar/descartar e deduplicação secundária por CNPJ, domínio, telefone e
  nome+endereço.
- **Critério de conclusão:** uma pesquisa pode ser fechada e retomada sem nova
  chamada externa; atualizar preserva decisões anteriores e mostra a idade dos
  dados; nenhum candidato convertido reaparece como novo.

### PROS-002 — Cobertura completa do OpenStreetMap

- **Estado:** `PRONTO PARA FAZER` (primeiro incremento entregue em 2026-08-11)
- **Problemas confirmados no código atual:**
  - consulta somente `node` e `way`, ignorando `relation`;
  - `out center 60` limita silenciosamente a resposta a 60 elementos;
  - usa `bbox` da cidade, que pode incluir municípios vizinhos;
  - o dicionário fixo cobre poucas categorias;
  - categoria desconhecida cai em regex no `name` do estabelecimento, que
    raramente representa o tipo real do negócio;
  - não há paginação/varredura, indicador de cobertura ou resultado parcial.
- **Entregar:**
  - consulta `nwr` por área administrativa do município e fallback geográfico;
  - varredura por quadrantes quando a área/volume exigir, com deduplicação;
  - taxonomia configurável de segmentos → sinônimos → tags OSM/CNAE;
  - múltiplas tags por segmento em `shop`, `amenity`, `office`, `craft`,
    `tourism`, `leisure` e combinações relevantes;
  - total, fonte, cobertura, limite e falhas parciais visíveis na interface;
  - cache obrigatório, atribuição OSM/ODbL e respeito às quotas públicas.
- **Entregue neste ciclo:** consulta `nwr` (inclui relações), remoção do teto
  silencioso de 60 resultados, área administrativa do município com fallback
  para `bbox`, mais segmentos e opção ampla “Todos os negócios”. A interface
  informa fonte, cache e data da coleta.
- **Falta:** varredura por quadrantes, taxonomia configurável fora do código,
  indicador quantitativo de cobertura/resultado parcial e testes de referência
  por município e segmento.
- **Critério de conclusão:** testes conhecidos por município/segmento não perdem
  resultados por tipo OSM ou limite fixo, e o operador sabe quando a busca foi
  completa, parcial, cacheada ou atualizada.

### PROS-003 — Enriquecimento gratuito, verificável e multi-fonte

- **Estado:** `PRONTO PARA FAZER` (primeiro incremento entregue em 2026-08-11)
- **Depende de:** `PROS-001`, `PROS-002` e definição inicial de segmentos/UFs.
- **Fontes e ferramentas para o desenho inicial:**
  - dados abertos mensais do CNPJ/Receita Federal, filtrados por situação ativa,
    município, CNAE, porte e data de abertura;
  - códigos e indicadores municipais do IBGE/SIDRA;
  - OSM para presença física, contato e localização;
  - site oficial do candidato para `schema.org`, telefone, e-mail corporativo,
    redes sociais e tecnologias, respeitando `robots.txt` e limites;
  - Lighthouse em fila própria para desempenho, SEO e qualidade do site;
    MapLibre para visualização geográfica;
  - DuckDB opcional somente no ETL dos arquivos grandes da Receita, carregando
    no PostgreSQL operacional o recorte necessário.
- **Regras:** não usar IA para inventar faturamento, porte, contato ou CNPJ;
  separar `observado`, `derivado` e `estimado`, sempre com fonte, data e
  confiança; não importar QSA/CPF para prospecção.
- **Entregue neste ciclo:** cada candidato passou a guardar estado, data, fonte
  e confiança do enriquecimento. Novas coletas OSM/site atualizam o snapshot;
  o enriquecimento de abordagem sob demanda também fica persistido e reaparece
  na fila de captação.
- **Entregue também:** observações versionadas por campo, preservando valor
  anterior, valor observado, fonte, confiança e data.
- **Falta:** extrator governado de site, integração com recortes públicos
  CNPJ/IBGE e proteção explícita para dados corrigidos manualmente.
- **Critério de conclusão:** cada campo enriquecido mostra origem e atualização;
  o score é explicável; correções manuais sobrevivem à sincronização.

### PROS-004 — Bot pesquisador e qualificador com revisão humana

- **Estado:** `VALIDAR` desde 2026-08-11
- **Depende de:** `PROS-001` a `PROS-003`, `CRM-001`, política de privacidade da
  prospecção e teste de balanceamento de legítimo interesse.
- **Escopo recomendado:** job interno usando `BackgroundService`/fila existente,
  não um robô que raspa Google Maps ou dispara mensagens. Deve:
  - executar pesquisas salvas em agenda e respeitar quotas por fonte;
  - detectar novos estabelecimentos e mudanças relevantes;
  - enriquecer e recalcular score de forma determinística;
  - montar uma fila diária priorizada com justificativa;
  - sugerir abordagem, próxima ação e responsável;
  - exigir aprovação humana antes de criar oportunidade ou iniciar contato.
- **Não fazer:** contato automático em massa, WhatsApp não oficial, scraping de
  fontes que proíbem automação, compra de listas, coleta de dados sensíveis ou
  reprocessamento de quem pediu oposição/descadastro.
- **Entregue neste ciclo:**
  - `BackgroundService` `.NET` com campanhas por cidade/segmento, frequência,
    pausa/ativação, execução manual e limite de candidatos priorizados;
  - fila persistente, trava contra execução concorrente da mesma campanha e
    histórico com início, término, resultado, novos candidatos e falha;
  - consumo deliberadamente serializado em uma campanha por minuto, mantendo
    cache e fallback das fontes já existentes;
  - fila de captação na interface, atualizada periodicamente, onde o operador
    enriquece e aprova a conversão em lead;
  - nenhuma criação de lead ou contato automático pelo worker.
  - lista de oposição por identificador da fonte, telefone e domínio, aplicada
    também às coletas futuras e acionável na fila pelo operador;
  - orçamento diário configurável por campanha e retentativas automáticas com
    backoff de 5, 15 e 45 minutos, sem criar execuções duplicadas;
  - histórico de mudanças por campo com valor anterior, novo valor, fonte,
    confiança e data, visível no cartão do candidato.
- **Falta para contato ativo, não para o robô de pesquisa:** política/legal
  validada, responsável sugerido e notificações. O worker continua proibido de
  iniciar contato automaticamente.
- **Critério de conclusão:** execução idempotente, auditável, com orçamento,
  quotas, lista de oposição, proveniência e aprovação humana demonstrados.

### PROS-005 — Operação de contato e cadência

- **Estado:** `BLOQUEADO`
- **Depende de:** `CRM-002`, `CRM-003` e `PROS-004`.
- **Falta decidir:** canais permitidos, horários, identidade do remetente,
  templates, frequência, SLA e regra de oposição.
- **Possibilidades gratuitas/open source:** templates e tarefas no próprio CRM;
  e-mail self-hosted com listmonk somente quando houver base legal e descadastro.
- **Critério de conclusão:** cada contato vira atividade, tem finalidade/base
  registradas, identifica origem, permite oposição e mede resposta.

### Referências verificadas para PROS-001 a PROS-005

- [Política pública do Nominatim](https://operations.osmfoundation.org/policies/nominatim/):
  máximo absoluto de 1 requisição/s, identificação da aplicação, atribuição,
  cache obrigatório em lote e proibição de uso pesado recorrente na instância
  pública; self-host é uma opção futura se o volume justificar.
- [Overpass API e instâncias públicas](https://wiki.openstreetmap.org/wiki/Overpass_API):
  serviço comunitário sem SLA, sujeito a timeout, memória, carga e rate limit.
- [Dados abertos do CNPJ — Receita Federal](https://www.gov.br/receitafederal/pt-br/acesso-a-informacao/dados-abertos/cadastros):
  fonte oficial gratuita para situação, CNAE, município, porte e abertura; os
  arquivos nacionais são grandes e exigem ETL, atualização e recorte próprios.
- [Localidades do Brasil — IBGE](https://www.ibge.gov.br/geociencias/organizacao-do-territorio/estrutura-territorial/27385-localidades.html):
  códigos e arquivos geográficos oficiais para municípios e localidades.
- [Guia da ANPD sobre legítimo interesse](https://www.gov.br/anpd/pt-br/assuntos/noticias/anpd-lanca-guia-orientativo-sobre-legitimo-interesse):
  antes de contato ativo, documentar finalidade, necessidade, balanceamento,
  salvaguardas, transparência e oposição; dado público continua sujeito à LGPD.

### REF-001 — Completar o controle de indicações e comissões

- **Estado:** `VALIDAR`
- **Já existe na main:** vendedores autônomos, percentuais separados de implantação
  e mensalidade, ciclos opcionais, dia de pagamento, agenda, baixa, MRR indicado e
  proteção contra duplicidade/reabertura após comissão paga.
- **Falta validar/decidir:**
  - regra padrão de duração da comissão mensal;
  - retenções/impostos e documento do prestador;
  - estorno/cancelamento formal depois de comissão paga;
  - exportação de extrato e comprovante de repasse;
  - acesso do próprio vendedor a um portal somente leitura;
  - política para mudança de vendedor de um cliente já com histórico.
- **Critério de conclusão:** regras aprovadas, migração aplicada e ciclo completo
  validado em staging: cliente paga → comissão vence → vendedor recebe → extrato.

## P1 — aquisição, indexação e mensuração

### MKT-001 — Colocar a 3E Systen no Google

- **Estado:** `BLOQUEADO` por verificação da conta Google/DNS.
- **Diagnóstico em 2026-08-11:** `https://3esysten.com.br` e o sitemap respondem
  `200` inclusive para Googlebot; `robots.txt`, canonical, metadata, JSON-LD e
  `index, follow` estão corretos. A busca `site:3esysten.com.br` não retorna
  páginas e não existe TXT `google-site-verification` no DNS.
- **Fazer:** criar propriedade de domínio no Search Console, adicionar o TXT no
  Cloudflare, enviar `/sitemap.xml`, inspecionar a home e solicitar indexação.
- **Depois:** Bing Webmaster Tools/IndexNow e Perfil da Empresa no Google se a
  operação atender presencialmente ou em área de serviço.
- **Critério de conclusão:** propriedade verificada, sitemap lido sem erro,
  página inicial indexada e consultas de marca monitoradas no Search Console.

### MKT-002 — Stack gratuita de analytics e mídia com consentimento

- **Estado:** `PRONTO PARA FAZER` após criação das contas/IDs.
- **Stack:** Google Tag Manager como ponto único; GA4; conversões do Google Ads;
  Meta Pixel; TikTok Pixel; LinkedIn Insight Tag; Microsoft Clarity.
- **Eventos mínimos:** `view_pricing`, `whatsapp_click`, `lead_submit`,
  `sign_up`, `trial_started` e `subscription_paid`, com UTM/origem persistida no
  CRM e deduplicação entre navegador e servidor.
- **Privacidade:** Analytics só após consentimento de análise; pixels e
  remarketing só após consentimento de marketing; revogação precisa interromper
  novas coletas.
- **Evolução:** Meta Conversions API, TikTok Events API e conversões aprimoradas
  do Google somente depois de eventos web e consentimento estarem validados.
- **Critério de conclusão:** Tag Assistant/pixel helpers sem erro, eventos de
  teste chegam uma única vez, origem aparece no lead e nenhuma tag opcional
  dispara após recusa.

### MKT-003 — Conteúdo orgânico por intenção comercial

- **Estado:** `PRONTO PARA FAZER`.
- **Entregar:** páginas próprias para ERP de restaurante, sistema para lojas,
  PDV com NFC-e, estoque, crediário e portal do contador; casos reais, FAQ e
  links internos; marca escrita consistentemente como `3E Systen` e produto
  `Octus`.
- **Critério de conclusão:** páginas úteis no sitemap, sem conteúdo duplicado,
  indexadas e recebendo impressões por consultas não ligadas apenas à marca.

## P1 — dados e inteligência de mercado

### DATA-001 — Camada analítica governada

- **Estado:** `PRONTO PARA FAZER`
- **Princípio:** não consultar tabelas transacionais soltas para responder perguntas
  de mercado. Criar uma camada de fatos/dimensões ou read models versionados.
- **Fatos mínimos:** aquisição de lead, mudança de etapa, atividade comercial,
  conversão, cobrança, pagamento, comissão, churn/suspensão e uso de módulos.
- **Dimensões mínimas:** tempo, tenant, segmento, cidade/UF, plano, origem/campanha,
  vendedor, etapa e produto/módulo.
- **Critério de conclusão:** dicionário de dados, donos, atualização, qualidade,
  retenção e consultas de referência versionadas.

### DATA-002 — KPIs comerciais e de SaaS

- **Estado:** `BLOQUEADO`
- **Depende de:** `CRM-001`, `CRM-003` e `DATA-001`.
- **Métricas:** leads qualificados, conversão por etapa/origem, ciclo de venda,
  CAC, receita por vendedor, MRR novo, expansão, contração, churn, LTV, payback,
  inadimplência e custo de comissão.
- **Critério de conclusão:** cada KPI tem fórmula, granularidade, janela temporal,
  fonte, responsável e teste contra uma amostra conhecida.

### DATA-003 — Enriquecimento e análise de mercado

- **Estado:** `BLOQUEADO`
- **Decisões necessárias:** regiões/segmentos prioritários, fontes autorizadas,
  orçamento e base legal/LGPD.
- **Possíveis fontes:** dados públicos (IBGE, Receita/CNPJ quando permitido,
  municípios), presença digital, campanhas próprias e provedores contratados.
- **Não fazer:** scraping ou compra de listas sem validar termos, qualidade,
  consentimento e finalidade.
- **Critério de conclusão:** catálogo de fontes com licença, custo, cobertura,
  atualização e qualidade; enriquecimento rastreável; análise por segmento/região
  sem misturar estimativa com dado observado.

## P1 — receita e pagamentos da plataforma

### PAY-001 — Cobrança recorrente real dos tenants

- **Estado:** `PRONTO PARA FAZER`
- **Já existe:** preço por tenant, implantação, mensalidades, competência,
  vencimento, baixa manual, MRR, inadimplência e comissões.
- **Não existe:** assinatura/cobrança automática do SaaS e suspensão automatizada.
- **Decisão pendente:** Mercado Pago OAuth, Banco Inter ou outro gateway para a
  mensalidade da plataforma. Não confundir com Pix das vendas dos lojistas.
- **Critério de conclusão:** cobrança idempotente, webhook validado, retry,
  conciliação, régua de inadimplência, suspensão segura, reativação e trilha de
  auditoria em sandbox e produção.

### PAY-002 — Mercado Pago para vendas dos lojistas

- **Estado:** `PRONTO PARA FAZER`
- **Evidência:** existe um plano detalhado em
  `docs/PLANO-PAGAMENTOS-MULTITENANT-MERCADO-PAGO.md`, mas o código atual só
  registra configuração; OAuth, criação de pagamento e webhook não existem.
- **Critério de conclusão:** fluxo OAuth por tenant, Pix, consulta autenticada,
  webhook idempotente e isolamento comprovado; Banco Inter continua funcionando.

## P1 — restaurante e comandas

### REST-001 — Validar em staging a nova estrutura de produção

- **Estado:** `VALIDAR`
- **Já existe na main:** comandas acopladas ao módulo Restaurante, comentários do
  cliente, áreas de produção, snapshots dos itens, fila e estados
  Recebido → Preparando → Pronto → Servido, além de SignalR.
- **Falta:** aplicar migração no ambiente, testar cozinha/salão em dispositivos
  simultâneos e confirmar que desligar Restaurante oculta comandas sem apagar dados.
- **Critério de conclusão:** roteiro E2E com mesa, cliente, caixa e produção;
  reconexão SignalR e reabertura sem perda/duplicidade.

## P2 — confiabilidade e operação

### OPS-001 — Monitoramento externo e alertas

- **Estado:** `PRONTO PARA FAZER`
- **Já existe:** `/health`, health check do banco e health checks de containers.
- **Falta:** monitor externo, alerta de indisponibilidade, latência e runbook.
- **Critério de conclusão:** alerta testado para API e frontend, com responsável e
  procedimento de recuperação.

### OPS-002 — Cloudflare Full (Strict)

- **Estado:** `BLOQUEADO`
- **Dependência:** certificado de origem no VPS e ajuste de nginx/Cloudflare.
- **Critério de conclusão:** Cloudflare → origem em HTTPS validado e renovação
  documentada, sem regressão nos subdomínios.

### OPS-003 — Domínio próprio sem Cloudflare do lojista

- **Estado:** `BLOQUEADO`
- **Já existe:** roteamento por domínio customizado.
- **Falta:** emissão/renovação TLS por domínio ou Cloudflare for SaaS.
- **Gatilho:** implementar quando houver demanda real ou decisão comercial.

### QA-002 — Error boundaries por área

- **Estado:** `PRONTO PARA FAZER`
- **Já existe:** boundaries raiz e `/admin`.
- **Falta:** `/plataforma` e `/cliente`, preservando seus layouts e ações de retry.

### QA-003 — Testes E2E essenciais

- **Estado:** `VALIDAR`
- **Correção do backlog antigo:** já existem cinco specs Playwright em
  `frontend/tests/`; não é mais verdade que “não há nenhum teste escrito”.
- **Falta:** confirmar execução no CI e cobrir login, venda, fechamento de comanda,
  comissão e isolamento de tenant com dados determinísticos.

### QA-004 — Upgrade do Next.js

- **Estado:** `BLOQUEADO`
- **Atual:** Next `14.2.35`, React `18.3.1`.
- **Regra:** levantar vulnerabilidades atuais e seguir guia/codemods oficiais;
  não misturar upgrade major com feature de CRM ou pagamento.

## P2 — experiência e manutenção

### UX-001 — Responsividade e design system

- **Estado:** `PRONTO PARA FAZER`
- **Falta:** migrar modais inline, aumentar adoção de `Button`, `EmptyState`,
  `Spinner`, `Badge`, `PageHeader` e `StatCard`, remover cores hardcoded e validar
  as telas de maior tráfego em celular real.
- **Critério de conclusão:** inventário por tela, checklist responsivo e nenhuma
  regressão de teclado/foco.

### TECH-001 — Decompor arquivos grandes por domínio

- **Estado:** `PRONTO PARA FAZER`
- **Maiores candidatos atuais:** venda avulsa, fiscal, estoque, usuários,
  financeiro, crediário e landing principal.
- **Regra:** refatorar com testes e sem misturar mudança de comportamento.

### TECH-002 — Dividir classes centrais do backend

- **Estado:** `PRONTO PARA FAZER`
- **Candidatos:** `Program.cs` e controllers/serviços extensos identificados por
  tamanho e responsabilidade, começando pelos que bloquearem CRM/pagamentos.

### DEMO-001 — Dados de exemplo seguros

- **Estado:** `PRONTO PARA FAZER`
- **Falta:** seed idempotente por tenant de demonstração, nunca automático em
  tenant real, com botão/script explícito e opção de limpeza.

### AI-001 — Quota e custo por tenant

- **Estado:** `PRONTO PARA FAZER`
- **Correção do backlog antigo:** o widget já é exibido apenas quando o módulo
  `ia` está habilitado e o usuário tem permissão.
- **Falta:** rate limit, medição de uso/custo por tenant, política de plano e opção
  de chave própria ou cobrança repassada.

## P3 — oportunidades condicionais

### PROD-001 — Personalização avançada por tenant

- **Estado:** `BLOQUEADO`
- **Já existe:** nome, textos, cores, logo e diretório público de lojas ativas.
- **Falta/decidir:** favicon, ícone PWA, imagens adicionais e armazenamento de
  arquivos por tenant.
- **Gatilho:** definir escopo comercial e armazenamento antes de implementar.

### INFRA-001 — Zero-downtime/blue-green

- **Estado:** `BLOQUEADO`
- **Gatilho:** volume real de tráfego/SLA que justifique a complexidade. O deploy
  atual já constrói antes de recriar containers; não é prioridade imediata.

## Entregas confirmadas — não recolocar como pendência

- Multi-tenancy por schema, resolução por domínio/subdomínio e isolamento no backend.
- Migração para PostgreSQL e catálogo central de tenants.
- Diretório público de lojas ativas com nome/logo.
- Billing ciclo 1: preço, implantação, mensalidades, MRR, baixa e inadimplência.
- CRM inicial: leads, prospecção, score, presença digital e conversão.
- Controle de indicações e comissões de vendedores autônomos.
- Comandas estruturadas sob Restaurante, comentários e produção por área/status.
- Desconto editável em percentual e reais no fechamento/pagamento.
- IBPT local, rotinas fiscais e controles de contingência implementados no código;
  homologação externa continua em `FIS-001`.
- IA condicionada ao módulo e à permissão.
- Error boundaries raiz/admin e cinco specs Playwright existentes.
- Diretório público de tenants; o item antigo que dizia “não implementado” está obsoleto.
- Arquivos `.pptx` não estão rastreados atualmente pelo Git.
- Consentimento de cookies funcional e versionado, recusa de opcionais sem
  quebrar autenticação, política de cookies, termos e privacidade v2.
- SEO técnico público: metadata/canonical institucional, imagem social,
  `robots.txt`, `sitemap.xml` e `X-Robots-Tag` nas áreas privadas.

## Decisões que o produto precisa tomar

Estas perguntas não bloqueiam a limpeza do código, mas bloqueiam CRM/analytics e
pagamentos completos:

1. **CRM:** quais segmentos e regiões são prioridade nos próximos 90 dias?
2. **Funil:** quais etapas comerciais, responsáveis e SLA de contato serão padrão?
3. **Dados de mercado:** quais fontes externas são autorizadas e qual orçamento?
4. **Comissões:** percentual/duração padrão, impostos, estorno e portal do vendedor?
5. **Cobrança SaaS:** gateway escolhido e política de inadimplência/suspensão?
6. **Métricas:** quais metas trimestrais de leads, conversão, MRR, churn e CAC?
7. **Prospecção:** quais CNAEs/segmentos e UFs entram no primeiro recorte da base
   CNPJ, e qual frequência aceitável de atualização?
8. **Contato ativo:** quais canais/cadências são autorizados, quem revisa as
   abordagens e como será registrada a oposição do prospect?

---

# Arquivo histórico do backlog — até 2026-08-11

> Conteúdo preservado para contexto de decisões e sessões anteriores. Estados e
> frases como “falta” abaixo podem estar desatualizados. Para priorização, use
> somente o backlog operacional acima.

## Pricing da plataforma — decidido em 2026-07-27

Tabela fechada pelo dono da plataforma (fonte de verdade: `PLANOS` em
`frontend/app/institucional/page.tsx`):

| Plano | Mensal | Implantação (2 mensalidades) | Usuários |
|---|---|---|---|
| Essencial | R$ 120 | R$ 240 | 2 |
| Completo | R$ 269 | R$ 538 | 6 |
| Avançado | R$ 487 | R$ 974 | ilimitado |

Primeiro mês de acesso sem mensalidade; cobrança começa no 2º mês. Sem
fidelidade, sem multa de cancelamento. Valores por loja.

**Sugestões apresentadas e a decisão de cada uma:**
- Implantação **fixa** em vez de escalonada (o serviço de implantação é o mesmo
  nos 3 planos, e R$974 de entrada é barreira justo no plano de maior margem) —
  **recusada**, mantida escalonada por decisão comercial.
- **Plano anual** ("pague 10 leve 12") pra melhorar caixa e derrubar churn —
  **adiada**, não entra agora. Fica registrada aqui como próximo passo natural
  da página de planos.
- Usar a implantação como **moeda de fechamento** ("fecha anual e a implantação
  sai pela metade") em vez de objeção — depende do anual, adiada junto.
- **Teto de usuários no Avançado** (hoje "ilimitado"): risco de margem, porque
  cada usuário no comanda mantém conexão SignalR aberta. `Tenant.MaxUsers` já é
  nullable, então dá pra pôr teto sem tocar em código — só no valor do campo.
  Não decidido.
- Charm pricing inconsistente (120 redondo vs 269/487 calculados) — não alterado.

## Visibilidade de falha (error boundaries + lint + log por tenant) — 2026-07-27

Auditoria de boas práticas pedida pelo usuário. Os três itens atacados juntos
tinham a mesma causa: quando algo quebrava, ninguém ficava sabendo.

**Feito:**
- **Error boundaries do Next.js** — não existia NENHUM nos 67 arquivos de
  `app/`. Exceção de render virava tela branca. Criados `app/global-error.tsx`
  (estilo inline: substitui o root layout, então não tem o globals.css),
  `app/error.tsx`, `app/admin/error.tsx` (renderiza dentro do `<main>`, Sidebar
  preservada) e `app/not-found.tsx`. Os quatro validados no navegador com rota
  descartável que lançava exceção.
- **ESLint** — o script `next lint` existia no `package.json` mas o eslint não
  estava instalado (nem no lockfile), então nunca rodou e os
  `// eslint-disable` espalhados pelo código eram decorativos. Instalados
  `eslint` 8, `eslint-config-next` e o plugin/parser `@typescript-eslint`
  (o config-next não os traz sozinho). `.eslintrc.json` com
  `react/no-unescaped-entities` restrita a `>` e `}` — a regra cheia acusava
  ~20 aspas de texto em português que renderizam sem problema. Step de lint
  adicionado ao CI.
- **Log com identidade do tenant** — não havia `BeginScope` em lugar nenhum:
  40 controllers logando sem dizer de qual loja. `TenantResolutionMiddleware`
  agora abre escopo com `TenantSchema`/`TenantId` no único ponto por onde toda
  requisição passa, e `Logging:Console:IncludeScopes` foi ligado (sem isso o
  .NET descarta escopo silenciosamente). `docker compose logs api | grep
  TenantSchema:<schema>` isola uma loja.

**Bug achado pelo lint recém-instalado:** `admin/comanda/page.tsx` chamava
`fetchHistory(histData)` dentro do handler `ComandaClosed` do SignalR, com
`histData` fora das dependências do effect — o handler congelava a data do
mount. Fechar uma comanda em outro caixa recarregava o histórico de HOJE
enquanto o filtro na tela mostrava outra data. Corrigido com `histDataRef`,
mesmo padrão que o arquivo já usava no `siteNameRef` logo acima.

**Falta:**
- 2 avisos de `react-hooks/exhaustive-deps` ainda abertos
  (`admin/estoque/page.tsx:255`, `components/admin/TimerAlarmOverlay.tsx:115`).
  Ambos parecem benignos, mas não foram investigados a fundo. Enquanto
  existirem, o CI não pode usar `--max-warnings=0`.
- Nenhum boundary específico pra `/plataforma` e `/cliente` — caem no
  `app/error.tsx` da raiz, que funciona mas perde o layout da área.

## Design system + responsividade do admin — 2026-07-25/26 (2 rodadas feitas)

Pedido do usuário: componentizar/padronizar o admin, mais opções de
personalização de site, e responsividade real (nunca ficar sem visão ou
quebrado em nenhum tamanho de tela — mobile pode reorganizar o layout, mas
mantendo a ordem lógica). Prioridade combinada: base do design system +
responsividade primeiro, personalização de site fica pra depois.

**Feito (rodada 1, 25/07):**
- `components/admin/ui/`: `Modal`, `Button`, `EmptyState`, `Spinner`, `Badge`
  — extraídos de padrões duplicados (auditoria achou 13+ shells de modal
  copiados à mão, ~20 usos crus de `Loader2`, ~15 blocos de empty state,
  `.btn-*`/`.badge-*` sem componente React por trás).
- 3 bugs pontuais: `VariantPicker.tsx` usava tokens CSS inválidos
  (`var(--surface)`/`var(--border)`, provável causa de modal sem fundo/borda
  visível); `.nav-item` classe órfã no `Sidebar.tsx`; `StatCard` duplicado
  com nome colidindo em `reservas/page.tsx` (renomeado `ReservaStatCard`).
- Responsividade: header do `qrcodes` sem wrap, tabela de categorias
  (`estoque`) e de solicitações (`lgpd`) sem fallback mobile, headers de
  `perfis`/`reservas` sem wrap, `AiChatWidget` com largura fixa que podia
  estourar em ~320px, grid fixo de 3 colunas no mini-dashboard fiscal.

**Feito (rodada 2, 26/07):**
- Fallback mobile nas 2 tabelas do `financeiro/page.tsx` (view simples e
  análise, com cálculo de margem/preço sugerido) e na trilha de auditoria do
  `lgpd/page.tsx` — formulas copiadas exatas do desktop, mesmo padrão já
  usado em `estoque`.
- Migração de mais 8 modais pro `Modal` (`CobrancaPixModal`,
  `AuditLogDetailModal`, `financeiro/DayDetailModal`,
  `financeiro/KpiChartModal`, `comanda/AddItemModal`,
  `comanda/CloseComandaModal`, `comanda/ComandaReceiptModal`,
  `comanda/EscolherContaCrediarioModal`). `Modal` ganhou prop `stacked`
  (z-index maior) pra suportar modal-sobre-modal.
- `comanda/EditarComandaModal` deixado de fora de propósito: usa
  `items-end sm:items-center` (bottom-sheet no mobile) e z-index próprio,
  padrão que o `Modal` genérico ainda não cobre — migrar ali seria
  regressão de UX, não economia de código.

**Não verificado visualmente em mobile real** — `resize_window` (ferramenta
de automação de browser) não teve efeito nesse ambiente (Wayland/GNOME não
aplicou o resize, confirmado via `window.innerWidth` parado em 1366 mesmo
depois do "sucesso" reportado pela tool). Os padrões aplicados são cópia
exata de implementações já existentes e funcionando no mesmo repo — mas vale
o usuário conferir no celular de verdade assim que puder.

**Falta (registrado, não começado):**
- Migrar os shells de modal inline escritos direto nas páginas (não em
  arquivo `*Modal.tsx` separado) pro `Modal`: `usuarios/page.tsx` (5 modais
  no mesmo arquivo!), `contas-receber`, `eventos`, `fiscal`, `integracoes`,
  `lgpd`, `timer`, `reservas`, `suporte`. Deixado de fora por ser refatoração
  mecânica de baixo valor (não corrige bug nem UX).
- Migrar `Button`/`EmptyState`/`Spinner`/`Badge` nas ~28 páginas que ainda
  escrevem esses padrões cru.
- Sistema de variantes tipado (CVA ou equivalente) — hoje `Button`/`Badge`
  usam `Record<Tone, string>` na mão, funciona mas não escala tão bem quanto
  CVA pra combinações tamanho×variante.
- Mais opções de personalização de site pro lojista (pedido original, ainda
  nem começado — perguntar escopo específico antes de atacar).
- Adoção de `StatCard`/`PageHeader` (já existem, adoção de ~35% das páginas)
  nas páginas que ainda escrevem header/card à mão.

## Pacote fiscal de homologação — 2026-07-21

- **CNPJ vai mudar de contrato/modelagem.** Não criar constraint, índice funcional ou
  validação estrutural definitiva baseada no campo atual. Toda normalização usada
  pelo motor SEFAZ deve ficar centralizada em uma função adaptadora, para que a
  futura origem/formatação do CNPJ seja trocada sem espalhar regras pelos serviços.
  Antes de fechar o novo modelo, definir: origem do identificador, representação por
  tenant/estabelecimento, compatibilidade com matriz/filial e migração dos dados atuais.
- **ICMS-ST configurável implementado.** CSOSN `201`, `202` e `203` aceitam origem,
  modalidade BC-ST, MVA ou pauta/base fixa, redução, alíquota própria, alíquota ST e
  FCP-ST por natureza dentro do schema da loja. O preço cadastrado é tratado como final
  ao consumidor e decomposto em operação + ST + FCP sem alterar o total pago. Parâmetros
  incompletos bloqueiam somente a emissão daquele documento, com erro acionável.
- **Implementado nesta leva:** contingência offline com identidade imutável e QR
  coerente; retransmissão contínua com alerta antes de 24 horas; certificado validado
  localmente; `nfeProc` e `procEventoNFe` persistidos/exportados; pré-voo sem engessar
  o futuro CNPJ; CSC criptografado; unicidade por origem; numeração atômica e estável
  no reprocessamento; bloqueio de regimes ainda não suportados; gates do módulo fiscal
  nos jobs/DF-e; janela de cancelamento baseada na autorização real; QR Code v3 com
  `urlChave`; campos obrigatórios da NFC-e 4.00; textos de homologação; IE sanitizada;
  persistência de NCM/natureza ao editar produto; e IBS/CBS 2026 com base líquida após
  desconto, totalizadores e trava explícita para anos cujas alíquotas não estejam
  configuradas.
- **Implementado para o go-live de 25/07:** inutilização explícita de número/faixa,
  com protocolo/XML e bloqueio de documentos válidos; estorno ERP transacional e
  idempotente após cancelamento, cobrindo estoque, crediário ainda não pago,
  cashback, pontos usados/ganhos e pagamento dividido. Reembolso externo de
  dinheiro/Pix/cartão gera alerta e continua sendo confirmação operacional humana.
- **Bloqueadores restantes de produção:** validar com o contador os parâmetros das
  naturezas efetivamente usadas e executar/registrar o roteiro real de
  homologação da SEFAZ em `docs/GO-LIVE-FISCAL-2026-07-25.md` com certificado/CSC
  do estabelecimento. Código aprovado localmente não substitui autorização real.
- **Critério de conclusão:** teste real em ambiente de homologação da SEFAZ,
  incluindo autorização, rejeição, contingência/retransmissão, cancelamento,
  inutilização e abertura do XML pelo sistema do contador.

## Concluído (sessão 2026-07-15, módulos/export/BYO domain)
- **Seletor de módulos na criação de tenant** — `CreateTenantModal` perguntava
  nada antes (dava pra escolher só depois, editando); agora pergunta já na
  criação. Lista cresceu de 2 pra 4 opções.
- **Fidelidade/Pontos e Portal do Contador viram módulos pagos de verdade** —
  antes Pontos só tinha o toggle operacional da loja (`SiteConfig.PontosFidelidadeAtivo`,
  sem gate de billing por trás) e o Contador vivia de carona no gate de
  "fiscal". Agora os dois têm `RequireModule` próprio; Pontos exige os dois
  "sim" (módulo da plataforma + toggle da loja).
- **Exportação self-service de dados** (`/api/export/*`, UI em `/admin/lgpd`) —
  produtos, clientes e crediário em aberto em CSV. Só exportação por
  enquanto — reduz o medo de lock-in sem depender da gente pra tirar os dados.
- **Domínio próprio por tenant (BYO domain)** — campo `CustomDomain` +
  `TenantResolutionMiddleware` resolve por ele quando não é subdomínio do
  slug. Sem automação de TLS: o lojista põe o domínio dele atrás da própria
  Cloudflare (modo Flexible) — nginx já aceita qualquer Host, zero mudança de
  infra. UI em `/plataforma/tenants/{id}`.
- 34 testes novos nesta sessão (módulos, CSV, resolução de domínio, endpoint
  de cadastro) — suíte completa 238/238.

## Concluído (sessão 2026-07-14, avaliações externas + vulnerabilidades npm)
- Duas avaliações externas (`avaliacao_completa_softnerd.md` /
  `avaliacao_completa_2esysten.md`, ambas de 14/07, fora do repo) foram
  verificadas item a item contra o código atual antes de agir. Vários achados
  já estavam **desatualizados**: `JSON.parse` sem try-catch (os 7 call sites já
  estão protegidos), MongoDB no docker-compose de produção (já removido),
  migrations frágeis (já squashadas na `InitialSquash` de 10/07).
- **Corrigido — vulnerabilidades npm** (9 → 2): `npm audit fix` resolveu axios
  (8 CVEs: ReDoS, vazamento de Proxy-Authorization, prototype pollution),
  form-data (CRLF injection), js-cookie (hijack de protótipo) e ws (DoS por
  exaustão de memória, via SignalR) dentro dos ranges já declarados.
- **Corrigido — next 14.2.5 → 14.2.35**: ~25 CVEs acumulados (cache poisoning,
  SSRF via middleware, bypass de autorização, XSS com nonce de CSP). Patch na
  mesma branch 14.2.x; `next build` e `next dev` smoke-testados, e o
  `scripts/patch-next.js` do postinstall continua aplicando.
- **Corrigido — jspdf 2.x → 4.2.1 + jspdf-autotable 3.x → 5.0.8**: elimina o
  dompurify vulnerável (14 CVEs de XSS/bypass de sanitização). Migração via
  `applyPlugin(jsPDF)` no helper `getJsPDF()` dos 3 geradores de relatório
  (`lib/relatorio*.ts`) — API `doc.autoTable`/`lastAutoTable.finalY` preservada,
  smoke test de runtime validou PDF válido com os padrões usados.
- **Restam 2 vulns npm** (next high + postcss moderate) que só saem com upgrade
  major pro Next 16 — fora do escopo, ver backlog abaixo.
- **Corrigido — COOKIE_SECURE**: warning em todo boot de produção (não só no
  seed) enquanto `COOKIE_SECURE=false`, + comentários inequívocos no
  `deploy/setup.sh` e `deploy/.env.example` sobre o follow-up obrigatório ao
  configurar domínio+Cloudflare.
- **Corrigido — senha padrão removida do STATUS.md** (ficava em texto puro
  rastreado pelo git; agora aponta pra `ADMIN_SEED_PASSWORD`).
- **Corrigido — hash de IP LGPD**: `SHA256(salt + ip)` (vulnerável a
  length-extension) → `HMACSHA256(key: salt, msg: ip)` nos 3 pontos
  (`AuditService`, `AuditSaveChangesInterceptor`, `LgpdController`). Hashes
  antigos no banco ficam órfãos de correlação — aceitável, são pseudônimos.
- **Corrigido — magic strings de pagamento**: constantes `PaymentMethod` (já
  existiam em `VendaAvulsa.cs`) agora usadas em ComandaService/Controller,
  CrediariosController, AnalyticsController, NfceEmissionService,
  ReservationController e DTOs — antes cada um tinha literais `"Pix"` etc.
- **Corrigido — `.AsNoTracking()`** nas queries de leitura de Analytics,
  FinanceiroCalculoService, Relatórios e background services de
  fechamento/export fiscal.
- **Corrigido — `BrazilTime` centralizado** (`Common/BrazilTime.cs`): as cópias
  de `DiaBrasil()`/conversões de fuso duplicadas em controllers/services agora
  delegam pra um único helper.
- **Criado — CI/CD no GitHub Actions** (`.github/workflows/ci.yml`): job de
  backend com Postgres 16 real (mesma porta/credenciais do `TestDbFactory`,
  connection string default da suíte já funciona) + job de frontend com
  `npm ci` + `next build`. Era P0 nas duas avaliações. Push na main com CI
  verde dispara deploy automático via SSH (`update.sh` no VPS) + smoke test.
  **Pendente de ativação**: cadastrar 3 secrets no GitHub (Settings → Secrets
  and variables → Actions): `DEPLOY_HOST` (IP do VPS), `DEPLOY_USER` (ex:
  root) e `DEPLOY_SSH_KEY`. Gerar a chave dedicada NO VPS:
  `ssh-keygen -t ed25519 -f ~/.ssh/gh_deploy -N "" && cat ~/.ssh/gh_deploy.pub
  >> ~/.ssh/authorized_keys && cat ~/.ssh/gh_deploy` — o conteúdo privado
  impresso vai no secret. Sem os secrets o job de deploy pula com aviso, o CI
  continua normal.
- **Corrigido — idempotência no pagamento de crediário**: duplo clique/retry
  em `POST /api/crediarios/{id}/pagamento` podia debitar duas vezes. Agora o
  frontend manda `IdempotencyKey` (GUID por tentativa — vida do modal de
  pagamento), gravada em `PagamentoCrediario` com índice único filtrado
  (migration `AddPagamentoIdempotencyKey`). Replay devolve 200 com o estado
  atual (checado antes da validação de "já quitado", pra retry de quitação não
  virar 400); corrida entre retries simultâneos cai no índice único (23505) e
  devolve o estado gravado. Coberto por 3 testes novos em
  `CrediariosControllerTests` contra Postgres real — suíte completa 195/195.

- **Criado — testes de isolamento multi-tenant** (`TenantIsolationTests`, 7
  testes): exercitam o `TenantConnectionInterceptor` REAL de produção contra
  Postgres — dado do tenant A invisível pro B, troca de tenant no mesmo escopo,
  allowlist de nome de schema, e a rede de segurança do `current_schema()`.
  O teste da rede de segurança **achou bug real**: com schema inexistente,
  `current_schema()` retorna NULL SQL e o cast direto `(string?)` estourava
  `InvalidCastException` antes da mensagem de diagnóstico — corrigido com
  `as string` (isolamento nunca esteve em risco, só o erro era obscuro).
- **Feito — primeira fatia da decomposição do financeiro**: `financeiro/page.tsx`
  caiu de 2198 → 913 linhas. Extraídos pra `components/admin/financeiro/`:
  `CurvaABCSection` (449 l), `FinanceiroCharts` (BarChart/DayPieChart/
  MargemDonut/DateQuickFilter, 361 l), `FormasPagamentoSection` (197 l),
  `KpiChartModal` (138 l), `DayDetailModal` (133 l) e `financeiro-shared`
  (fmt/FORMA_LABELS/Preset/getRange). Zero mudança de comportamento — bundle
  idêntico (22 kB), typecheck e build verdes. Faltam as próximas fatias:
  comanda (104 KB), venda-avulsa (87 KB), estoque (71 KB), usuarios (61 KB).

## Backlog — pendências das avaliações externas de 14/07
Em ordem de prioridade sugerida pelas avaliações, já descontado o que foi feito:
- **Decompor os 5 maiores arquivos do frontend** — `financeiro/page.tsx`
  (113 KB / 2198 → 913 linhas, 6 componentes) e `comanda/page.tsx`
  (2258 → 699 linhas, 8 componentes + shared.ts em
  `components/admin/comanda/`) já feitos, sem mudança de comportamento.
  Faltam: venda-avulsa (87 KB), estoque (71 KB), usuarios (61 KB) + landing
  `app/page.tsx` (48 KB). Sobrepõe com o "retrabalho de UI/UX" abaixo.
- **Bug de hidratação React (#425/#418/#423)** — pré-existente, sistêmico no
  admin; suspeita: script inline de FOUC. Parcialmente atacado no commit
  `2364296`, mas as avaliações ainda o listam — reverificar se persiste.
- **Upgrade Next 16** — elimina as 2 vulns npm restantes; major com breaking
  changes (App Router). Fazer com calma, não junto de outras mudanças.
- **Testes de integração** — feito pro multi-tenancy: `TenantIsolationTests`
  usa o `TenantConnectionInterceptor` real (não mock) contra Postgres real,
  cobrindo isolamento entre schemas, troca de tenant no mesmo escopo,
  rejeição de nome de schema inválido e a rede de segurança do
  `current_schema()` — achou e corrigiu de quebra um bug real (`InvalidCastException`
  em vez de erro claro quando o schema não existe, por causa de cast direto pra
  `DBNull`). Falta ainda: `WebApplicationFactory` end-to-end pra outros fluxos
  (não só multi-tenancy).
- **SSL Cloudflare "Flexible" → "Full (Strict)"** — hoje Cloudflare→VPS trafega
  HTTP puro. Requer cert no nginx do VPS (origin certificate da Cloudflare é o
  caminho barato). Ação de infra, não de código.
- **Monitoring básico** — UptimeRobot (ou similar) no `/health`; hoje se a API
  cair ninguém fica sabendo.
- **Zero-downtime deploy** — claim da avaliação estava exagerado: `update.sh`
  já faz `build` antes do `up -d`, então o downtime é só a recriação dos
  containers (segundos). Blue-green de verdade só vale quando houver tráfego
  que justifique.
- **`.pptx` fora do repo** (5 arquivos, >1 MB) e, se o repo for publicado um
  dia, `git filter-repo`/BFG pra limpar senha/IP do histórico.
- **Coverage (Coverlet) + testes Playwright** — Playwright está configurado
  mas sem nenhum teste escrito; mínimo: login, abrir comanda, fechar comanda.

## Backlog — configuração fiscal por tenant (motores de cálculo de tributos)
### Diretriz arquitetural não negociável

- O `softNerd`/Santuário é caso de teste e fonte de bugs reais, não regra fiscal global.
- O catálogo identifica o tenant e seus módulos; os dados fiscais operacionais ficam no
  schema PostgreSQL exclusivo da loja. Não duplicar `tenant_id` indiscriminadamente nas
  tabelas já isoladas por schema.
- Certificado, CSC, ambiente, credenciamento, série, numeração, emitente, regime,
  naturezas, produtos, NCM e regras tributárias são independentes por loja.
- O módulo `fiscal` controla acesso às telas/endpoints e execução dos jobs. Tenant sem o
  módulo deve continuar vendendo normalmente e não pode gerar documento fiscal.
- Falha ou configuração incompleta de uma loja pode deixar apenas sua NFC-e pendente/
  rejeitada; não pode desfazer a venda, travar o PDV, executar no schema `public` por
  engano nem interromper jobs de outros tenants.
- O motor deve resolver uma regra tributária configurável por natureza/produto e regime,
  com provedores substituíveis. Valores fixos do Santuário (CSOSN, CST, `cClassTrib`,
  MVA, reduções ou alíquotas) só podem virar padrão explícito daquela loja, nunca
  constante universal silenciosa.
- Regra ainda não suportada deve bloquear somente a emissão fiscal daquele documento,
  com diagnóstico acionável e sem inventar imposto. O objetivo é ampliar os provedores
  até cobrir ICMS-ST, regimes normais e classificações IBS/CBS por produto/tenant.

### Concluído em 2026-07-22 — CEST e transparência tributária

- CEST opcional por produto e por tenant, sanitizado para 7 dígitos e obrigatório na
  emissão quando o CSOSN é 201, 202, 203 ou 500; o XML recebe `prod/CEST`.
- Percentuais aproximados federal, estadual e municipal e fonte/versão configuráveis
  por produto. A emissão bloqueia apenas a NFC-e sem esses dados, sem inventar alíquota.
- `vTotTrib` calculado por item sobre o valor efetivamente pago após desconto, somado em
  `ICMSTot/vTotTrib` e detalhado em `infCpl` conforme a Lei 12.741/2012.
- Snapshot dos valores/fontes persistido na nota para o cupom continuar auditável mesmo
  se a tabela do produto mudar depois. Cupom admin e cliente exibem valor por item e
  totais federal/estadual/municipal.
- Importação/exportação CSV inclui CEST, percentuais e fonte.

### IBPT-002 — tabela local diária no lugar da consulta por produto — implementado em 2026-08-06

**Motivador:** o `POST /api/fiscal/ibpt/sincronizar` derrubou com 500 em produção
(trace `0HNNI4IMEL3EK:00000001`) porque a API do IBPT não respondeu em 15s. O
defeito de tratamento foi corrigido na PR #68 — timeout agora vira falha daquele
produto e o laço segue. Mas isso trata o sintoma; o desenho continua frágil.

**Por que o modelo atual não escala.** A sincronização faz uma chamada HTTP por
NCM distinto, dentro da requisição, com 15s de timeout cada. Há cache por NCM no
laço, então o custo é `nº de NCMs distintos`, não `nº de produtos` — mas segue
ilimitado: um catálogo com 200 NCMs e a API lenta ultrapassa qualquer timeout de
proxy reverso muito antes de terminar. O usuário não tem progresso, não pode
fechar a aba, e uma falha no meio não é retomável. Somando os tenants, é a mesma
tabela nacional sendo baixada NCM a NCM, repetidas vezes, por lojas diferentes.

**Desenho proposto (a validar):**

1. job diário baixa a **tabela** do IBPT (por UF), em vez de consultar produto a
   produto;
2. a tabela é persistida localmente (`ncm + uf + vigência → alíquotas`), com
   versão e vigência registradas;
3. cadastrar/alterar produto vira **lookup local** — instantâneo, sem rede, sem
   timeout, sem o `Task.Run` em escopo próprio que hoje existe no
   `ProductController`;
4. a sincronização em lote deixa de existir como operação de request e vira
   consequência do job.

**Perguntas abertas — resolver antes de estimar:**

- **O token atual dá acesso ao download da tabela?** A credencial de hoje é da
  API `apidoni.ibpt.org.br`. O arquivo do "De Olho no Imposto" é outro produto e
  pode exigir credencial/fluxo distinto. **Verificar antes de qualquer código.**
- **A licença permite compartilhar a tabela entre tenants?** A tabela é
  licenciada ao CNPJ que a baixou. Uma tabela nacional por UF servindo todas as
  lojas seria o desenho eficiente, mas pode violar a licença — o que empurra
  para "um download por tenant, com o token dele", perdendo boa parte do ganho
  de eficiência (mas mantendo todo o ganho de latência e robustez). **Decisão
  jurídica, não técnica.**
- **Onde a tabela mora?** Se for por tenant, no schema do tenant. Se for
  compartilhada, no schema público — e aí muda o modelo de dados e o
  provisionamento.
- **O que acontece com quem não tem token?** Hoje o produto fica sem
  transparência tributária e a emissão é bloqueada por tabela vencida. Isso
  continua igual.

**Aceite:** cadastrar um produto com NCM preenche os tributos **sem nenhuma
chamada de rede na requisição**; virar o dia atualiza a tabela sem intervenção; e
a API do IBPT fora do ar por um dia não impede cadastrar produto nem emitir.

**Não fazer junto:** trocar o provedor de cálculo (ver a lista de candidatos
abaixo). Este cartão é sobre COMO a mesma informação chega, não sobre trocar a
fonte.

**Implementado — e as duas perguntas abertas deixaram de bloquear.** A tabela
local é construída pela API que já usamos (`apidoni.ibpt.org.br`), consultada por
NCM distinto pelo job diário, e não pelo download do arquivo do "De Olho no
Imposto". Isso resolve o problema real — tirar a rede do caminho do usuário — sem
depender de credencial nova nem de decisão de licenciamento: cada tenant continua
usando o próprio token, e a tabela vive no schema dele.

O download do arquivo em bloco continua sendo a evolução natural (uma requisição
em vez de N), e as duas perguntas acima continuam valendo **para essa etapa**.
Mas ela deixou de ser pré-requisito.

Entregue: `IbptTabelaEntry` (chave `NCM + UF + origem`), `AtualizarTabelaLocalAsync`
(job, único ponto de rede), `PreencherProdutoDaTabelaLocalAsync` (cadastro, sem
rede) e `AplicarTabelaLocalAsync` (botão da tela, sem rede). 6 testes.

**Complemento entregue em 07/08/2026 — importação por arquivo (PR #72).** A API
do IBPT ficou fora do ar (confirmado de três redes independentes: VPS, ambiente
de desenvolvimento e o navegador do lojista — não foi bloqueio por excesso de
requisição, que devolveria 429), e com ela a tabela local não tinha como ser
construída. `Admin > Fiscal → Importar tabela (.csv)` aceita o
`TabelaIBPTax<UF><versão>.csv` do pacote oficial e substitui a tabela da UF
inteira de uma vez — ~12 mil NCMs numa operação, sem rede.

Isso inverte a dependência que motivava o cartão: **a tabela local passa a ser a
fonte, e a API vira só um mecanismo de atualização.** A API fora do ar deixa de
impedir cadastrar produto ou emitir.

Duas armadilhas do formato, ambas cobertas por teste contra recorte do arquivo
real (`Fixtures/Ibpt/TabelaIBPTax SP 26.1.L`), não contra fixture inventada:
decimal com **ponto** (`13.45` lido em cultura pt-BR viraria 1345) e alíquotas
que variam mais do que se supõe — "Cartas de jogar" (95044000) tem estadual de
**25%**, não os 18% da maioria das linhas. A **UF não está no conteúdo** do
arquivo, só no nome; por isso a importação recusa quando a UF do nome não bate
com a da loja, que é a única defesa contra importar a tabela do estado errado e
emitir com alíquota errada sem nada denunciar.

### Concluído em 2026-07-22 — preenchimento automático pela API IBPT

- Credencial IBPT própria por tenant, armazenada criptografada e nunca devolvida pela
  API. A chamada usa o CNPJ/UF do emitente e NCM, descrição, unidade, valor e GTIN do
  produto conforme o contrato oficial do IBPT.
- Preenchimento automático ao cadastrar/alterar produto e sincronização em lote na tela
  fiscal. Job periódico opera apenas tenants ativos com módulo fiscal e não deixa a
  falha de uma loja interromper as demais.
- Origem da mercadoria seleciona corretamente a alíquota nacional ou importada. Fonte,
  versão, chave e vigência ficam gravadas no produto para auditoria.
- Cadastro manual continua permitido como override: sincronizações não sobrescrevem
  valores completos informados pelo contador. Alterar manualmente percentuais/fonte
  retira a marca automática; trocar apenas o NCM invalida os valores antigos.
- Tabela automática vencida bloqueia somente a emissão fiscal do documento afetado,
  com mensagem acionável. Token não aparece em logs, respostas ou exportações CSV.

Registro histórico da proposta de `avaliacao_completa_2esysten.md`. A emissão usa
Zeus/DFe.NET e o motor atual já resolve regras configuráveis por natureza/produto no
schema de cada tenant; os itens abaixo continuam como opções futuras de provedores:
- Campo `FiscalMode` (`Online` | `Offline` | `Hybrid`) no `FiscalConfig` do
  tenant, com escolha de motor de cálculo por loja.
- Candidatos ainda avaliáveis para cálculo completo: MotorTributarioNet (multi-UF),
  Fiscal.Net, Focus NFe (emissão em escala, pago) e ACBrNCM (lookup offline). A API
  IBPT já está integrada para transparência tributária aproximada; ela não substitui
  as regras fiscais da operação nem a validação do contador.
- Credencial IBPT por tenant já usa o mecanismo AES-256-GCM com `ENCRYPTION_KEY`.
- Frontend expõe só as opções permitidas ao admin da loja.
- Escopo futuro: decidir quais motores de cálculo adicionais entram e se viram módulo
  de billing. O sincronizador IBPT já respeita o módulo fiscal existente.

## Concluído (sessão 2026-07-12, achados da análise técnica externa)
- Documento externo (`analise_tecnica.md`, feito pelo usuário com Gemini/outra
  ferramenta) apontou vários riscos — verificados um a um contra o código
  atual antes de agir (vários já estavam mitigados ou desatualizados: SQL
  injection no search_path já tem allowlist, refresh token já é hasheado,
  secret do JWT já está fora do Docker build, o vazamento de SignalR descrito
  não existe do jeito relatado).
- **Corrigido** (commit `06b921d`): `ComandaHub.AdminGroup` era uma constante
  ÚNICA compartilhada por todos os tenants — todo admin conectado recebia
  atualizações de comanda de TODAS as lojas, não só a própria. Virou
  `GetAdminGroup(tenantId)`.
- **Corrigido** (commit `06b921d`): em `ComandaService.ResolveItemAsync`, uma
  linha resalvava `product.StockQuantity` na entidade rastreada depois do
  decremento atômico (que já era seguro) — o próximo `SaveChangesAsync` da
  mesma requisição sobrescrevia esse valor sem trava, apagando silenciosamente
  o decremento de qualquer venda concorrente do mesmo produto. Removida.
- **Verificado, não era bug** — fechamento de comanda (`CloseComandaAsync`) já
  é atômico: um `SaveChangesAsync` só, todas as mutações (status, crediário,
  pontos) em cima de entidades rastreadas antes dele.
- **Corrigido** (commit `2c61ff0`), achado ao verificar o item acima:
  `CancelComandaAsync` restaura estoque via `ExecuteUpdateAsync` por item
  (gravado na hora) ANTES do `SaveChangesAsync` que marca a comanda como
  Cancelada — sem transação, se esse save falhasse depois do estoque já
  restaurado, um retry do cancelamento restauraria o mesmo estoque de novo
  (a guarda de "já cancelada" não pegava esse estado intermediário). Envolvido
  numa transação explícita.
- **Testado ao vivo e confirmado** (madrugada de 2026-07-12): seed de cliente
  de teste rodado em `loja-final`/`loja-teste3`, comanda aberta numa loja não
  apareceu na outra — vazamento do `AdminGroup` confirmado corrigido.
- **Corrigido** (commit `45fe05b`), achado testando o fix do `CancelComandaAsync`
  ao vivo: `AppDbContext` usa `EnableRetryOnFailure(5)`, e uma transação manual
  solta (`BeginTransactionAsync` sem `CreateExecutionStrategy()`) não é
  suportada com execution strategy que faz retry — o EF lança
  `InvalidOperationException` dentro do `SaveChangesAsync`, quebrando o
  cancelamento com 500. Corrigido ali e em mais 2 casos pré-existentes com o
  mesmo bug latente (nunca exercitados em produção) em
  `FiscalController.CreateNatureza`/`UpdateNatureza`.
- **Corrigido** (commit `b655e34`): lock de concorrência na criação de tenant —
  `SemaphoreSlim` em memória (proporcional à app rodar como instância única;
  precisaria de lock distribuído de verdade só se um dia virar multi-réplica).
- **Corrigido** (commit `11ef5e3`): rate limit dedicado (30/min por IP) nas
  conexões do `ComandaHub` — só conta negotiate/upgrade, não mensagens de uma
  conexão já estabelecida.
- **Corrigido** (commit `677c0a1`): página dedicada de "loja suspensa" — antes
  a casca do frontend carregava vazia (sem produtos/config) em vez de avisar
  claramente. Reaproveitou o fetch de `SiteConfigContext` já existente (que
  silenciava todo erro) em vez de endpoint+middleware novos.
- **Corrigido** (commit `0c2d42b`): VLibras escondido no mobile via media
  query — mecanismo de toggle já existia, só faltava isso.
- **Corrigido** (commit `d83cc5c`): 2 gaps do portal do contador — convite
  cego (convidar por e-mail antes de existir conta, vira `Approved` na hora
  que o contador se cadastra) e endpoint de recusar solicitação pendente
  (apaga o vínculo, não bloqueia um re-pedido futuro), mais o seletor de
  "pra qual contador" no formulário de aviso quando há mais de um aprovado.

## Backlog — achados de menor urgência (mesma análise, não corrigidos ainda)
- `Program.cs` (570 linhas) e `CrediariosController.cs` (700 linhas) — god
  classes de verdade, valeria quebrar em serviços/extension methods menores.
- Middleware do Next.js (`middleware.ts`) não valida tenant do JWT vs
  subdomínio sozinho — mitigado hoje pelo `TenantClaimGuardMiddleware` no
  backend, mas seria mais robusto ter as duas camadas.

## Backlog — diretório de lojas + personalização por tenant
- Pedido original (ainda não implementado): no site institucional principal,
  uma seção/página listando os tenants ativos com link direto pra
  landing-page de cada um (uma espécie de redirecionador — "veja as lojas que
  usam a plataforma").
- Personalização por tenant do próprio visual do sistema deles: ícone do PWA,
  favicon do site institucional da loja, e o ícone que aparece no admin —
  hoje só existe personalização de nome/textos/cores via `SiteConfig`
  (`frontend/contexts/SiteConfigContext.tsx`), não de ícones/imagens.
- Escopo a decidir: upload de imagem por tenant (onde fica armazenado — hoje
  não há serviço de blob storage no projeto, só `uploadProfileImage` local
  pra perfil de usuário) e se o ícone do PWA precisa de manifest.json gerado
  dinamicamente por tenant (hoje é estático).

## Concluído (sessão 2026-07-11/12, melhorias do portal do contador)
- Polling de 20s na lista de solicitações pendentes em `/admin/fiscal` (só
  enquanto a página está aberta) — antes só carregava uma vez no mount, então
  o lojista não via um pedido novo sem F5.
- Lembrete visual de vencimento do DAS (dia 20) pra lojas no Simples Nacional
  — puramente informativo, não calcula valor nem guarda "pago/não pago".
- Resumo do período (faturamento autorizado, nº de notas, valor cancelado) no
  drill-down do contador, calculado a partir dos dados já buscados.
- Badges de saúde na lista de clientes do contador: certificado A1 vencendo
  (usa `FiscalConfig.CertificadoValidade`, já existia) e "sem nota há Xd"/
  "nenhuma nota emitida ainda".
- Mural de avisos simples (`ContadorAviso`, catálogo/schema `public`, preso a
  um `ContadorTenantLink`) — contador e lojista trocam recados curtos.
  Endpoints reaproveitam as mesmas guardas de isolamento de `convidar`/`aprovar`.
  Commit `32882a2`.
- **Gap encontrado testando**: quando uma loja tem **mais de um contador
  Approved** ao mesmo tempo (ex: troca de escritório em andamento), o backend
  de `POST /api/fiscal/contador/avisos` exige `linkId` no corpo pra saber pra
  qual contador é o recado — mas o formulário do lojista não tem seletor
  nenhum, só um campo de texto. Hoje isso só dá erro nesse cenário raro; falta
  um dropdown de "pra qual contador" quando há mais de um vinculado.

## Concluído (sessão 2026-07-11/12, portal do contador — versão cross-tenant)
- **Substitui por completo** a primeira versão (commit `dfa7d5f`, Contador como
  `User` dentro do schema de UMA loja) por uma versão cross-tenant de verdade:
  `ContadorAccount` + `ContadorTenantLink` (N:N) vivem no catálogo
  (`CatalogDbContext`, schema `public`), mesmo andar arquitetural do
  `PlatformOwner`/`Tenant`. Um contador loga uma vez pelo domínio raiz e vê só
  os clientes (lojas) vinculados e aprovados.
- Dois fluxos de vínculo: lojista convida por e-mail em `/admin/fiscal`
  (`Approved` na hora — exige que o contador já tenha se cadastrado antes) ou o
  contador se cadastra sozinho em `/contador/cadastro` e solicita acesso por
  slug (`Pending` até o lojista aprovar). Commit `89b54c8`.
- Ponto de maior risco (isolamento entre tenants) revisado com cuidado extra:
  `ContadorPortalController.AutorizarEObterTenantAsync` exige um
  `ContadorTenantLink` `Approved` (consultado sempre contra o catálogo,
  schema `public`, nunca afetado pela troca de tenant) antes de trocar o
  `ITenantContext` e servir dado de qualquer loja — confirmado que não tem
  jeito de pedir dado de um tenant sem vínculo aprovado.
- **Gaps conhecidos** (decisão consciente do fork, revisar depois se for
  problema real):
  - Convite por e-mail só funciona se o contador **já** tem conta — não tem
    "convite cego" (pré-criar vínculo antes de existir a conta); precisaria de
    uma tabela de convite pendente separada.
  - Só existe endpoint de **aprovar** solicitação, não de rejeitar/recusar.

## Concluído (sessão 2026-07-09/10)
- Branding genericizado: nome/e-mail/endereço/logo da loja vêm de `SiteConfig` dinâmico
  em vez de string fixa "softNerd"/"Santuário Nerd" — backend (`EmailService`,
  `LgpdController` etc.) e frontend (`contexts/SiteConfigContext.tsx`, `useSiteConfig()`).
- Domínio próprio configurado: `2esysten.com.br` na Cloudflare, apontando pro VPS do
  Tenant-ERP (`179.197.67.64`), com registro wildcard (`*.2esysten.com.br`) já pronto
  pra quando o roteamento por subdomínio do multi-tenant estiver no ar.
- Domínio do Maikon (`santuarionerd.tech`) migrado da conta Cloudflare do usuário pra
  conta própria do Maikon (estava incorretamente numa conta que não era dele).
- **Bug de login corrigido (2 causas empilhadas):**
  1. `setup.sh` detectava o IP público via `curl ifconfig.me` sem forçar IPv4 — em VPS
     com IPv6 configurado, gravava um endereço IPv6 inacessível em `APP_URL`, quebrando
     a URL da API embutida no build do frontend e o `JwtSettings:Issuer`. Login falhava
     silenciosamente, sem nenhuma requisição de rede sequer aparecer.
  2. `AuthController.SetAuthCookies` nunca lia a variável `COOKIE_SECURE` — calculava
     `Secure` só por `!_env.IsDevelopment()`, sempre `true` em produção. Login retornava
     200 e setava o cookie, mas o navegador descartava por ser `Secure` numa origem HTTP
     pura (sem domínio/HTTPS ainda) — parecia "deslogar sozinho".

## Concluído (sessão 2026-07-10/11)
- Multi-tenant (isolamento por schema no Postgres + resolução por subdomínio +
  remoção do MongoDB + squash de migrations) — mergeado pelo Ultraplan.
- Refatoramento visual/estrutural do painel admin: `PageHeader`/`StatCard`
  compartilhados, cor do tenant (`SiteConfig.colorPrimary`) agora propaga pro admin
  inteiro via CSS vars, e split do antigo `dashboard/page.tsx` (2610 linhas, 4
  responsabilidades misturadas) em `/admin/comanda` (comanda ao vivo, SignalR) e
  `/admin/dashboard` (Painel Geral, analytics) — ver commits `595976e`..`b94a785`.

## Concluído (sessão 2026-07-11)
- Painel do dono da plataforma (`/plataforma`): listar/cadastrar/suspender-reativar
  tenants. Cadastrar provisiona o schema Postgres novo, roda as migrations
  (`InitialSquash`) nele e já cria o admin inicial da loja — tudo síncrono no mesmo
  request. Role `PlatformOwner` + policy dedicada; login do dono reusa a tela normal
  de `/login`, só muda o redirect. Seed do primeiro dono da plataforma é automático
  no boot via `PLATFORM_OWNER_EMAIL`/`PLATFORM_OWNER_SEED_PASSWORD` no `.env` (mesmo
  padrão do seed do admin) — commits `0998437`..`fbaf89d`.
  Só gestão de tenant, sem billing (ver item de cobrança abaixo).
- **Bug real de isolamento corrigido, encontrado testando o provisionamento pela
  primeira vez**: o `search_path` era setado como `"<schema>", public` (fallback
  pro public). Como `public` é o schema de dados de verdade do tenant-zero (não um
  schema neutro de extensões), qualquer tabela ainda ausente no schema recém-criado
  — inclusive a própria `__EFMigrationsHistory`, antes da primeira migration rodar —
  resolvia silenciosamente pra `public` via busca de nome do Postgres. O EF achava
  "já migrado" e nunca criava nada no schema novo; o admin inicial da loja caía em
  `public.users` em vez do schema isolado. Corrigido em duas partes: (1) removido o
  fallback do search_path (só o schema do tenant), (2) `MigrationsHistoryTable`
  configurado com o schema explícito do tenant atual (a checagem interna do
  provider Npgsql pra saber se a tabela de histórico existe não era scoped pelo
  search_path da mesma forma que a leitura real, causando um segundo mismatch depois
  do fix nº1). Commits `276fb88`, `ffce231`. Validado em produção: tenant de teste
  isolado corretamente, só com seu próprio admin, sem vazar pra `public`.
- **Ressalva confirmada ao testar suspensão**: suspender um tenant bloqueia as
  chamadas de API (`/api/*` retorna 403 — validado com `product`, `announcements`,
  `site-config` todos bloqueados), mas a casca estática do frontend (HTML/JS do
  Next.js) continua carregando normalmente, porque o `TenantResolutionMiddleware`
  que checa `TenantStatus` vive só no backend (.NET) — o container do frontend não
  passa por ele. Resultado: o visitante vê a página carregar mas sem nenhum dado
  (produtos/config vazios por causa dos 403), em vez de uma tela clara de "loja
  suspensa". Melhoria futura: página dedicada de "loja suspensa" no frontend
  (checagem via alguma rota leve tipo `/api/tenant-status` antes de renderizar o
  resto), ou aceitar o comportamento atual como suficiente por ora.

## Concluído (sessão 2026-07-11, continuação)
- Billing ciclo 1: `Tenant` ganha `PlanName`/`PaymentStatus`
  (Pago/Atrasado/Isento)/`EnabledModules` no catálogo. Só o módulo **Fiscal**
  entra no gate técnico (`RequireModuleAttribute` no `FiscalController`, 403 se
  desabilitado; defesa em profundidade em `ComandaService`/`VendaAvulsaService`
  ignorando a flag de emissão se o módulo estiver desligado). Painel
  `/plataforma` ganhou edição de plano/pagamento/módulos por tenant. Sem gateway
  de pagamento — só rastreio manual, por decisão explícita.
- **Bug real de vazamento de tema corrigido**: o toggle claro/escuro do painel
  admin salva a preferência (classe `light` no `<html>`) em localStorage, que
  cascateia pra QUALQUER página do site, não só `/admin`. Os overrides
  `!important` de tema claro (classes Tailwind cruas E as variáveis CSS
  `--bg-card`/`--text-primary`/etc.) vazavam pra páginas com esquema de cor
  próprio (institucional, `/plataforma`) — texto branco sobre fundo virando
  branco também, ficando invisível. Corrigido escopando tudo numa classe nova
  `.admin-shell` (wrapper que envolve Sidebar + conteúdo no layout do admin,
  não só o `<main>`) — commits `ff71519` (primeira tentativa, incompleta),
  `00a3492` (fix completo).
- **Gap fechado**: tela de "esqueci minha senha" já existia pronta
  (`/reset-password`, backend com `forgot-password`/`reset-password` em
  `AuthController.cs`), mas só era alcançável a partir do login do cliente
  (`/entrar`) — o login do admin (`/login`) não linkava pra ela. Adicionado o
  link, com `?from=admin` pra voltar pro lugar certo depois do reset —
  commit `1b7d41b`.

## Concluído (sessão 2026-07-11, billing ciclo 2 — módulo Estoque)
- CRUD básico de produto/categoria e a venda em si (PDV, Comanda, vitrine
  pública) continuam **sempre livres** — travar isso quebraria a loja pra quem
  não pagasse (confirmado na exploração do ciclo 1). Só as features avançadas
  entraram no gate: pré-venda/lista de espera (`ProductWaitListController`/
  `ReservationController`, só nas ações admin — as de cliente continuam sempre
  livres), patrimônio + Curva ABC (dashboard/financeiro, gate 100% frontend, sem
  endpoint próprio pra cortar), relatórios PDF de estoque, e variantes de
  produto (só criar/editar/remover grade — a leitura na hora da venda continua
  sempre livre, senão quebraria o PDV pra produto com grade já configurada).
  Commit `bd641fe`.
- **2 bugs pré-existentes corrigidos** (achados na exploração, sem relação com
  billing, corrigidos antes de continuar): `GET /api/products/{id}/variants`
  exigia `AdminOnly`, bloqueando o autoatendimento do cliente ao escolher
  variante — virou `AllowAnonymous`. A tela `/admin/comanda` não suportava
  lançar produto com grade (`hasVariants`) — agora usa o mesmo `VariantPicker`
  já usado na Frente de Caixa.

## Backlog — billing ciclo 2 (gateway de pagamento)
- Integrar Inter (já usado no projeto pra Pix) e/ou Mercado Pago pra cobrança
  recorrente de verdade, e suspensão automática por inadimplência (hoje é
  manual, pelo painel `/plataforma`).
- Página pública de planos/preços e self-signup de tenant com pagamento (hoje
  só o dono da plataforma cadastra manualmente via `/plataforma`).

## Em andamento
- Nada em execução no momento — painel de tenants e billing ciclo 1 testados e
  validados (isolamento, suspender/reativar; billing ainda falta testar em
  produção depois do deploy).

## Bug conhecido, não corrigido
- Hidratação React (erros minificados #425/#418/#423 no console) aparece em toda
  navegação do painel admin, inclusive em páginas não tocadas nesta sessão (ex.
  `/admin/estoque`) — não é regressão do split do dashboard, é pré-existente e
  sistêmico. Suspeita: o script inline de FOUC (tema claro/escuro + cor do tenant)
  em `app/admin/layout.tsx` renderiza diferente no server vs. no primeiro paint do
  client. Precisa investigar isolado.
- VLibras (widget de Libras do governo, `vlibras.gov.br/app/vlibras-plugin.js`,
  embutido em `app/layout.tsx`) não é usável em mobile — limitação conhecida do
  próprio plugin oficial, não é bug nosso. Considerar esconder via CSS em telas
  pequenas (`components/VLibrasController.tsx` já tem o mecanismo de toggle, só
  falta a media query) já que hoje ele atrapalha mais do que ajuda no celular.

## Backlog — cobrança da plataforma (dono do SaaS)
- Sistema de cobrança para os tenants: o dono do Tenant-ERP (nós) cobra as lojas que
  usam a plataforma — provavelmente planos/assinatura + integração de pagamento
  recorrente + painel de faturamento para o super-admin da plataforma (diferente do
  admin de cada loja/tenant).
- Escopo ainda não detalhado: definir com o usuário modelo de cobrança (mensalidade
  fixa, por uso, por número de tenants/usuários), gateway de pagamento, e se entra
  antes ou depois do multi-tenant estar pronto.

## Backlog — retrabalho de UI/UX
- Revisão completa de interface e experiência do usuário do sistema.
- Escopo ainda não detalhado: quais telas (painel admin, área do cliente, ambos?),
  prioridade, se é redesign visual ou também reestruturação de fluxos — confirmar
  com o usuário antes de começar.
- Fase 5 do refatoramento do admin (adiada de propósito): responsividade mobile das
  6 telas de maior tráfego, as ~19 páginas de cauda longa que não entraram neste
  ciclo, migração de cores hardcoded (`PAY_COLORS`/`ABC_COLORS`/`#7C3AED`) pra tokens
  do tema.

## Backlog — assistente IA (Gemini) por tenant
- Hoje `GeminiChatService` usa UMA `GEMINI_API_KEY` global (env var única do app) pra
  TODOS os tenants, e `AiChatWidget` é montado incondicionalmente em
  `app/admin/layout.tsx` (todo admin de toda loja tem acesso, sem toggle por tenant
  nem contagem de uso). Isso não escala: é admin-only (não é exposto a clientes
  finais da loja), mas o free tier do Gemini é por chave de API, compartilhado entre
  todos os tenants — quanto mais lojas usarem, mais rápido estoura o limite grátis,
  e uma loja "barulhenta" pode consumir a cota de todas as outras.
- Antes de lançar pra mais tenants: (1) decidir se IA é feature de plano pago
  (amarra no sistema de cobrança da plataforma, ver item acima), (2) colocar
  rate-limit por tenant no `AiChatController` (já tem `[EnableRateLimiting("api")]`
  global, falta segmentar por tenant), (3) considerar migrar pra tier pago do Gemini
  com uso repassado no billing, ou usar chave própria por tenant se ele já tiver uma.

## Backlog — dados de teste / seed
- Gerar dados de teste (produtos, clientes, comandas, vendas) pra tenants novos
  explorarem o sistema sem estar vazio, e pra facilitar teste manual de fluxos
  (hoje o tenant de teste em `2esysten.com.br` não tem nenhum cliente cadastrado,
  o que impediu testar o fluxo completo de abrir/fechar comanda no navegador).
- Escopo a decidir: botão "gerar dados de exemplo" no onboarding do tenant, um
  script/seed de dev, ou os dois.

## Backlog — domínio próprio por tenant (BYO domain), parte que falta
- **Feito** (sessão 2026-07-15): campo `CustomDomain` + roteamento no
  `TenantResolutionMiddleware`. Ver "Concluído" no topo do arquivo.
- **Falta**: automação de certificado TLS por domínio (Let's Encrypt via
  HTTP-01/DNS-01 a cada domínio cadastrado, ou produto tipo Cloudflare for
  SaaS) — hoje o lojista precisa ter a própria conta Cloudflare na frente do
  domínio dele (documentado na UI). Esforço bem maior, não fazer apressado;
  só vale a pena se aparecer lojista real pedindo domínio próprio sem já ter
  Cloudflare configurado.

## Concluído (sessão 2026-07-12, motor financeiro mais robusto)
- Fechamento formal de dia/semana/mês (`FechamentoPeriodo`, snapshot congelado
  por tenant) — job `FechamentoBackgroundService` fecha sozinho todo dia às
  00:10 BR (dia), toda segunda (semana) e todo dia 1 (mês); endpoint manual
  `POST /api/analytics/fechamentos/fechar-agora` serve de backfill e de
  "reabrir" (upsert). Commits `4d2fc83`, `73123a9`.
- Comparação período-a-período generalizada pra todos os presets (antes só
  existia pra "mês") — prefere o snapshot congelado quando existe, cai pro
  cálculo ao vivo quando o período ainda não foi fechado. Commit `4a2e2f9`.
- Previsão ponderada por dia da semana (histórico de fechamentos `Dia`),
  substituindo a projeção linear flat que vivia duplicada e inconsistente
  entre dashboard e financeiro — fonte única na API agora. Commit `87e0ce0`.
- Curva ABC e o layout do DRE ficaram de fora de propósito (fora de escopo,
  não pediam mudança).

## Concluído (sessão 2026-07-15, migração de dados completa: export + import)
- Export e import self-service dos 3 tipos de dado (produtos, clientes,
  crediário em aberto) em `/admin/lgpd`, CSV com o mesmo formato de ida e
  volta (`CsvWriter`/`CsvReader`, separador `;`, RFC 4180).
- Import é self-service com relatório de erro por linha (não é
  tudo-ou-nada): linhas válidas entram, inválidas ficam listadas com motivo
  pra corrigir e reenviar só essas.
- Rede de segurança no import de crediário — o item mais arriscado, porque é
  literalmente criar dívida em nome de alguém a partir de uma planilha: só
  aceita linha onde o cliente já existe (resolvido por CPF ou e-mail),
  nunca cria conta nova só pra pendurar dívida nela. Log de auditoria da
  importação de crediário sai com severidade Warning.
- Escopo que ficou de fora de propósito: adaptadores pra formato de sistemas
  concorrentes específicos (só o CSV genérico por enquanto).
