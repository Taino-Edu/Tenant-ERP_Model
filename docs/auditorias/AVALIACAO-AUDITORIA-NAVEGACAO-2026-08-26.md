# Avaliação da auditoria de navegação — 26/08/2026

Revisão crítica de `output/ux-audit-2026-08-26/auditoria-navegacao-e-home.md`,
produzida por outro assistente a partir de screenshots. Cada afirmação do
documento original foi conferida contra o código deste repositório.

**Resumo:** o diagnóstico de UX é majoritariamente correto, mas a seção sobre o
menu do admin descreve **outra instância do produto**, e os achados de
acessibilidade mais graves — os únicos mensuráveis — ficaram de fora.

## 1. O menu auditado não é o deste repositório

A auditoria afirma que os 27 destinos estão divididos em "Dia a dia, Catálogo,
Clientes, Financeiro, Eventos, Divulgação, Configuração e Ajuda & Sistema".
Esses rótulos não existem nesta branch nem em `origin/main`.

O que `frontend/lib/adminNav.ts` realmente define:

| Seção real | Itens |
| --- | ---: |
| Operacional | 4 |
| Administração | 5 |
| Módulos | 4 |
| Vendas & Clientes | 3 |
| Estoque & Catálogo | 1 |
| Financeiro | 3 |
| Comunicação | 3 |
| Compliance | 3 |
| Pessoal | 1 |
| **Total** | **27** |

O total bate; a estrutura não. O screenshot `02-octus-reservas-topo.png` mostra
a loja **Santuário Nerd** com itens que não existem no menu deste repositório
— *Categorias*, *Cartas TCG*, *Mercado de Cartas*. O painel auditado é outra
implantação.

Duas críticas do documento caem junto com isso:

- **"Categorias parece uma área independente"** — aqui já não é. `/admin/categorias`
  não está no menu; é uma aba dentro de `/admin/estoque` (`?tab=categorias`).
- **"Manual, Sobre e LGPD ocupam o mesmo peso visual"** — `Manual` não está no
  menu. É alcançado pelo atalho `H`, por Primeiros Passos, por Sobre e pela
  subnav do Financeiro.

Consequência: a seção "Arquivos do produto relacionados" do documento original
mapeia recomendações a arquivos que não produzem a tela auditada.

## 2. O que a auditoria acerta

- **Home institucional** — confere integralmente. `NAV_LINKS` tem os sete links
  descritos (Plataforma, Recursos, Portal do Contador, Planos, Clientes,
  Fundadores, Afiliados), mais VLibras, tema, Entrar e Contato. A promessa
  ampla está em `app/institucional/page.tsx:180`.
- **"Configurações" é nome errado** — a crítica mais sólida do documento. A
  própria página se descreve como *"Preferências salvas por perfil de usuário"*:
  IA, VLibras, sons, desconto padrão, painéis do dashboard. Nada de negócio.
  Renomear para **Minhas preferências** e mover para o menu do usuário é barato
  e correto.
- **Personalizar Site é longa demais** — 365 linhas numa rolagem única.

## 3. Onde a auditoria erra o alvo

**"27 destinos visíveis" é o teto, não a média.** `visibleSections()` já filtra
por permissão e por `enabledModules`. Um operador com `pdv` + `comandas` vê três
ou quatro itens. Os 27 só aparecem para admin com todos os módulos ligados. O
problema existe, mas é de *onboarding de dono de loja*, não da operação diária.

**A contagem de destinos é baixa, não alta.** O menu esconde profundidade que
não entrou na conta: `FinanceiroSubnav` tem oito destinos (Insights,
Rentabilidade, Ponto de equilíbrio, Projeção de caixa, Capital de giro, Estoque
inteligente, Manual) invisíveis na sidebar. O número real passa de 35.

**O celular foi ignorado.** `MobileTabBar` já faz o que a auditoria recomenda:
quatro destinos de uso diário mais "Menu". A recomendação de sete destinos foi
escrita como se o produto fosse só desktop.

