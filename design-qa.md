# Design QA — hero Octus claro e escuro

## Evidências

- Verdade visual: `C:\Users\TI06\Documents\GitHub\Tenant-ERP_Model\design-qa-light-final.png`
- Implementação escura: `C:\Users\TI06\Documents\GitHub\Tenant-ERP_Model\design-qa-dark-final.png`
- URL: `http://localhost:3000/institucional`
- Viewport/capturas: 1276 × 717 CSS px, 1276 × 717 px, densidade 1×.
- Estados: topo do hero em tema claro e no tema escuro após acionar o controle de tema.

## Comparação

### Visão completa

O modo escuro preserva a mesma composição do claro: ondas à direita, fio dourado, espaço de leitura à esquerda, proporções do hero e hierarquia dos CTAs. A transformação troca a luminância sem alterar o recorte do asset, produzindo ondas azuis sobre navy em vez de um bloco preto uniforme.

### Superfícies de fidelidade

- **Tipografia:** família, peso, tamanho, line-height, tracking e quebras permanecem idênticos entre os estados; branco/azul substituem navy/azul com contraste suficiente.
- **Espaçamento e ritmo:** nenhuma alteração de geometria, padding, grid, raios ou posição ocorreu na troca de tema.
- **Cores e tokens:** cabeçalho e página usam navy; o asset recebe inversão de luminância, rotação de matiz, brilho reduzido e saturação azul reforçada. CTA e destaque mantêm o azul vivo.
- **Imagem:** o mesmo raster original é reutilizado, com recorte e nitidez preservados; não há arte CSS, placeholder ou SVG improvisado.
- **Copy:** conteúdo, preços e chamadas permanecem iguais nos dois estados.

## Interações e console

- Alternância claro → escuro → claro → escuro verificada no navegador interno.
- Estado final deixado no modo escuro para inspeção do usuário.
- Console verificado após as alternâncias: nenhum warning ou erro.

## Histórico

1. **P2 — hero escuro sem detalhe visual.** O asset era renderizado somente quando `isDark` era falso, deixando o modo escuro uniforme.
2. **Correção:** asset passou a existir nos dois temas; no escuro usa `invert`, `hue-rotate-180`, `brightness-50`, `saturate-150` e opacidade 40%.
3. **Pós-correção:** comparação lado a lado confirma ondas visíveis, contraste legível e paridade de composição.

## Findings

Nenhum P0, P1 ou P2 restante.

## Follow-up polish

- P3: substituir o wordmark textual pelo SVG oficial quando a marca for entregue.

## Final result

final result: passed
