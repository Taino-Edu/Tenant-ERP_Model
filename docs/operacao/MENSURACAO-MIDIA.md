# Mensuração, Google Ads e Meta

O frontend possui uma única camada de eventos e respeita as categorias do
banner de cookies. GTM e Meta não são carregados antes da escolha correspondente.
O escopo é somente `/`, `/institucional` e `/parceiros`, no domínio raiz da
plataforma (com ou sem `www`). Lojas, domínios personalizados, login e painéis
não inicializam essas tags. `localhost`/`127.0.0.1` permitem testes locais;
subdomínios de lojas em `.localhost` continuam excluídos.

A fila recebe primeiro `consent default` negado, depois a decisão e só então
a inicialização dos fornecedores. Reduzir permissões recarrega o documento
para encerrar scripts já executados; remover um componente React não bastaria.
Links do comercial para áreas fora desse escopo usam navegação completa.
Mudanças de consentimento em outras abas também são observadas.

## Variáveis

Preencha no `.env` canônico de produção e refaça o build do frontend:

```env
GTM_ID=GTM-KB3S5LZ8
META_PIXEL_ID=123456789012345
GOOGLE_SITE_VERIFICATION=
```

O `.env` que vale é o de `/opt/tenant-erp/` — o `deploy/.env` é cópia que o
`update.sh` sobrescreve. E "refaça o build" é literal: estas três entram como
build-arg no Dockerfile do frontend e ficam congeladas na imagem, então editar o
`.env` e reiniciar o container não muda nada. Para auditar o que está no ar sem
acessar o servidor, procure o valor no bundle publicado:

```bash
curl -s https://3esysten.com.br/institucional \
  | grep -o '/_next/static/chunks/app/layout-[^"]*'
curl -s "https://3esysten.com.br<chunk>" | grep -o 'GTM-[A-Z0-9]*'
```

- `GTM_ID`: contêiner usado como ponto único para GA4 e Google Ads.
- `META_PIXEL_ID`: opcional; carrega apenas com consentimento de Marketing.
- `GOOGLE_SITE_VERIFICATION`: token da tag HTML do Search Console. Para a
  propriedade de domínio, prefira o TXT no DNS.

Não configure GA4 ou Google Ads diretamente no código e também no GTM: isso
duplica pageviews e conversões. `ads.txt` não é necessário para quem compra
anúncios; ele serve a sites que vendem inventário publicitário.

## Eventos disponíveis no GTM

Crie gatilhos do tipo **Evento personalizado**:

| Evento | Momento | Uso sugerido |
| --- | --- | --- |
| `octus_page_view` | navegação com consentimento | GA4 page view |
| `view_pricing` | seção de planos visível, inclusive no celular | funil comercial |
| `select_plan` | escolha de plano | intenção comercial, não contratação |
| `whatsapp_click` | clique para o Marketing | microconversão |
| `lead_submit` | lead aceito pela API | conversão principal |
| `sign_up` | cadastro concluído | etapa futura |
| `trial_started` | teste efetivamente iniciado | etapa futura |
| `subscription_paid` | cobrança confirmada | receita futura |

Os quatro campos do Consent Mode v2 são atualizados: `analytics_storage`,
`ad_storage`, `ad_user_data` e `ad_personalization`. No GTM, mantenha as
verificações de consentimento de cada tag. **Os checks nativos sozinhos não
bastam para bloquear todo envio sem consentimento**: configure checks adicionais
por categoria e valide cada tag, inclusive Custom HTML. Analytics-only pode
carregar o contêiner; nenhuma tag de anúncios deve disparar nesse cenário.

O evento Meta é um único `PageView`, não outro evento customizado com o mesmo
significado. Não instale o Pixel também pelo GTM.

## Pageview: as duas montagens possíveis (não misture)

A Tag do Google manda um `page_view` sozinha quando carrega, e **só na primeira
vez**. Disparar a tag de configuração no evento personalizado `octus_page_view`
achando que sai um pageview por navegação NÃO funciona: a segunda vez que ela
dispara não gera hit nenhum, e as trocas de rota client-side entre
`/institucional` e `/parceiros` somem do relatório sem nenhum erro visível.

**Montagem A — a que está em produção (3E Systen, GTM-KB3S5LZ8 / G-L1R4P5T84J).**
Uma tag só: Tag do Google no acionador nativo `Initialization - All Pages`, sem
parâmetro extra. Mede um pageview por carregamento real de página. Não conta as
navegações internas do SPA. Zero configuração, zero risco de contagem dupla.
`Initialization - All Pages` é seguro aqui porque quem decide onde o contêiner
existe é o app (`isMarketingPage` + consentimento): ele nunca é carregado em
painel, login ou loja de tenant, então "All Pages" já significa "só as três
páginas comerciais".