**Reduzir a sete grupos custa um clique nas telas de uso diário.** Frente de
Caixa viraria `Vender > Frente de Caixa`. Para um PDV operado o dia inteiro isso
é regressão, a menos que atalhos e tab bar cubram esses três ou quatro destinos.

**Não há um dado sequer.** "P0 — reduzir abandono" é opinião, e o produto já
coleta a evidência: `UsageTracker` mede tempo por tela e grava em
`PageViewEvent` (ingestão em `UsageController`, `POST /api/usage/events`). Dá
para saber quais dos 27 destinos ninguém abre antes de reescrever a arquitetura
de informação. Falta um endpoint de leitura — hoje só há ingestão.

## 4. O que a auditoria deixou passar

A seção de acessibilidade do documento original diz que o contraste "precisa de
medição". Medido, com a fórmula de luminância relativa da WCAG 2.1:

| Onde | Cor / fundo | Antes | Depois |
| --- | --- | ---: | ---: |
| Rótulos da sidebar, tema claro, sobre `--surface-900` | `#777788` / `#F5F5F7` | 4,04:1 ✗ | 4,88:1 ✓ |
| Texto auxiliar, tema claro, sobre `--surface-800` | `#777788` / `#FFFFFF` | 4,39:1 ✗ | 5,32:1 ✓ |
| Rótulos da sidebar, tema escuro, sobre `--surface-900` | `#6B7280` / `#121215` | 3,87:1 ✗ | 5,14:1 ✓ |
| Texto auxiliar, tema escuro, sobre `--surface-800` | `#6B7280` / `#1A1A1F` | 3,59:1 ✗ | 4,77:1 ✓ |
| Atalho `kbd` no menu, tema claro | `#999AAA` / `#FFFFFF` | 2,78:1 ✗ | 7,30:1 ✓ |
| Atalho `kbd` no menu, tema escuro | `#4B5563` / `#1A1A1F` | 2,29:1 ✗ | 6,83:1 ✓ |

Texto de 14px em peso normal exige 4.5:1 na WCAG AA. **O menu inteiro reprovava
nos dois temas**, e o tema escuro — que sequer tinha override para `text-gray-500`
— estava pior que o claro. Isso é P0 real, medido, e resolve-se trocando duas
cores.

Mais dois defeitos concretos em `components/admin/Sidebar.tsx`:

- **Drawer com `aria-hidden` e foco vivo.** O `<aside>` mobile permanecia no DOM
  com `aria-hidden={!mobileOpen}`, sem `inert` nem controle de `tabIndex`. Os
  **25 elementos focáveis** do menu continuavam tabuláveis enquanto invisíveis —
  quem navega por teclado entrava num menu que não vê. `aria-hidden` sobre
  conteúdo focável é violação por si só (regra `aria-hidden-focus`).
- **Drawer aberto sem focus trap.** O foco continuava na página atrás ao abrir o
  menu, e o `Tab` percorria o conteúdo de baixo com o menu por cima.
- **Sem `aria-current="page"`.** `FinanceiroSubnav` e `MobileTabBar` marcam a
  tela atual; a sidebar principal só trocava classe CSS. Leitor de tela
  percorria os 27 links sem nenhum deles dizer "você está aqui".

E uma alternativa que o documento não considera: com
`lib/adminKeyboardShortcuts.ts` já existindo, uma **paleta de comandos (Ctrl+K)**
entrega boa parte do ganho de "achar as coisas" sem o risco de reescrever a
arquitetura de informação inteira.

## 5. Priorização revisada

| | Auditoria original | Esta avaliação |
| --- | --- | --- |
| P0 | Renomear Configurações; central única de configurações; menu de 7; mover páginas para abas | Contraste do menu (AA); `inert` no drawer; `aria-current`; renomear Configurações |
| P1 | Primeiros passos no dashboard | Ler `PageViewEvent` e decidir o menu com dado; paleta Ctrl+K |
| P2 | Simplificar a home | Menu de 7 grupos, preservando acesso direto ao PDV; home |

A inversão é deliberada: o P0 do documento original é a mudança mais cara e mais
arriscada dele, justificada por opinião. O P0 acima é medido, pequeno, e já está
quebrado hoje.

