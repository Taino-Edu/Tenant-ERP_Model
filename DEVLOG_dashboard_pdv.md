# Devlog — Dashboard Colapsável + PDV Multiplos Pagamentos

**Data:** 2026-06-19  
**Branch:** main  
**Commits relacionados:**
- `7db4b65` feat(pdv+ia): carrinho tamanho dinâmico e botão IA arrastável
- `73f3cf6` feat(prefs): VLibras e chat IA controláveis pelas configurações
- `9a51caa` feat(dashboard): painéis colapsáveis com persistência e configurações

---

## 1. PDV — Múltiplas formas de pagamento (Passo 3)

**Arquivo:** `frontend/app/admin/venda-avulsa/page.tsx`

### O que foi feito
O passo 3 do PDV (pagamento) foi reescrito para funcionar igual ao modal "Fechar comanda": lista vertical de botões ao invés do grid de ícones antigo.

### Estrutura nova
- **Lista vertical** (`grid grid-cols-1 gap-2`) com 7 métodos de pagamento
- Cada botão mostra saldo inline à direita (Cashback: saldo, Pontos: pts)
- **Toggle "Dividir em dois métodos"** — estilo checkbox com `✓`
- Quando split ativo: aparece um `<select>` com TODOS os métodos exceto o primário
- Label do primário muda para "PAGAMENTO PRINCIPAL (RESTANTE)" quando split está ativo

### Variáveis de estado relevantes
```typescript
const [payment, setPayment]           // método primário
const [splitEnabled, setSplitEnabled]  // toggle de divisão
const [secondPayment, setSecondPayment] // método secundário
const [secondAmount, setSecondAmount]  // valor do segundo método
```

### Regras de negócio
- Qualquer método pode ser primário OU secundário (não há restrição)
- Se o usuário mudar o primário para o mesmo valor do secundário, o secundário reseta automaticamente
- Backend (`VendaAvulsaService.cs`) já suporta `SecondPaymentMethod` e `SecondPaymentAmountInCents` no MongoDB

---

## 2. Dashboard — Painéis Colapsáveis

**Arquivo:** `frontend/app/admin/dashboard/page.tsx`

### Hook `usePersistentPanel`

Definido antes do componente. Lê e escreve no `localStorage` com a chave `dash_panel_{key}`:

```typescript
function usePersistentPanel(key: string, defaultOpen = true): [boolean, () => void]
```

**Por que é persistente:** quando o Maikon colapsa um painel, fecha o sistema, e volta no dia seguinte, o painel continua como ele deixou.

### Painéis criados

| State | Chave localStorage |
|---|---|
| `panelGrafico` | `dash_panel_grafico` |
| `panelPrevisao` | `dash_panel_previsao` |
| `panelPatrimonio` | `dash_panel_patrimonio` |
| `panelClientes` | `dash_panel_clientes` |
| `panelProdutos` | `dash_panel_produtos` |
| `panelLgpd` | `dash_panel_lgpd` |
| `panelPreInscricoes` | `dash_panel_preinscricoes` |

### Layout novo

```
[Gráfico de receita — 7 dias (colapsável)]
[Previsão financeira do mês (colapsável)]

[ Patrimônio compacto ] [ Top Clientes ] [ LGPD ]   ← 3 colunas no md

[ Top Produtos — 7 dias ] [ Pré-inscrições ]          ← 2 colunas no md
```

Patrimônio virou uma lista compacta de 4 linhas (custo/venda/margem/peças) sem gráfico.

### MiniBarChart

- `BAR_H = 60` (era 120) — barras menores
- Recebe `{ dias, open, onToggle, scheme }` — colapsável pelo próprio header
- Esquema de cores via `CHART_SCHEMES[scheme]`

---

## 3. Sistema de Preferências — Dashboard

### `frontend/lib/api.ts` — tipos novos

```typescript
export type DashChartScheme = 'default' | 'blue' | 'neon'
export type DashRefreshInterval = 15 | 30 | 60 | 0  // 0 = manual

export interface DashboardPanels {
  finHoje: boolean; grafico: boolean; previsao: boolean; patrimonio: boolean
  clientes: boolean; produtos: boolean; lgpd: boolean; preInscricoes: boolean
}

// Adicionado em UserPreferences:
dashboard: {
  refreshInterval: DashRefreshInterval
  chartScheme: DashChartScheme
  panels: DashboardPanels
}
```

`DEFAULT_DASHBOARD_PANELS` — todos `true` por padrão.

### `frontend/hooks/usePreferences.ts` — deep merge

`mergeWithDefaults` foi atualizado para fazer merge profundo no dashboard, garantindo que usuários sem `panels` no JSON salvo recebam os defaults corretos:

```typescript
const dash: Partial<UserPreferences['dashboard']> = partial.dashboard ?? {}
dashboard: {
  ...DEFAULT_PREFERENCES.dashboard,
  ...dash,
  panels: { ...DEFAULT_DASHBOARD_PANELS, ...(dash.panels ?? {}) },
}
```

---

## 4. Configurações do Dashboard (`/admin/configuracoes`)

**Arquivo:** `frontend/app/admin/configuracoes/page.tsx`

Nova seção "Dashboard" adicionada com:

### Intervalo de atualização automática
Botões: **15s / 30s / 1min / Manual (0)**  
Controla `dp.refreshInterval` → `intervalMs = refreshInterval * 1000` no polling do dashboard.

### Esquema de cores do gráfico
3 opções visuais com preview de barras:
- **Padrão** — gold/green/brand/red
- **Azul** — cyan/brand/brand-dark/navy  
- **Neon** — violet/emerald/fuchsia/orange

### Painéis visíveis
8 toggles, um por painel. Quando desligado, o painel some permanentemente do dashboard (ocultação via `dp.panels.X &&`). Diferente do colapso local (que é temporário), isso é uma preferência salva no backend.

### Resetar layout
Botão que remove todas as chaves `dash_panel_*` do `localStorage`, reabrindo todos os painéis colapsados.

---

## Dois níveis de controle de painéis

| Nível | Onde | Persistência | Efeito |
|---|---|---|---|
| `dp.panels.X` | `/configuracoes` | Backend (por usuário) | Oculta permanentemente |
| `panelX` (localStorage) | Chevron no dashboard | localStorage (por browser) | Colapsa/expande visualmente |

---

## Arquivos modificados nesta sessão

```
frontend/lib/api.ts
frontend/hooks/usePreferences.ts
frontend/app/admin/dashboard/page.tsx
frontend/app/admin/venda-avulsa/page.tsx
frontend/app/admin/configuracoes/page.tsx
```
