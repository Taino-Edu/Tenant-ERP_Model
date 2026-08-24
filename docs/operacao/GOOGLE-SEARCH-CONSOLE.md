# Google Search Console e PageSpeed Insights

Duas ferramentas gratuitas do Google, com papéis diferentes:

| | Search Console | PageSpeed Insights |
|---|---|---|
| **Responde** | O Google acha, entende e indexa o site? | A página carrega rápido? |
| **Dado** | Real, de quem buscou | Laboratório, simulação |
| **Onde vive** | Painel do Google (contínuo) | Workflow do CI (`pagespeed.yml`) |

O que dá para automatizar já está no repositório. O que sobra exige entrar na
conta do Google e mexer no DNS — **isso é seu**, do mesmo jeito que foi no R2:
eu não crio conta, não faço login e não digito credencial em lugar nenhum.

---

## Parte 1 — Search Console

### 1. Crie a propriedade (escolha "Domínio", não "Prefixo do URL")

Em [search.google.com/search-console](https://search.google.com/search-console)
→ **Adicionar propriedade** → coluna da **esquerda, "Domínio"** → digite
`3esysten.com.br` (sem `https://`, sem `www`).

A escolha importa mais aqui do que na maioria dos sites. Uma propriedade de
**Domínio** cobre `3esysten.com.br`, `www`, http, https **e todo subdomínio** —
ou seja, a vitrine de cada loja (`fulano.3esysten.com.br`) entra junto, sem
cadastrar uma propriedade por cliente. "Prefixo do URL" cobriria só a variante
exata que você digitou, e cada loja nova ficaria de fora.

### 2. Verificação: um registro TXT no DNS

O Google mostra uma linha assim (o valor é único da sua conta):

```
google-site-verification=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

No painel da Cloudflare → domínio `3esysten.com.br` → **DNS** → **Add record**:

| Campo | Valor |
|---|---|
| Type | `TXT` |
| Name | `@` |
| Content | a linha inteira que o Google mostrou |
| TTL | Auto |

Salve, volte ao Google e clique em **Verificar**. Costuma passar em minutos;
se falhar, espere e tente de novo — é propagação de DNS, não erro seu.

> **Não apague esse registro depois.** O Google revalida de tempos em tempos;
> sem o TXT, a propriedade é removida e o histórico vai junto.

### 3. Envie o sitemap

Já verificado: menu **Sitemaps** → digite `sitemap.xml` → **Enviar**.

O arquivo já existe e é gerado pelo próprio app (`frontend/app/sitemap.ts`).
Confira a qualquer momento:

```bash
curl -s https://3esysten.com.br/sitemap.xml
```

**Ele responde por host.** Na plataforma lista as páginas comerciais; num
subdomínio de loja lista a home, o catálogo e **cada produto público daquela
loja**, com a data real da última alteração — é o `lastmod` que faz o Google
voltar depois de uma mudança de preço em vez de reindexar no ritmo dele.

Antes era fixo no domínio da plataforma: o `robots.txt` de cada vitrine apontava
o buscador para um sitemap que não falava de nenhuma página existente naquele
host.

> **Domínio próprio ainda não entra.** A loja que usa `suamarca.com.br` recebe
> um sitemap com home e catálogo, sem a lista de produtos. O tenant é resolvido
> pelo subdomínio (`extractSlug`), e num domínio próprio não há subdomínio para
> extrair. Os produtos continuam sendo alcançados pelos links do catálogo — é
> mais lento, não é invisível.

### 3.1 Redes sociais

O rodapé e o JSON-LD saem da mesma lista (`frontend/lib/contatos.ts`), então um
perfil novo aparece no site e é declarado ao Google de uma vez só. Perfil com
URL vazia não vira ícone nem entra no `sameAs` — declarar ao Google um perfil
que não existe é pior que não declarar nada.

Hoje estão preenchidos Instagram e LinkedIn. TikTok, YouTube e Facebook estão
prontos e vazios: basta a URL do perfil no mesmo arquivo.

### 4. O que olhar depois (e o que ignorar)

Dê ao Google alguns dias antes de esperar dado. Quando aparecer:

- **Páginas → Não indexadas**: é o relatório que vale. Motivo esperado e certo:
  as rotas de `/admin`, `/plataforma`, `/contador`, `/cliente` aparecerem como
  "Bloqueada pelo robots.txt" — elas são de painel, não de busca.
- **"Duplicada, o Google escolheu um canônico diferente"** sobre `/privacidade`,
  `/termos` ou `/cookies`: **não deveria mais aparecer.** Esses três textos são
  os mesmos em todo host onde o app responde, e cada loja publicava a sua cópia.
  Hoje eles declaram um canônico absoluto no domínio da plataforma. Se voltar a
  aparecer, é regressão.
- **Experiência → Core Web Vitals**: fica vazio por um bom tempo. Ele usa dado
  de usuário real (CrUX) e exige volume mínimo de visitas; site novo não tem.
  Enquanto isso, é o PageSpeed da Parte 2 que responde.

---

## Parte 2 — PageSpeed Insights no CI

O workflow [`pagespeed.yml`](../../.github/workflows/pagespeed.yml) mede a home e a
`/parceiros`, em mobile e desktop, e escreve as notas no resumo da execução.

Roda depois de cada deploy na `main` e uma vez por dia às 03:00 (Brasília). No
PR **não** roda de propósito: a PSI mede a URL pública, então no PR ela daria a
nota do site que já está no ar — número verdadeiro respondendo a pergunta
errada.

### A chave é obrigatória, ao contrário do que a documentação diz

A documentação do Google trata a chave como opcional. Testado em 21/08/2026, a
chamada anônima responde:

```
HTTP 429 — Quota exceeded ... "quota_limit_value": "0"
```

Cota zero sem chave. Não é o IP do runner do GitHub sendo penalizado: é que não
existe mais faixa anônima.

**Como gerar (gratuito):**

1. [console.cloud.google.com](https://console.cloud.google.com) → crie um
   projeto (ou use um existente)
2. **APIs e serviços → Biblioteca** → busque **PageSpeed Insights API** →
   **Ativar**
3. **APIs e serviços → Credenciais** → **Criar credenciais → Chave de API**
4. Na chave criada, **Restrições de API** → *Restringir chave* → marque só
   **PageSpeed Insights API**

O passo 4 não é burocracia: chave sem restrição serve para qualquer API do
Google habilitada no projeto. Com restrição, vazar a chave custa cota de
PageSpeed e nada mais.

**Onde guardar:** GitHub → repositório → **Settings → Secrets and variables →
Actions → New repository secret** → nome `PAGESPEED_API_KEY`.

Sem o secret o workflow não quebra: registra um aviso e sai verde. Workflow que
nasce vermelho por configuração ausente é workflow que todo mundo aprende a
ignorar — mesmo desenho do job de deploy no `ci.yml`.

O CI passa a chave por cabeçalho (`X-goog-api-key`), não em `&key=` na URL: URL
inteira aparece em `ps`, em redirect e em mensagem de erro da própria API.

### Lendo o resultado

| Nota | Significa |
|---|---|
| **Performance** | oscila com a rede do datacenter que mediu. Olhe a tendência entre execuções, não o número de uma execução |
| **SEO** | quase determinístico: cai quando falta título, descrição ou heading |
| **LCP** | o que mais pesa hoje neste site — ver a ressalva abaixo |

O job **não** falha por nota baixa, só quando *nenhuma* medição sai (aí é a
página fora do ar, não performance).

> **Ressalva importante sobre o LCP.** A medição da PSI sai de um datacenter do
> Google. Uma boa parte do tempo real do visitante brasileiro hoje não é a
> aplicação: medido em 20/08/2026, a origem responde em ~4 ms e o total é
> ~840 ms, porque o plano gratuito da Cloudflare roteia este domínio por
> Miami/Newark. A landing já é cacheada no edge (`s-maxage=300`), o que tira a
> viagem do caminho de quem pega HIT — mas nota de performance que não melhora
> depois de otimizar o código provavelmente está medindo a rota, não o código.

---

## O que ficou de fora (e por quê)

- **Google Analytics / Tag Manager.** O banner de cookies já separa "Análise e
  desempenho" como categoria que exige consentimento; ligar GA sem respeitar
  essa escolha contradiz a própria política e o módulo de LGPD que o produto
  vende. Se for entrar, entra atrás do consentimento.
- **Google Business Profile.** Vale para a 3E Systen como empresa local de São
  José do Rio Preto, mas é cadastro comercial, não configuração de sistema.
