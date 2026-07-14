// =============================================================================
// financeiro-shared.tsx — utilitários e constantes compartilhados entre a
// página /admin/financeiro e seus componentes extraídos.
// =============================================================================
import { Banknote, CreditCard, DollarSign, QrCode, Star, Wallet } from 'lucide-react'

// ── Helpers ───────────────────────────────────────────────────────────────────
export function toDateInput(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
export function fmt(v: number) {
  return `R$ ${v.toFixed(2).replace('.', ',').replace(/\B(?=(\d{3})+(?!\d))/g, '.')}`
}
export function fmtShort(v: number) {
  if (v >= 1000) return `R$${(v / 1000).toFixed(1)}k`
  return `R$${v.toFixed(0)}`
}

// ── Formas de pagamento ───────────────────────────────────────────────────────
export const FORMA_LABELS: Record<string, string> = {
  Dinheiro:      'Dinheiro',
  Pix:           'Pix',
  CartaoCredito: 'Cartão de Crédito',
  CartaoDebito:  'Cartão de Débito',
  Crediario:     'Crediário',
  Pontos:        'Pontos de Fidelidade',
  Cashback:      'Cashback (Saldo)',
}

export const FORMA_ICONS: Record<string, React.ReactNode> = {
  Dinheiro:      <Banknote   className="w-4 h-4 text-emerald-400" />,
  Pix:           <QrCode     className="w-4 h-4 text-brand-400"   />,
  CartaoCredito: <CreditCard className="w-4 h-4 text-purple-400"  />,
  CartaoDebito:  <CreditCard className="w-4 h-4 text-blue-400"    />,
  Crediario:     <DollarSign className="w-4 h-4 text-amber-400"   />,
  Pontos:        <Star       className="w-4 h-4 text-yellow-400"  />,
  Cashback:      <Wallet     className="w-4 h-4 text-pink-400"    />,
}
export type Preset = 'hoje' | '7d' | 'mes' | 'custom'

export function getRange(preset: Preset) {
  const now = new Date(), hoje = toDateInput(now)
  if (preset === 'hoje') return { inicio: hoje, fim: hoje }
  if (preset === '7d') {
    const ini = new Date(now); ini.setDate(ini.getDate() - 6)
    return { inicio: toDateInput(ini), fim: hoje }
  }
  const ini = new Date(now.getFullYear(), now.getMonth(), 1)
  return { inicio: toDateInput(ini), fim: hoje }
}

// ── Page ───────────────────────────────────────────────────────────────────────
