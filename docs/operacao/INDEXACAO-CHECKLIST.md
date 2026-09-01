# Google e preparação do site — 01/09/2026

## Feito e confirmado

- A propriedade de domínio `3esysten.com.br` já estava acessível no Search Console.
  O bloqueio antigo do backlog por falta de acesso não é mais atual.
- Enviado `https://3esysten.com.br/sitemap.xml` pelo relatório Sitemaps.
  O detalhe confirmou **O sitemap foi processado**, com **6 páginas encontradas**
  e última leitura em 01/09/2026. A listagem exibiu erro transitório imediatamente
  após o envio; o resultado posterior no detalhe confirmou processamento.
- As seis URLs publicadas (home, parceiros, privacidade, termos, cookies e LGPD)
  responderam HTTP 200. O XML é válido e não lista painéis ou contas de clientes.
- `robots.txt` público aponta para o sitemap. Há também regras adicionadas pela
  Cloudflare para rastreadores de IA; não confundir esses bots com Googlebot.
- Home e parceiros têm título, descrição, canonical e `index, follow`.
  A imagem de compartilhamento respondeu 200. Login retorna `X-Robots-Tag: noindex`.
- A inspeção de `https://3esysten.com.br/` confirmou **O URL está no Google** e
  **A página está indexada**, com HTTPS. Isso não elimina o alerta de segurança
  da propriedade nem garante exibição para determinada busca.

Sitemap processado não significa que todas as URLs estejam indexadas ou bem
posicionadas. São etapas diferentes.

## Prioridade 1 — alerta de segurança

O Search Console mostra **1 problema: Páginas enganosas**. URLs de amostra: **N/D**.
O relatório alerta para possíveis avisos aos visitantes em navegadores.

Isso é uma classificação do Google, não uma confirmação nossa de invasão,
nem evidência suficiente para concluir falso positivo. Não foi solicitada revisão
e não foi declarado que o problema está corrigido.

Próximo trabalho: investigar a implantação real, conteúdo e recursos de terceiros,
redirecionamentos, uploads públicos, páginas de login e subdomínios incluídos na
propriedade. Conferir também proprietários autorizados no Search Console.
Depois de identificar/corrigir o problema ou reunir evidência de falso positivo,
preparar uma solicitação de revisão factual.