**Montagem B — cobre também a navegação client-side.** Na tag de configuração,
em *Definições de configuração*, adicione o parâmetro `send_page_view` = `false`;
depois crie uma segunda tag, **Google Analytics: evento do GA4**, com nome de
evento `page_view` e acionador de evento personalizado `octus_page_view`.

Fazer metade de uma e metade da outra é o único jeito de errar: a tag de evento
`page_view` sem o `send_page_view=false` conta cada visita duas vezes; o
`send_page_view=false` sem a tag de evento zera a medição de pageview.

Em qualquer das duas, marque em cada tag *Configurações de permissão* →
**Exigir consentimento adicional** → `analytics_storage`. Sem isso a Tag do
Google entra em modo negado e ainda envia ping sem cookie — o roteiro de
validação abaixo exige silêncio total.

O emissor aceita apenas valores conhecidos de `page_path`, `form`, `lead_kind`,
`placement` e `plan`. Campos livres, URLs completas, parâmetros de busca,
e-mail, telefone e nomes são descartados. Isso limita os eventos próprios;
não substitui a revisão das coletas automáticas do contêiner e do Pixel.

## Validação antes de publicar campanhas

1. Recuse todos os opcionais e confirme no DevTools que GTM, Google Analytics e
   Facebook não recebem requisições.
2. Autorize apenas Análise: GA4 pode funcionar; Ads/remarketing e Meta não.
3. Autorize Marketing e valide o Pixel no Events Manager.
4. Use Tag Assistant para confirmar um único `lead_submit` após sucesso real do
   formulário, nunca apenas no clique do botão.
5. Confira se UTMs, página de entrada e referência chegaram no CRM.
6. Revogue cada categoria e confira a recarga, a decisão persistida e a ausência
   de novas requisições proibidas. Repita com duas abas abertas.
7. Navegue para login/painel e lojas: não pode haver tags comerciais nesses
   documentos. Teste também o botão Voltar do navegador.

## Limites da validação local

Os testes de unidade usam IDs fictícios e filas simuladas: verificam ordem,
recusa, categorias, escopo, idempotência e filtragem de dados sem enviar nada
a Google/Meta. Não validam o conteúdo de um contêiner real. A aprovação no
Tag Assistant/Events Manager e a configuração das contas são pendências antes
de publicar campanhas. Cadastro, início de teste e pagamento ainda precisam
de integração no ponto real de confirmação; não são disparados por clique.

Rate limiting agora separa IPs/tenants/usuários conforme a política e informa
`Retry-After` da janela real. O IP vem de `UseForwardedHeaders`, não de um
`CF-Connecting-IP` arbitrário. Preserve a lista de proxies confiáveis e mantenha
a origem protegida. As cotas são em memória por processo, não globais entre
réplicas; uma implantação horizontal exige limite compartilhado/no gateway.

O script de carga local bloqueia troca de origem nos caminhos e não segue
redirecionamentos. Um smoke com poucas requisições não certifica capacidade,
e testes de regressão não equivalem a um pentest.

## Evidências da revisão de 01/09/2026

- 24 testes de regras do frontend: navegação/permissões, consentimento e filas
  simuladas de marketing. Não são 24 jornadas completas no navegador.
- 85 testes .NET de segurança, middleware e rate limiting aprovados, incluindo
  13 novos cenários de particionamento/rejeição. Sem banco de produção.
- Build Next.js 15.5.21: 73 páginas; lint e checagem de tipos aprovados.
- Smoke HTTP standalone: institucional, termos e privacidade retornaram 200;
  robots `text/plain` e sitemap `application/xml`, ambos com `s-maxage=3600`
  e `stale-while-revalidate=86400`.
- Carga curta no standalone em `127.0.0.1:3102`, concorrência 3, quatro lotes
  por rota: 36 respostas 200 e zero erros. p95: institucional 276,16 ms,
  robots 29,40 ms, sitemap 14,94 ms. Só 12 amostras por rota; não extrapolar
  para capacidade de produção. A página HTML manteve cache privado/no-store.
- A checagem visual em 390 px identificou o botão de preferências ausente no
  rodapé comercial e um `sr-only` da tabela escapando da área de rolagem.
  Correções: botão no `SiteFooter` e contêiner da tabela posicionado.
  Reteste no build final: viewport 390 px, largura do documento 384 px (sem
  vazamento horizontal); recusa fechou o banner e o botão visível reabriu as
  categorias. A prévia de teste foi encerrada após a verificação.
- O backend local da prévia isolada (`localhost:5000`) estava indisponível.
  Portanto, essa checagem não confirma cadastro real de leads nem operações
  de clientes. As contas Google/Meta continuam sem validação real.

Nenhuma campanha ou deploy foi publicado nesta revisão. A prévia preexistente
na porta 3000 não foi reiniciada e ainda pode servir o build anterior.

O sitemap continua em `/sitemap.xml` e o `robots.txt` aponta para ele por host.
Domínios de lojas geram sitemap próprio com os produtos públicos daquele tenant.