## 6. Correções aplicadas nesta rodada

Os quatro defeitos de acessibilidade da seção 4 foram corrigidos.

**`frontend/app/globals.css`** — `text-gray-500` do admin ajustado para `#6A6A79`
no tema claro e `#808697` no escuro (este último não tinha override nenhum). As
duas regras usam `html.light` e `html:not(.light)` para serem mutuamente
exclusivas: elas empatam em especificidade — `:not()` herda a do argumento —, e
sem a exclusão mútua a ordem no arquivo decidiria também o tema claro. O escopo
segue em `.admin-shell` / `.admin-portal`, então o site institucional e a vitrine
pública não são afetados.

**`frontend/components/admin/Sidebar.tsx`** — `aria-current="page"` no item
ativo; atalho `kbd` de `text-gray-600` para `text-gray-400`; `inert` no drawer
fechado; focus trap no drawer aberto.

O `inert` e o foco moram no mesmo efeito porque a ordem entre eles importa:
`inert` desfoca de imediato quem estiver dentro do drawer, então a pergunta "o
foco estava no menu?" precisa ser feita antes de aplicá-lo — depois, o
`activeElement` já é o `body` e a resposta seria sempre não. O foco só volta para
o botão do menu se estava lá dentro; sem essa guarda, o drawer roubaria o foco no
primeiro carregamento, quando ele já nasce fechado.

O atributo vai pelo DOM e não como prop JSX: o `react-dom` 18.3.1 não conhece
`inert` e descarta um valor booleano, enquanto o `@types/react` 18.3.28 o tipa
como `boolean` e recusa a string que o runtime aceitaria — nenhum valor passa nos
dois. Com React 19 isso vira `inert={!mobileOpen}` e o efeito guarda só o foco.

O trap do `Tab` é manual porque o conteúdo da página não é irmão do componente:
não dá para marcá-lo `inert` a partir dali e deixar o navegador resolver, que
seria o caminho curto.

### Verificação

Estático: `tsc --noEmit` e `next lint` limpos; `next build` completo com
`robots.txt`, `sitemap.xml` e `manifest.webmanifest` gerados como antes.

Em navegador, no viewport de 375px, com o comportamento medido e não só olhado:

| Verificação | Resultado |
| --- | --- |
| Drawer fechado tem `inert` | sim |
| Focar à força um dos 25 elementos do drawer fechado | recusado, `activeElement` continua `body` |
| Abrir o menu | `inert` sai e o foco entra no botão "Fechar menu" |
| `Shift+Tab` no primeiro item | volta para o último ("Sair") |
| `Tab` no último item | volta para o primeiro ("Fechar menu") |
| `Esc` | fecha, reaplica `inert` e devolve o foco ao botão "Abrir menu" |
| `aria-current="page"` | presente no drawer, na sidebar desktop e na tab bar |
| Cor do item inativo, tema escuro | `#808697` sobre `#121215` |
| Cor do item inativo, tema claro | `#6A6A79` sobre `#F5F5F7` |

Nenhum aviso de React, hidratação ou `aria` no console. Os erros 500 observados
são do backend ausente no ambiente de teste, não das telas.

Uma armadilha de medição que vale registrar: o link do menu tem
`transition-all duration-150`, e a leitura de `getComputedStyle` logo após trocar
de tema devolve a cor **do meio da transição** — pior ainda numa aba que não está
compondo quadros, onde a transição nunca avança e o valor fica congelado no
antigo. A cor só confere depois de zerar a transição do elemento. Duas leituras
"erradas" foram isso, não o CSS.

## 7. O que continua aberto

- Endpoint de leitura de `PageViewEvent` para embasar o redesenho do menu.
- Reflow a 200% e comunicação de mudança de estado seguem sem verificação — como
  o documento original corretamente ressalva, screenshot não responde a isso.
- O `inert` não vai no HTML do servidor: entre a hidratação e o primeiro efeito
  existe uma janela curta em que o drawer fechado volta a ser tabulável. Fechar
  isso depende do React 19, quando `inert` puder ser prop.