Referência: [orientação do Google sobre páginas enganosas](https://developers.google.com/search/docs/monitor-debug/security/social-engineering).

### Investigação complementar — 01/09/2026

- Após o usuário ativar Always Use HTTPS, GETs públicos confirmaram 301 de
  HTTP para HTTPS na raiz, em `www`, `/privacidade` e `/lgpd`. A raiz HTTPS,
  `www`, parceiros, sitemap e robots responderam 200. Isso não verifica TLS
  entre Cloudflare e origem nem todas as integrações.
- O relatório de segurança continua com **Páginas enganosas**, sem exemplos
  (`N/D`). Não foi enviada solicitação de revisão nem validação de correção.
- Em Usuários e permissões há apenas o usuário atual, proprietário confirmado,
  e zero tokens de propriedade não usados. Não foi alterada nenhuma permissão.
- A inspeção do DOM público da home e de parceiros mostrou conteúdo da
  3E Systen/Octus, contato e identificação da empresa. Na amostra da home,
  os scripts externos presentes eram Cloudflare Insights e VLibras; não havia
  iframe nem meta refresh. É uma amostra da sessão, não auditoria completa
  de scripts, uploads, variações por visitante ou arquivos do servidor.
- O login público identifica Octus/Painel de Gestão, mas não explica nessa
  tela a relação entre a plataforma e a loja nem oferece links legais nela
  (o Footer global exclui `/login`). Avaliar uma identificação discreta sem
  remover a marca do lojista. Isso é melhoria de clareza, NÃO causa comprovada
  da classificação do Google.

#### Subdomínios antigos encontrados no índice

O detalhe das dez páginas indexadas inclui:

- `https://testefunfa.3esysten.com.br/`
- `https://tettetetete.3esysten.com.br/reset-password?from=admin`
- Páginas legais de `rudyinho.3esysten.com.br`, além das páginas institucionais
  e versões HTTP de `/privacidade` e `/lgpd`.

As três amostras do relatório **Erro soft 404** são todas em
`tettetetete.3esysten.com.br`: `/produtos`, `/privacidade` e `/login`.
Essas amostras são de INDEXAÇÃO, não URLs apontadas como enganosas.

Verificação atual, sem autenticar nem enviar formulários:

- A home de `testefunfa` retorna HTTP 200 com conteúdo inicial genérico Octus;
  após execução do cliente, vai para **Loja não encontrada**.
- A recuperação de senha de `tettetetete` retorna HTTP 200 e
  `X-Robots-Tag: noindex, nofollow, noarchive`; inicialmente mostra o formulário
  e depois vai para **Loja não encontrada**. O índice registra rastreamento
  antigo, de 04/08; não se conclui que o noindex atual esteja sendo ignorado.
- O endpoint público `/api/public/site-icons?slug=...` retorna 404 para ambos.
- No código, `SiteConfigContext.tsx` inicia com configuração genérica e faz
  redirecionamento no navegador após 404/403 da API. Isso explica o conteúdo
  temporário, mas não constitui evidência de invasão ou de falso positivo.

Próximas ações: confirmar com o responsável o destino desses dois subdomínios;
para lojas inexistentes, tratar a resposta no servidor antes de mostrar vitrine
ou formulário. Distinguir tenant inexistente de timeout/erro de infraestrutura,
para não desindexar lojas válidas numa falha temporária. Validar status HTTP,
renderização sem JavaScript, isolamento entre tenants e fluxos de lojas ativas.
Não apagar DNS, lojas ou dados, nem solicitar remoção do Google sem essa decisão.

A investigação da causa do alerta ainda requer confrontar a implantação real,
arquivos/uploads e logs. Nenhum acesso administrativo ao servidor foi realizado
nesta rodada. O sitemap e HTTPS estão verificados; segurança não está declarada
como resolvida.

### Correção local da resposta inicial de tenants — 01/09/2026

Implementado, ainda NÃO publicado:

- O middleware verifica páginas públicas de subdomínios da plataforma antes
  de renderizar a vitrine/login. Um 404 explícito de negócio vindo da API
  produz HTML 404 sem scripts ou formulários, `noindex` e `no-store`.
- Timeout, erro de infraestrutura, 404 genérico de proxy ou resposta inválida
  produzem 503 com `Retry-After: 60`, sem serem classificados como loja ausente.
- O endpoint de ícones ganhou `errorCode: tenant_unavailable` na resposta
  404 já existente (loja ausente OU inativa); nenhum tenant/DNS foi apagado.
- Institucional, domínios próprios, arquivos, APIs, hubs e áreas logadas não
  passam por essa consulta. A reescrita institucional continua limitada a `/`.
- A consulta usa URL interna configurada, slug codificado, sem encaminhar
  cookies e sem cache negativo/compartilhado entre lojas. Há uma consulta extra
  por navegação pública de tenant, limitada a dois segundos; medir latência
  real após implantação antes de adicionar cache de disponibilidade.

Verificação: 31 testes unitários frontend aprovados (15 novos), dois testes
backend (tenant ausente e suspenso), build Next.js completo aprovado e dez
cenários HTTP com build real e API local simulada (vitrine, login, recuperação,
página legal, produtos, RSC, HEAD, 503, loja ativa e domínio principal).
A tela 404 foi conferida no navegador: conteúdo presente, sem formulário,
sem overlay de erro e sem overflow horizontal no viewport desktop testado.
Essa simulação não equivale a testar uma loja real autenticada em produção.

Publicação: subir a API com o novo código de erro ANTES do frontend; confirmar
que `INTERNAL_API_URL` alcança a API (padrão `http://cardgamestore_api:5000`).
API antiga sem código explícito fará o novo frontend responder 503 nos tenants
ausentes, deliberadamente sem inferir inexistência de um erro genérico.
Após publicar, repetir os testes em uma loja ativa e nos dois hosts antigos,
conferir logs/latência e só então validar indexação. Não solicitar revisão de
segurança alegando que esta correção resolveu "Páginas enganosas": a relação
causal continua não demonstrada.

## Prioridade 2 — revisar a indexação

O relatório consultado, atualizado em 27/08/2026, apresenta 10 páginas indexadas
e 26 não indexadas. A propriedade inclui subdomínios; não são necessariamente
36 páginas do institucional.

| Motivo | Quantidade | Ação |
| --- | ---: | --- |
| Cópia sem canonical selecionada pelo usuário | 10 | Inspecionar URLs antes de alterar canonical/redirecionamentos |
| Alternativa com canonical adequada | 6 | Pode ser exclusão intencional; conferir |
| Soft 404 | 3 | Conferir página vazia/erro retornando 200 |
| Não encontrado (404) | 3 | Conferir links antigos e remoções intencionais |
| Excluída por noindex | 2 | Preservar exclusão de páginas privadas; verificar URLs |
| Rastreada, não indexada no momento | 2 | Avaliar conteúdo, duplicação e links internos |

Não remover `noindex` indiscriminadamente. `robots.txt` orienta rastreamento,
mas não protege dados: páginas privadas precisam de autenticação/autorização.

## Demais pendências, em ordem

1. Confirmar HTTPS até a origem e proteção do servidor. O repositório expõe
   Nginx na porta 80; verificar a implantação antes de alterar o modo SSL.
2. Publicar `/.well-known/security.txt` com um contato realmente acompanhado
   e prazo de validade. Hoje a URL pública retorna 404.
3. Revisar os metadados com a orientação de Next.js e manter o sitemap automático;
   só declarar datas de atualização confiáveis, sem atualizar datas para simular
   conteúdo novo.
4. Configurar GA4/Google Ads/Meta com consentimento e testar conversões reais
   antes de investir em campanhas. Não há necessidade de instalar todos os pixels.
5. Depois da segurança: Bing Webmaster Tools e páginas comerciais úteis por
   necessidade do cliente. Não são condições para enviar sitemap ao Google.

## Tradução rápida

- **Sitemap:** lista de páginas que queremos apresentar ao buscador.
- **robots.txt:** instruções de rastreamento para robôs cooperativos.
- **noindex:** pedido para não mostrar uma página nos resultados.
- **Canonical:** qual endereço representa a versão principal de um conteúdo.
- **Metadados:** título, descrição e imagem usados na apresentação de links.
- **security.txt:** contato para comunicar problemas de segurança.

Referências: [enviar sitemap](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap),
[robots.txt e indexação](https://developers.google.com/search/docs/crawling-indexing/robots/intro).

Nesta etapa foi enviado o sitemap na conta do Google e atualizada a documentação
local. Não houve deploy, compra de serviços, mudança de DNS ou pedido de revisão
de segurança.
