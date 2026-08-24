# Design QA — contraste do pagamento em dinheiro

- Evidencias versionadas: [tema claro](light-final.png), [tema escuro](dark-final.png)
  e [viewport mobile](mobile-final.png).
- Source pixels: 2048 × 864
- Implementation screenshot: indisponível
- Intended viewport/state: desktop, tema claro, `/admin/comanda`, modal “Fechar comanda”, pagamento Dinheiro
- Density normalization: não aplicável; a implementação não pôde ser capturada

## Full-view comparison evidence

A referência foi aberta e mostra texto verde-claro com pouco contraste no painel de dinheiro. A captura da implementação ficou bloqueada porque o controle do navegador autenticado não conseguiu inicializar nesta sessão.

## Focused region comparison evidence

Região-alvo: painel “Valor entregue em dinheiro”, linha “Devido em dinheiro” e resultado “Troco”. Não foi possível produzir a captura pós-alteração no mesmo estado autenticado.

## Findings and implementation history

- [P1] Contraste insuficiente no tema claro.
  - Evidência anterior: `text-emerald-300` sobre superfícies muito claras.
  - Correção aplicada: tokens semânticos de sucesso/perigo específicos para tema claro e escuro.
- [P1] Valor devido pequeno e apagado.
  - Evidência anterior: texto de 11 px em cinza fraco.
  - Correção aplicada: linha dedicada, valor em 14 px, monoespaçado, negrito e cor primária.
- [P2] Troco sem hierarquia suficiente.
  - Correção aplicada: borda de estado, superfície sólida e valor em 16 px/negrito.
- [P1] Opções ativas usam tonalidades 300/400 feitas para o tema escuro.
  - Evidência anterior: “Dinheiro” aparece em ciano-claro sobre fundo lilás-claro.
  - Correção aplicada: cores de marca e estados recebem foreground escuro no escopo do admin claro.

## Required fidelity surfaces

- Fonts and typography: preservada a fonte do sistema; pesos e tamanhos reforçados apenas nos valores monetários.
- Spacing and layout rhythm: preservados largura, padding e raio do componente; adicionada linha interna para o valor devido.
- Colors and visual tokens: substituídas cores Tailwind fixas por tokens semânticos adaptáveis ao tema.
- Image quality and asset fidelity: não há imagens raster no componente; ícones existentes foram preservados.
- Copy and content: texto funcional preservado.

## Implementation checklist

- [x] Corrigir contraste do sucesso no tema claro.
- [x] Manter contraste equivalente no tema escuro.
- [x] Destacar valor devido e troco.
- [x] Validar TypeScript e build de produção.
- [ ] Capturar e comparar a tela autenticada após o deploy.

final result: blocked

Blocker: não foi possível inicializar o controle do navegador autenticado para gerar a captura pós-alteração.
