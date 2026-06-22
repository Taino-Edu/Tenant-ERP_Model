'use client'
import { useEffect, useState, useCallback, useRef, useMemo } from 'react'
import { analyticsApi, vendaAvulsaApi, FinanceiroDto, FormaPagamentoTotalDto } from '@/lib/api'
import { gerarRelatorioPDF } from '@/lib/relatorio'
import toast from 'react-hot-toast'
import {
  TrendingUp, TrendingDown, DollarSign, AlertCircle,
  RefreshCw, Printer, Package, ShoppingBag, BarChart2,
  Banknote, CreditCard, QrCode, Receipt, ChevronDown, ChevronUp,
  Store, ShoppingCart, X, Search, Star, Wallet, Filter,
  FileText, Lightbulb, ArrowUp, ArrowDown, Minus,
} from 'lucide-react'

// ── Helpers ───────────────────────────────────────────────────────────────────
function toDateInput(d: Date) { return d.toISOString().split('T')[0] }
function fmt(v: number) {
  return `R$ ${v.toFixed(2).replace('.', ',').replace(/\B(?=(\d{3})+(?!\d))/g, '.')}`
}
function fmtShort(v: number) {
  if (v >= 1000) return `R$${(v / 1000).toFixed(1)}k`
  return `R$${v.toFixed(0)}`
}

// ── Formas de pagamento ───────────────────────────────────────────────────────
const FORMA_LABELS: Record<string, string> = {
  Dinheiro:      'Dinheiro',
  Pix:           'Pix',
  CartaoCredito: 'Cartão de Crédito',
  CartaoDebito:  'Cartão de Débito',
  Crediario:     'Crediário',
  Pontos:        'Pontos de Fidelidade',
  Cashback:      'Cashback (Saldo)',
}

const FORMA_ICONS: Record<string, React.ReactNode> = {
  Dinheiro:      <Banknote   className="w-4 h-4 text-emerald-400" />,
  Pix:           <QrCode     className="w-4 h-4 text-brand-400"   />,
  CartaoCredito: <CreditCard className="w-4 h-4 text-purple-400"  />,
  CartaoDebito:  <CreditCard className="w-4 h-4 text-blue-400"    />,
  Crediario:     <DollarSign className="w-4 h-4 text-amber-400"   />,
  Pontos:        <Star       className="w-4 h-4 text-yellow-400"  />,
  Cashback:      <Wallet     className="w-4 h-4 text-pink-400"    />,
}

// ── Modal com gráfico de evolução ─────────────────────────────────────────────
interface ChartPoint { label: string; value: number }

function KpiChartModal({
  title, points, color, totalLabel, onClose,
  extra,
}: {
  title: string
  points: ChartPoint[]
  color: string
  totalLabel: string
  onClose: () => void
  extra?: React.ReactNode
}) {
  const maxVal = Math.max(...points.map(p => p.value), 1)
  const hasData = points.some(p => p.value > 0)

  const colorMap: Record<string, { bar: string; text: string }> = {
    green:  { bar: '#10b981', text: 'text-emerald-400' },
    red:    { bar: 'rgba(239,68,68,0.7)', text: 'text-red-400' },
    brand:  { bar: '#42B6EE', text: 'text-brand-400' },
    yellow: { bar: '#f59e0b', text: 'text-yellow-400' },
  }
  const { bar: barColor, text: textClass } = colorMap[color] ?? colorMap.brand

  const W = 480, H = 160, PAD = { top: 12, right: 8, bottom: 28, left: 48 }
  const chartW = W - PAD.left - PAD.right
  const chartH = H - PAD.top - PAD.bottom
  const barW   = Math.max(6, chartW / (points.length || 1) - 3)

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm"
      onClick={e => { if (e.target === e.currentTarget) onClose() }}
    >
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-lg shadow-2xl">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-surface-600">
          <h3 className="font-semibold text-white">{title}</h3>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-300 transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-5 space-y-4">
          {/* Total destaque */}
          <p className={`text-3xl font-bold font-mono ${textClass}`}>{totalLabel}</p>

          {/* Gráfico */}
          {hasData ? (
            <div className="bg-surface-900 rounded-xl p-3 overflow-x-auto">
              <svg viewBox={`0 0 ${W} ${H}`} className="w-full" style={{ minWidth: Math.max(200, points.length * 18) }}>
                {/* Grid lines */}
                {[0.25, 0.5, 0.75, 1].map(f => (
                  <g key={f}>
                    <line
                      x1={PAD.left} y1={PAD.top + chartH * (1 - f)}
                      x2={W - PAD.right} y2={PAD.top + chartH * (1 - f)}
                      stroke="#32323f" strokeWidth="1"
                    />
                    <text x={PAD.left - 4} y={PAD.top + chartH * (1 - f) + 4}
                      textAnchor="end" fontSize="8" fill="#6b7280">
                      {fmtShort(maxVal * f)}
                    </text>
                  </g>
                ))}

                {/* Barras */}
                {points.map((p, i) => {
                  const slotW = chartW / points.length
                  const x     = PAD.left + slotW * i + (slotW - barW) / 2
                  const bH    = p.value > 0 ? Math.max(2, (p.value / maxVal) * chartH) : 0
                  const showLabel = points.length <= 31 || i % Math.ceil(points.length / 12) === 0
                  return (
                    <g key={p.label}>
                      {bH > 0 && (
                        <rect
                          x={x} y={PAD.top + chartH - bH}
                          width={barW} height={bH}
                          fill={barColor} rx="2"
                        />
                      )}
                      {!bH && (
                        <rect x={x + barW * 0.2} y={PAD.top + chartH - 1} width={barW * 0.6} height={1} fill="#32323f" />
                      )}
                      {showLabel && (
                        <text x={x + barW / 2} y={H - 4}
                          textAnchor="middle" fontSize="7" fill={bH > 0 ? '#9ca3af' : '#4b5563'}>
                          {p.label.slice(5)}
                        </text>
                      )}
                    </g>
                  )
                })}
              </svg>
            </div>
          ) : (
            <div className="bg-surface-900 rounded-xl p-6 text-center text-gray-500 text-sm">
              Sem dados para o período selecionado
            </div>
          )}

          {/* Extra (breakdown) */}
          {extra}
        </div>
      </div>
    </div>
  )
}

// ── KPI Card ──────────────────────────────────────────────────────────────────
function KpiCard({ label, value, sub, color = 'brand', icon: Icon, onClick, change }: {
  label: string; value: string; sub?: string
  color?: string; icon: React.ElementType
  onClick?: () => void
  change?: number | null
}) {
  const colors: Record<string, string> = {
    brand: 'text-brand-400', green: 'text-emerald-400',
    red:   'text-red-400',   yellow: 'text-yellow-400',
  }
  const bgs: Record<string, string> = {
    brand: 'bg-brand-600/15', green: 'bg-emerald-500/15',
    red:   'bg-red-500/15',   yellow: 'bg-yellow-500/15',
  }
  return (
    <button
      onClick={onClick}
      className={`card flex flex-col gap-3 text-left w-full transition-all ${
        onClick ? 'hover:border-surface-400 hover:bg-surface-700/50 cursor-pointer active:scale-[0.98]' : ''
      }`}
    >
      <div className="flex items-center justify-between">
        <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">{label}</p>
        <div className={`w-8 h-8 rounded-lg flex items-center justify-center ${bgs[color]}`}>
          <Icon className={`w-4 h-4 ${colors[color]}`} />
        </div>
      </div>
      <p className={`text-2xl font-bold font-mono ${colors[color] ?? colors.brand}`}>{value}</p>
      {change != null && (
        <div className={`flex items-center gap-1 text-xs font-semibold ${change >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
          {change >= 0
            ? <TrendingUp  className="w-3 h-3 shrink-0" />
            : <TrendingDown className="w-3 h-3 shrink-0" />}
          <span>{change >= 0 ? '+' : ''}{change.toFixed(1)}%</span>
          <span className="text-gray-500 font-normal">vs mês ant.</span>
        </div>
      )}
      {sub && (
        <p className="text-xs text-gray-500 flex items-center gap-1">
          {sub}
          {onClick && <span className="ml-auto text-[10px] text-gray-400">clique para detalhar</span>}
        </p>
      )}
    </button>
  )
}

// ── Formas de pagamento com filtros ───────────────────────────────────────────
function FormasPagamentoSection({ formas }: { formas: FormaPagamentoTotalDto[] }) {
  const [expanded,    setExpanded]    = useState<string | null>(null)
  const [filterForma, setFilterForma] = useState<string>('Todas')
  const [filterMin,   setFilterMin]   = useState('')
  const [filterMax,   setFilterMax]   = useState('')
  const [searchCliente, setSearch]    = useState('')
  const [showFilters, setShowFilters] = useState(false)

  const minVal = parseFloat(filterMin.replace(',', '.')) || 0
  const maxVal = parseFloat(filterMax.replace(',', '.')) || Infinity

  // Filtra formas de pagamento
  const formasFiltradas = formas.filter(f => {
    if (filterForma !== 'Todas' && f.forma !== filterForma) return false
    if (f.total < minVal) return false
    if (f.total > maxVal) return false
    return true
  })

  // Dentro de cada forma, filtra transações
  function filtrarTransacoes(transacoes: FormaPagamentoTotalDto['transacoes']) {
    return transacoes.filter(t => {
      const cliente = (t.cliente ?? '').toLowerCase()
      if (searchCliente && !cliente.includes(searchCliente.toLowerCase())) return false
      if (t.valor < minVal) return false
      if (t.valor > maxVal) return false
      return true
    })
  }

  const hasFilters = filterForma !== 'Todas' || filterMin || filterMax || searchCliente

  return (
    <div className="card p-0 overflow-hidden">
      {/* Header + toggle filtros */}
      <div className="px-5 py-4 border-b border-surface-500">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Receipt className="w-4 h-4 text-brand-400" />
            <h3 className="text-sm font-semibold text-gray-300">Recebimentos por Forma de Pagamento</h3>
          </div>
          <button
            onClick={() => setShowFilters(v => !v)}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border transition-all ${
              showFilters || hasFilters
                ? 'bg-brand-600/20 border-brand-500/50 text-brand-300'
                : 'bg-surface-700 border-surface-600 text-gray-400 hover:text-gray-200'
            }`}
          >
            <Filter className="w-3.5 h-3.5" />
            Filtrar
            {hasFilters && <span className="w-1.5 h-1.5 rounded-full bg-brand-400" />}
          </button>
        </div>

        {/* Painel de filtros — aparece abaixo do header */}
        {showFilters && (
          <div className="mt-4 space-y-3">
            {/* Chips de método */}
            <div className="flex flex-wrap gap-1.5">
              {['Todas', ...formas.map(f => f.forma)].map(forma => (
                <button
                  key={forma}
                  onClick={() => setFilterForma(forma)}
                  className={`px-2.5 py-1 rounded-lg text-xs font-medium border transition-all ${
                    filterForma === forma
                      ? 'bg-brand-600/30 border-brand-500 text-brand-200'
                      : 'bg-surface-700 border-surface-600 text-gray-400 hover:border-surface-500'
                  }`}
                >
                  {forma === 'Todas' ? 'Todas' : (FORMA_LABELS[forma] ?? forma)}
                </button>
              ))}
            </div>

            {/* Faixa de valor + busca */}
            <div className="flex flex-wrap gap-2 items-center">
              <div className="relative">
                <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-gray-500" />
                <input
                  className="input pl-8 py-1.5 text-xs w-full sm:w-40"
                  placeholder="Buscar cliente..."
                  value={searchCliente}
                  onChange={e => setSearch(e.target.value)}
                />
              </div>
              <input
                className="input py-1.5 text-xs w-28"
                placeholder="Valor mín. R$"
                value={filterMin}
                onChange={e => setFilterMin(e.target.value)}
                type="number" min="0" step="0.01"
              />
              <span className="text-gray-500 text-xs">até</span>
              <input
                className="input py-1.5 text-xs w-28"
                placeholder="Valor máx. R$"
                value={filterMax}
                onChange={e => setFilterMax(e.target.value)}
                type="number" min="0" step="0.01"
              />
              {hasFilters && (
                <button
                  onClick={() => { setFilterForma('Todas'); setFilterMin(''); setFilterMax(''); setSearch('') }}
                  className="text-xs text-gray-500 hover:text-gray-300 transition-colors flex items-center gap-1"
                >
                  <X className="w-3.5 h-3.5" /> Limpar
                </button>
              )}
            </div>
          </div>
        )}
      </div>

      {/* Lista */}
      <div className="divide-y divide-surface-600">
        {formasFiltradas.length === 0 ? (
          <p className="text-center text-gray-500 text-sm py-8">Nenhuma forma de pagamento no filtro.</p>
        ) : (
          formasFiltradas.map(f => {
            const isCard = f.forma === 'CartaoCredito' || f.forma === 'CartaoDebito'
            const isOpen = expanded === f.forma
            const txsFiltradas = filtrarTransacoes(f.transacoes)

            return (
              <div key={f.forma}>
                <button
                  onClick={() => setExpanded(isOpen ? null : f.forma)}
                  className={`w-full flex items-center justify-between px-5 py-3 hover:bg-surface-700 transition-colors text-left ${isCard ? 'bg-purple-500/5' : ''}`}
                >
                  <div className="flex items-center gap-3">
                    {FORMA_ICONS[f.forma] ?? <Receipt className="w-4 h-4 text-gray-500" />}
                    <div>
                      <p className="text-sm font-medium text-white">
                        {FORMA_LABELS[f.forma] ?? f.forma}
                        {isCard && <span className="ml-2 text-[10px] bg-purple-500/20 text-purple-300 px-1.5 py-0.5 rounded font-semibold">NF</span>}
                      </p>
                      <p className="text-xs text-gray-500">{f.quantidade} transação{f.quantidade !== 1 ? 'ões' : ''}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <p className={`text-base font-bold font-mono ${isCard ? 'text-purple-300' : 'text-white'}`}>
                      {fmt(f.total)}
                    </p>
                    {isOpen ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
                  </div>
                </button>

                {isOpen && (
                  <div className="bg-surface-800 border-t border-surface-600 divide-y divide-surface-700">
                    {txsFiltradas.length === 0 ? (
                      <p className="text-center text-gray-500 text-xs py-4">Nenhuma transação no filtro.</p>
                    ) : (
                      txsFiltradas.map((t, i) => (
                        <div key={i} className="flex items-center justify-between px-6 py-2.5">
                          <div className="flex items-center gap-2">
                            {t.origem === 'Comanda'
                              ? <ShoppingCart className="w-3.5 h-3.5 text-brand-400 shrink-0" />
                              : <Store        className="w-3.5 h-3.5 text-amber-400 shrink-0" />
                            }
                            <div>
                              <p className="text-xs text-white font-medium">
                                {t.cliente ?? (t.origem === 'Comanda' ? 'Comanda' : 'Balcão')}
                              </p>
                              <p className="text-[10px] text-gray-500">
                                {t.origem === 'Comanda' ? 'Mesa' : 'Balcão'} ·{' '}
                                {new Date(t.data).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })}
                                {t.nota && <span className="ml-1 text-amber-500">{t.nota}</span>}
                              </p>
                            </div>
                          </div>
                          <p className={`text-sm font-bold font-mono ${t.valor < minVal || t.valor > maxVal ? 'text-gray-400' : 'text-white'}`}>
                            {fmt(t.valor)}
                          </p>
                        </div>
                      ))
                    )}
                  </div>
                )}
              </div>
            )
          })
        )}
      </div>
    </div>
  )
}

// ── Gráfico SVG de barras (principal) ─────────────────────────────────────────
function BarChart({ dias }: { dias: FinanceiroDto['diaDia'] }) {
  const [tooltip, setTooltip] = useState<{ x: number; y: number; d: FinanceiroDto['diaDia'][0] } | null>(null)
  if (dias.length === 0) return null

  const W = 700, H = 180, PAD = { top: 16, right: 8, bottom: 32, left: 56 }
  const chartW = W - PAD.left - PAD.right
  const chartH = H - PAD.top  - PAD.bottom
  const maxVal = Math.max(...dias.map(d => d.receita), 1)
  const barW   = Math.max(8, (chartW / dias.length) - 4)

  const gridLines = [0.25, 0.5, 0.75, 1].map(f => ({
    y:   PAD.top + chartH * (1 - f),
    val: maxVal * f,
  }))

  return (
    <div className="card relative">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
          <BarChart2 className="w-4 h-4 text-brand-400" /> Receita por dia
        </h3>
        <div className="flex items-center gap-4 text-xs text-gray-500">
          <span><span className="inline-block w-3 h-2 rounded-sm bg-brand-500 mr-1.5 align-middle" />Margem</span>
          <span><span className="inline-block w-3 h-2 rounded-sm bg-red-500/50 mr-1.5 align-middle" />Custo</span>
        </div>
      </div>
      <div className="overflow-x-auto">
        <svg viewBox={`0 0 ${W} ${H}`} className="w-full" style={{ minWidth: Math.max(400, dias.length * 28) }}>
          {gridLines.map(g => (
            <g key={g.val}>
              <line x1={PAD.left} y1={g.y} x2={W - PAD.right} y2={g.y} stroke="#32323f" strokeWidth="1" />
              <text x={PAD.left - 6} y={g.y + 4} textAnchor="end" fontSize="9" fill="#6b7280">
                {fmtShort(g.val)}
              </text>
            </g>
          ))}
          {dias.map((d, i) => {
            const slotW   = chartW / dias.length
            const x       = PAD.left + slotW * i + (slotW - barW) / 2
            const recH    = (d.receita / maxVal) * chartH
            const custoH  = d.custo > 0 ? Math.min((d.custo / maxVal) * chartH, recH) : 0
            const margemH = recH - custoH
            const hasData = recH > 0
            return (
              <g key={d.dia}
                onMouseEnter={hasData ? e => setTooltip({ x: e.clientX, y: e.clientY, d }) : undefined}
                onMouseLeave={hasData ? () => setTooltip(null) : undefined}
                className={hasData ? 'cursor-pointer' : ''}
              >
                {!hasData && <rect x={x + barW * 0.2} y={PAD.top + chartH - 1} width={barW * 0.6} height={1} fill="#32323f" />}
                {hasData && custoH > 0 && <rect x={x} y={PAD.top + chartH - recH} width={barW} height={custoH} fill="rgba(239,68,68,0.5)" rx="2" />}
                {hasData && <rect x={x} y={PAD.top + chartH - recH + custoH} width={barW} height={margemH} fill="#42B6EE" rx="2" />}
                {(hasData || i === 0 || i === dias.length - 1 || i % Math.ceil(dias.length / 8) === 0) && (
                  <text x={x + barW / 2} y={H - 4} textAnchor="middle" fontSize="8" fill={hasData ? '#9ca3af' : '#4b5563'}>
                    {d.dia.slice(5)}
                  </text>
                )}
              </g>
            )
          })}
        </svg>
      </div>
      {tooltip && (
        <div className="fixed z-50 bg-surface-700 border border-surface-500 rounded-lg px-3 py-2 text-xs shadow-xl pointer-events-none"
          style={{ left: tooltip.x + 12, top: tooltip.y - 48 }}>
          <p className="font-semibold text-white mb-1">{tooltip.d.dia}</p>
          <p className="text-emerald-400">Receita: {fmt(tooltip.d.receita)}</p>
          {tooltip.d.custo > 0 && <p className="text-red-400">Custo: {fmt(tooltip.d.custo)}</p>}
          <p className="text-brand-400">Margem: {fmt(tooltip.d.receita - tooltip.d.custo)}</p>
        </div>
      )}
    </div>
  )
}

// ── Donut custo vs receita ─────────────────────────────────────────────────────
function MargemDonut({ receita, custo }: { receita: number; custo: number }) {
  if (receita <= 0) return null
  const pct  = Math.min(100, custo > 0 ? (custo / receita) * 100 : 0)
  const r    = 42, circ = 2 * Math.PI * r
  const dash = (pct / 100) * circ
  return (
    <div className="card flex flex-col items-center justify-center gap-3 py-6">
      <h3 className="text-sm font-semibold text-gray-300 self-start">Custo vs Receita</h3>
      <svg width="120" height="120" viewBox="0 0 120 120">
        <circle cx="60" cy="60" r={r} fill="none" stroke="#42B6EE" strokeWidth="14" />
        <circle cx="60" cy="60" r={r} fill="none" stroke="rgba(239,68,68,0.5)" strokeWidth="14"
          strokeDasharray={`${dash} ${circ - dash}`} strokeDashoffset={circ / 4} strokeLinecap="round" />
        <text x="60" y="56" textAnchor="middle" fontSize="14" fontWeight="bold" fill="white">{(100 - pct).toFixed(0)}%</text>
        <text x="60" y="70" textAnchor="middle" fontSize="9" fill="#9ca3af">margem</text>
      </svg>
      <div className="flex gap-4 text-xs">
        <span className="flex items-center gap-1.5 text-brand-400"><span className="w-2.5 h-2.5 rounded-full bg-brand-500" />Margem {(100 - pct).toFixed(1)}%</span>
        <span className="flex items-center gap-1.5 text-red-400"><span className="w-2.5 h-2.5 rounded-full bg-red-500/60" />Custo {pct.toFixed(1)}%</span>
      </div>
    </div>
  )
}

// ── Preset de período ──────────────────────────────────────────────────────────
type Preset = 'hoje' | '7d' | 'mes' | 'custom'

function getRange(preset: Preset) {
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
export default function FinanceiroPage() {
  const [preset,    setPreset]    = useState<Preset>('mes')
  const [inicio,    setInicio]    = useState(getRange('mes').inicio)
  const [fim,       setFim]       = useState(getRange('mes').fim)
  const [data,      setData]      = useState<FinanceiroDto | null>(null)
  const [loading,     setLoading]     = useState(true)
  const [exporting,   setExporting]   = useState(false)
  const [backfilling, setBackfilling] = useState(false)
  const [kpiModal,   setKpiModal]   = useState<string | null>(null)
  const [targetPct,  setTargetPct]  = useState(40)
  const [tableView,  setTableView]  = useState<'simples' | 'analise'>('analise')
  const [prevData,   setPrevData]   = useState<FinanceiroDto | null>(null)
  const [filterPaymentMethod, setFilterPaymentMethod] = useState('')
  const [topOrigemFilter, setTopOrigemFilter] = useState<'Todos' | 'Comanda' | 'PDV'>('Todos')
  const [topCatFilter, setTopCatFilter] = useState<string | null>(null)
  const [metaManualInput, setMetaManualInput] = useState('')
  const iniRef = useRef(inicio)
  const fimRef = useRef(fim)

  const loadPrevMonth = useCallback(async (currentIni: string) => {
    const iniDate = new Date(currentIni + 'T12:00:00')
    const prevFimDate = new Date(iniDate.getFullYear(), iniDate.getMonth(), 0)
    const prevIniDate = new Date(prevFimDate.getFullYear(), prevFimDate.getMonth(), 1)
    try {
      const res = await analyticsApi.financeiro(toDateInput(prevIniDate), toDateInput(prevFimDate))
      setPrevData(res.data)
    } catch { setPrevData(null) }
  }, [])

  const load = useCallback(async (ini: string, f: string, pmFilter?: string) => {
    setLoading(true)
    try   { const res = await analyticsApi.financeiro(ini, f, pmFilter || undefined); setData(res.data) }
    catch { toast.error('Erro ao carregar dados financeiros') }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load(inicio, fim); loadPrevMonth(inicio) }, []) // eslint-disable-line

  function applyPreset(p: Preset) {
    setPreset(p)
    if (p !== 'custom') {
      const { inicio: ini, fim: f } = getRange(p)
      setInicio(ini); setFim(f)
      iniRef.current = ini; fimRef.current = f
      load(ini, f, filterPaymentMethod)
      if (p === 'mes') loadPrevMonth(ini)
      else setPrevData(null)
    }
  }

  function applyCustom() {
    setPreset('custom')
    load(inicio, fim, filterPaymentMethod)
  }

  const d = data

  // ── Projeção do mês ───────────────────────────────────────────────────────
  const hoje = new Date()
  const diasNoMes      = new Date(hoje.getFullYear(), hoje.getMonth() + 1, 0).getDate()
  const diaAtual       = hoje.getDate()
  const diasRestantes  = diasNoMes - diaAtual
  const mediaDiaria    = d && d.diaDia.length > 0 ? d.receita / d.diaDia.length : 0
  const projecaoMes    = d ? d.receita + mediaDiaria * diasRestantes : 0

  // ── Dados dos modais de KPI ────────────────────────────────────────────────
  const totalTx = d ? d.pagamentosPorForma.reduce((s, f) => s + f.quantidade, 0) : 0
  const ticketMedio = totalTx > 0 && d ? d.receita / totalTx : 0

  // ── Meta de faturamento ───────────────────────────────────────────────────
  const metaAuto    = d && d.custo > 0 ? Math.round(d.custo / (1 - targetPct / 100) * 100) / 100 : 0
  const metaFinal   = metaManualInput ? (parseFloat(metaManualInput.replace(',', '.')) || 0) : metaAuto
  const metaPct     = metaFinal > 0 && d ? Math.min(100, (d.receita / metaFinal) * 100) : 0

  // ── Top Produtos filtrados ────────────────────────────────────────────────
  const topCats = useMemo(() => {
    if (!d) return [] as string[]
    return [...new Set(d.topProdutos.map(p => p.categoria).filter(Boolean))] as string[]
  }, [d])

  const topFiltered = useMemo((): typeof d extends null ? [] : NonNullable<typeof d>['topProdutos'] => {
    if (!d) return []
    return d.topProdutos.filter(p => {
      if (topOrigemFilter === 'Comanda' && p.receitaComandas === 0 && p.qtdComandas === 0) return false
      if (topOrigemFilter === 'PDV'     && p.receitaAvulsa   === 0 && p.qtdAvulsa   === 0) return false
      if (topCatFilter && p.categoria !== topCatFilter) return false
      return true
    })
  }, [d, topOrigemFilter, topCatFilter])

  const kpiModais: Record<string, {
    title: string; color: string; totalLabel: string
    points: ChartPoint[]; extra?: React.ReactNode
  }> = d ? {
    receita: {
      title: 'Receita Total — Evolução Diária',
      color: 'green',
      totalLabel: fmt(d.receita),
      points: d.diaDia.map(x => ({ label: x.dia, value: x.receita })),
    },
    custo: {
      title: 'Custo Estimado — Evolução Diária',
      color: 'red',
      totalLabel: fmt(d.custo),
      points: d.diaDia.map(x => ({ label: x.dia, value: x.custo })),
      extra: d.topProdutos.filter(p => p.custo > 0).length > 0 ? (
        <div className="space-y-2">
          <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">Top custos por produto</p>
          {d.topProdutos.filter(p => p.custo > 0).slice(0, 6).map(p => (
            <div key={p.nome} className="flex items-center gap-2">
              <div className="flex-1 min-w-0">
                <p className="text-xs text-white truncate">{p.nome}</p>
                <div className="h-1 bg-surface-600 rounded-full overflow-hidden mt-1">
                  <div className="h-full bg-red-500/60 rounded-full"
                    style={{ width: `${Math.min(100, (p.custo / (d.topProdutos[0]?.custo || 1)) * 100)}%` }} />
                </div>
              </div>
              <span className="text-xs font-mono text-red-400 shrink-0">{fmt(p.custo)}</span>
            </div>
          ))}
        </div>
      ) : undefined,
    },
    margem: {
      title: 'Margem Bruta — Evolução Diária',
      color: d.margem >= 0 ? 'brand' : 'red',
      totalLabel: `${fmt(d.margem)} (${d.margemPercent.toFixed(1)}%)`,
      points: d.diaDia.map(x => ({ label: x.dia, value: Math.max(0, x.receita - x.custo) })),
    },
    ticket: {
      title: 'Ticket Médio — Distribuição por Pagamento',
      color: 'brand',
      totalLabel: fmt(ticketMedio),
      points: d.diaDia.map(x => ({ label: x.dia, value: x.receita })),
      extra: (
        <div className="space-y-2">
          <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">{totalTx} transações no período</p>
          {d.pagamentosPorForma.slice(0, 5).map(f => (
            <div key={f.forma} className="flex items-center justify-between text-xs">
              <span className="text-gray-300">{FORMA_LABELS[f.forma] ?? f.forma}</span>
              <span className="font-mono text-white">{f.quantidade}× · {fmt(f.total)}</span>
            </div>
          ))}
        </div>
      ),
    },
    crediarios: {
      title: 'Crediários em Aberto',
      color: 'yellow',
      totalLabel: fmt(d.crediarios),
      points: [], // sem granularidade diária
      extra: (
        <div className="bg-amber-500/10 border border-amber-500/20 rounded-xl p-3 text-sm text-amber-300">
          Valor total em aberto a receber de clientes com crediário.
          Gerencie em <a href="/admin/crediario" className="underline">Gestão de Crediários</a>.
        </div>
      ),
    },
  } : {}

  return (
    <div className="p-4 sm:p-6 space-y-4 sm:space-y-6 print:p-0">

      {/* Modal KPI */}
      {kpiModal && kpiModais[kpiModal] && (
        <KpiChartModal {...kpiModais[kpiModal]} onClose={() => setKpiModal(null)} />
      )}

      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3 print:hidden">
        <div>
          <h1 className="text-2xl font-bold text-white">Controle Financeiro</h1>
          <p className="text-gray-400 text-sm mt-0.5">Receita, custo e margem do período</p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={async () => {
              if (!d) return
              setExporting(true)
              try { await gerarRelatorioPDF(d, { inicio, fim }) }
              catch { toast.error('Erro ao gerar PDF') }
              finally { setExporting(false) }
            }}
            disabled={!d || exporting}
            className="btn-secondary text-sm print:hidden"
          >
            <Printer className="w-4 h-4" />
            {exporting ? 'Gerando...' : 'Exportar PDF'}
          </button>
          <button
            onClick={async () => {
              if (backfilling) return
              setBackfilling(true)
              try {
                const r = await vendaAvulsaApi.backfillCosts()
                toast.success(r.data.mensagem)
                load(inicio, fim)
              } catch {
                toast.error('Erro ao corrigir custos históricos')
              } finally {
                setBackfilling(false)
              }
            }}
            disabled={backfilling}
            title="Preenche custo zero em vendas avulsas antigas usando o custo atual dos produtos"
            className="btn-secondary text-sm print:hidden"
          >
            <RefreshCw className={`w-4 h-4 ${backfilling ? 'animate-spin' : ''}`} />
            {backfilling ? 'Corrigindo...' : 'Corrigir custos'}
          </button>
          <button onClick={() => load(inicio, fim, filterPaymentMethod)} disabled={loading} className="btn-secondary text-sm">
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>
        </div>
      </div>

      {/* ── Filtros ── sticky no topo */}
      <div className="sticky top-0 z-10 print:hidden">
        <div className="card flex flex-wrap gap-3 items-center border-surface-500 shadow-xl">
          {/* Presets */}
          <div className="flex gap-1.5 flex-wrap">
            {(['hoje', '7d', 'mes', 'custom'] as Preset[]).map(p => (
              <button key={p} onClick={() => applyPreset(p)}
                className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-all ${
                  preset === p ? 'bg-brand-600 text-white shadow-lg shadow-brand-600/20' : 'bg-surface-700 text-gray-400 hover:text-white hover:bg-surface-600'
                }`}
              >
                {{ hoje: 'Hoje', '7d': '7 dias', mes: 'Este mês', custom: 'Personalizado' }[p]}
              </button>
            ))}
          </div>

          {/* Filtro de forma de pagamento */}
          <div className="flex flex-wrap gap-1.5">
            {['', 'Pix', 'Dinheiro', 'CartaoCredito', 'CartaoDebito', 'Crediario'].map(pm => (
              <button
                key={pm || 'all'}
                onClick={() => {
                  setFilterPaymentMethod(pm)
                  load(inicio, fim, pm)
                }}
                className={`flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-medium border transition-all ${
                  filterPaymentMethod === pm
                    ? 'bg-brand-600/30 border-brand-500 text-brand-200'
                    : 'bg-surface-700 border-surface-600 text-gray-400 hover:border-surface-500 hover:text-gray-200'
                }`}
              >
                {pm ? FORMA_ICONS[pm] : null}
                {pm ? (FORMA_LABELS[pm] ?? pm) : 'Todos'}
              </button>
            ))}
          </div>

          {/* Período atual */}
          {preset !== 'custom' && (
            <span className="text-xs text-gray-500 ml-1">
              {inicio === fim ? inicio : `${inicio} → ${fim}`}
            </span>
          )}

          {/* Custom inline — sem scroll */}
          {preset === 'custom' && (
            <div className="flex items-center gap-2 flex-wrap">
              <input type="date" className="input py-1.5 text-sm w-full sm:w-36"
                value={inicio} max={fim}
                onChange={e => setInicio(e.target.value)} />
              <span className="text-gray-500 text-sm">até</span>
              <input type="date" className="input py-1.5 text-sm w-full sm:w-36"
                value={fim} min={inicio} max={toDateInput(new Date())}
                onChange={e => setFim(e.target.value)} />
              <button
                onClick={applyCustom}
                disabled={loading}
                className="btn-primary text-sm py-1.5 min-w-[90px]"
              >
                {loading ? <RefreshCw className="w-4 h-4 animate-spin" /> : null}
                {loading ? 'Carregando…' : 'Aplicar'}
              </button>
            </div>
          )}

          {/* Loading indicator inline */}
          {loading && preset !== 'custom' && (
            <RefreshCw className="w-4 h-4 animate-spin text-brand-400 ml-auto" />
          )}
        </div>
      </div>

      {loading ? (
        <div className="flex justify-center py-20">
          <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>
      ) : d ? (
        <>
          {/* KPIs — clicáveis */}
          {(() => {
            function pctChange(cur: number, prev: number): number | null {
              if (!prevData || prev === 0) return null
              return ((cur - prev) / prev) * 100
            }
            const prevTx = prevData ? prevData.pagamentosPorForma.reduce((s, f) => s + f.quantidade, 0) : 0
            const prevTicket = prevTx > 0 && prevData ? prevData.receita / prevTx : 0
            return (
              <div className="grid grid-cols-2 lg:grid-cols-5 gap-4">
                <KpiCard label="Receita total"      value={fmt(d.receita)}     sub={`${d.diaDia.length} dias`}                      color="green"  icon={TrendingUp}   onClick={() => setKpiModal('receita')}    change={pctChange(d.receita,    prevData?.receita    ?? 0)} />
                <KpiCard label="Custo estimado"     value={fmt(d.custo)}       sub="Clique para detalhar por produto"               color="red"    icon={ShoppingBag}  onClick={() => setKpiModal('custo')}      change={pctChange(d.custo,      prevData?.custo      ?? 0)} />
                <KpiCard label="Margem média"        value={`${d.margemPercent.toFixed(1)}%`} sub={`${fmt(d.margem)} sobre custo`} color={d.margem >= 0 ? 'brand' : 'red'} icon={d.margem >= 0 ? TrendingUp : TrendingDown} onClick={() => setKpiModal('margem')} change={pctChange(d.margemPercent, prevData?.margemPercent ?? 0)} />
                <KpiCard label="Ticket médio"       value={fmt(ticketMedio)}   sub={`${totalTx} transação${totalTx !== 1 ? 'ões' : ''}`}  color="brand"  icon={CreditCard}   onClick={() => setKpiModal('ticket')}     change={pctChange(ticketMedio,  prevTicket)} />
                <KpiCard label="Crediários abertos" value={fmt(d.crediarios)}  sub="A receber"                                      color="yellow" icon={AlertCircle}  onClick={() => setKpiModal('crediarios')} change={pctChange(d.crediarios, prevData?.crediarios ?? 0)} />
              </div>
            )
          })()}

          {/* Breakdown Comandas vs Avulsas */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="card flex items-center gap-4">
              <div className="w-10 h-10 rounded-xl bg-brand-600/15 flex items-center justify-center shrink-0">
                <ShoppingBag className="w-5 h-5 text-brand-400" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">Comandas (mesas)</p>
                <p className="text-xl font-bold font-mono text-brand-400">{fmt(d.receitaComandas)}</p>
                {d.receita > 0 && (
                  <p className="text-xs text-gray-400 mt-0.5">{((d.receitaComandas / d.receita) * 100).toFixed(1)}% do total</p>
                )}
              </div>
            </div>
            <div className="card flex items-center gap-4">
              <div className="w-10 h-10 rounded-xl bg-emerald-500/15 flex items-center justify-center shrink-0">
                <Package className="w-5 h-5 text-emerald-400" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">Venda Avulsa (balcão)</p>
                <p className="text-xl font-bold font-mono text-emerald-400">{fmt(d.receitaAvulsa)}</p>
                {d.receita > 0 && (
                  <p className="text-xs text-gray-400 mt-0.5">{((d.receitaAvulsa / d.receita) * 100).toFixed(1)}% do total</p>
                )}
              </div>
            </div>
          </div>

          {/* ── DRE ─────────────────────────────────────────────────────── */}
          {d.receita > 0 && (
            <div className="card p-0 overflow-hidden">
              <div className="px-5 py-4 border-b border-surface-500 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <FileText className="w-4 h-4 text-brand-400" />
                  <h3 className="text-sm font-semibold text-gray-300">DRE — Demonstração do Resultado</h3>
                </div>
                <span className="text-[11px] text-gray-500">{inicio === fim ? inicio : `${inicio} → ${fim}`}</span>
              </div>

              <div className="p-5 space-y-0">
                {/* Receita Bruta */}
                <div className="flex items-center justify-between py-2.5 border-b border-surface-600">
                  <div>
                    <p className="text-sm font-semibold text-gray-200">Receita Bruta</p>
                    <div className="flex gap-4 mt-1">
                      <span className="text-xs text-gray-500">Comandas: <span className="text-gray-300">{fmt(d.receitaComandas)}</span></span>
                      <span className="text-xs text-gray-500">Avulsas: <span className="text-gray-300">{fmt(d.receitaAvulsa)}</span></span>
                    </div>
                  </div>
                  <span className="text-emerald-400 font-bold font-mono text-lg">{fmt(d.receita)}</span>
                </div>

                {/* CMV */}
                {d.custo > 0 ? (
                  <>
                    <div className="flex items-center justify-between py-2.5 border-b border-surface-600">
                      <p className="text-sm text-gray-400">(−) CMV — Custo das Mercadorias Vendidas</p>
                      <span className="text-red-400 font-mono">({fmt(d.custo)})</span>
                    </div>

                    {/* Lucro Bruto */}
                    <div className="flex items-center justify-between py-3 border-b-2 border-surface-400">
                      <p className="text-base font-black text-white">LUCRO BRUTO</p>
                      <div className="text-right">
                        <span className={`font-black font-mono text-xl ${d.margem >= 0 ? 'text-brand-400' : 'text-red-400'}`}>
                          {fmt(d.margem)}
                        </span>
                        <span className="text-xs text-gray-500 ml-2 font-mono">({d.margemPercent.toFixed(1)}%)</span>
                      </div>
                    </div>

                    {/* Crediários */}
                    {d.crediarios > 0 && (
                      <>
                        <div className="flex items-center justify-between py-2.5 border-b border-surface-600">
                          <p className="text-sm text-gray-400">(−) Crediários em Aberto</p>
                          <span className="text-amber-400 font-mono">({fmt(d.crediarios)})</span>
                        </div>
                        <div className="flex items-center justify-between py-2.5 border-b border-surface-600">
                          <p className="text-sm text-gray-300">Resultado Estimado</p>
                          <span className={`font-semibold font-mono ${d.margem - d.crediarios >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                            {fmt(d.margem - d.crediarios)}
                          </span>
                        </div>
                      </>
                    )}

                    {/* Projeção */}
                    {diasRestantes > 0 && mediaDiaria > 0 && preset === 'mes' && (
                      <div className="flex items-center justify-between py-3 mt-1 rounded-xl bg-brand-500/8 px-4 -mx-0">
                        <div>
                          <p className="text-xs font-semibold text-brand-300">📈 Projeção para o mês completo</p>
                          <p className="text-[11px] text-gray-500 mt-0.5">
                            {diasRestantes} dias restantes · média {fmt(mediaDiaria)}/dia
                          </p>
                        </div>
                        <span className="font-black font-mono text-brand-400 text-lg">{fmt(projecaoMes)}</span>
                      </div>
                    )}
                  </>
                ) : (
                  <p className="py-3 text-xs text-yellow-400/80">
                    Cadastre o preço de custo nos produtos para ver Lucro Bruto e CMV.
                  </p>
                )}
              </div>
            </div>
          )}

          {/* ── Meta de Faturamento ─────────────────────────────────────────── */}
          {d.receita > 0 && (
            <div className="card p-0 overflow-hidden">
              <div className="px-5 py-4 border-b border-surface-500 flex items-center justify-between flex-wrap gap-3">
                <div className="flex items-center gap-2">
                  <TrendingUp className="w-4 h-4 text-emerald-400" />
                  <h3 className="text-sm font-semibold text-gray-300">Meta de Faturamento</h3>
                </div>
                {/* Seletor margem alvo */}
                <div className="flex items-center gap-1.5">
                  <span className="text-xs text-gray-500">Margem alvo:</span>
                  {[30, 40, 50, 60].map(pct => (
                    <button key={pct} onClick={() => setTargetPct(pct)}
                      className={`px-2.5 py-1 rounded-lg text-xs font-bold border transition-all ${
                        targetPct === pct
                          ? 'bg-brand-500/20 border-brand-500/60 text-brand-300'
                          : 'bg-surface-700 border-surface-600 text-gray-400 hover:text-gray-200'
                      }`}>
                      {pct}%
                    </button>
                  ))}
                </div>
              </div>
              <div className="p-5 space-y-4">
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                  {/* Meta automática */}
                  <div className="bg-surface-800 rounded-xl p-4 border border-surface-600">
                    <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold mb-1">Meta Automática</p>
                    <p className="text-lg font-bold font-mono text-brand-400">{metaAuto > 0 ? fmt(metaAuto) : '—'}</p>
                    <p className="text-[10px] text-gray-500 mt-1">Custo ÷ (1 − {targetPct}%) · baseada no CMV do período</p>
                  </div>
                  {/* Meta manual */}
                  <div className="bg-surface-800 rounded-xl p-4 border border-surface-600">
                    <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold mb-1">Meta Manual</p>
                    <div className="flex items-center gap-2">
                      <span className="text-gray-400 text-sm">R$</span>
                      <input
                        className="input py-1 text-sm font-mono flex-1 min-w-0"
                        placeholder="Ex: 10000"
                        value={metaManualInput}
                        onChange={e => setMetaManualInput(e.target.value)}
                        type="number" min="0" step="0.01"
                      />
                      {metaManualInput && (
                        <button onClick={() => setMetaManualInput('')} className="text-gray-500 hover:text-gray-300 transition-colors">
                          <X className="w-4 h-4" />
                        </button>
                      )}
                    </div>
                    <p className="text-[10px] text-gray-500 mt-1">
                      {metaManualInput ? 'Meta manual ativa' : 'Vazio = usa meta automática'}
                    </p>
                  </div>
                  {/* Realizado */}
                  <div className="bg-surface-800 rounded-xl p-4 border border-surface-600">
                    <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold mb-1">Realizado</p>
                    <p className={`text-lg font-bold font-mono ${metaPct >= 100 ? 'text-emerald-400' : metaPct >= 75 ? 'text-brand-400' : metaPct >= 50 ? 'text-yellow-400' : 'text-red-400'}`}>
                      {fmt(d.receita)}
                    </p>
                    <p className="text-[10px] text-gray-500 mt-1">
                      Falta {metaFinal > d.receita ? fmt(metaFinal - d.receita) : 'Meta atingida!'}
                    </p>
                  </div>
                </div>

                {/* Barra de progresso */}
                {metaFinal > 0 && (
                  <div className="space-y-2">
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-gray-400">Progresso em direção à meta de {fmt(metaFinal)}</span>
                      <span className={`font-bold font-mono ${metaPct >= 100 ? 'text-emerald-400' : metaPct >= 75 ? 'text-brand-400' : metaPct >= 50 ? 'text-yellow-400' : 'text-red-400'}`}>
                        {metaPct.toFixed(1)}%
                      </span>
                    </div>
                    <div className="h-3 bg-surface-700 rounded-full overflow-hidden">
                      <div
                        className={`h-full rounded-full transition-all ${metaPct >= 100 ? 'bg-emerald-500' : metaPct >= 75 ? 'bg-brand-500' : metaPct >= 50 ? 'bg-yellow-500' : 'bg-red-500'}`}
                        style={{ width: `${Math.min(100, metaPct)}%` }}
                      />
                    </div>
                    <div className="flex justify-between text-[10px] text-gray-500">
                      <span>R$ 0</span>
                      {metaPct < 90 && <span style={{ marginLeft: `${Math.max(0, metaPct - 5)}%` }}>{fmt(d.receita)}</span>}
                      <span>{fmt(metaFinal)}</span>
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Gráfico + Donut */}
          <div className="grid grid-cols-1 lg:grid-cols-4 gap-4">
            <div className="lg:col-span-3"><BarChart dias={d.diaDia} /></div>
            <MargemDonut receita={d.receita} custo={d.custo} />
          </div>

          {/* Formas de pagamento */}
          {d.pagamentosPorForma.length > 0 && (
            <FormasPagamentoSection formas={d.pagamentosPorForma} />
          )}

          {/* Top produtos */}
          {d.topProdutos.length > 0 && (
            <div className="card p-0 overflow-hidden">
              {/* Header */}
              <div className="px-5 py-4 border-b border-surface-500 space-y-3">
                <div className="flex items-center justify-between flex-wrap gap-3">
                  <div className="flex items-center gap-2">
                    <Lightbulb className="w-4 h-4 text-yellow-400" />
                    <h3 className="text-sm font-semibold text-gray-300">
                      {tableView === 'analise' ? 'Top Produtos — Rentabilidade & Sugestão de Preço' : 'Top Produtos — Resumo de Vendas'}
                    </h3>
                  </div>
                  <div className="flex items-center gap-3 flex-wrap">
                    {/* Toggle Simples / Análise */}
                    <div className="flex rounded-lg overflow-hidden border border-surface-600 text-xs font-semibold">
                      <button
                        onClick={() => setTableView('simples')}
                        className={`px-3 py-1.5 transition-colors ${tableView === 'simples' ? 'bg-brand-600/30 text-brand-300' : 'text-gray-400 hover:text-gray-200'}`}
                      >Simples</button>
                      <button
                        onClick={() => setTableView('analise')}
                        className={`px-3 py-1.5 transition-colors border-l border-surface-600 ${tableView === 'analise' ? 'bg-brand-500/20 text-brand-300' : 'text-gray-400 hover:text-gray-200'}`}
                      >Análise</button>
                    </div>
                  </div>
                </div>

                {/* Filtro por origem */}
                <div className="flex flex-wrap gap-2 items-center">
                  <div className="flex rounded-lg overflow-hidden border border-surface-600 text-xs font-semibold">
                    {(['Todos', 'Comanda', 'PDV'] as const).map(o => (
                      <button
                        key={o}
                        onClick={() => setTopOrigemFilter(o)}
                        className={`flex items-center gap-1.5 px-3 py-1.5 transition-colors ${o !== 'Todos' ? 'border-l border-surface-600' : ''} ${
                          topOrigemFilter === o ? 'bg-brand-500/20 text-brand-300' : 'text-gray-400 hover:text-gray-200'
                        }`}
                      >
                        {o === 'Comanda' && <ShoppingCart className="w-3 h-3" />}
                        {o === 'PDV'     && <Store        className="w-3 h-3" />}
                        {o}
                      </button>
                    ))}
                  </div>

                  {/* Chips de categoria */}
                  {topCats.length > 0 && (
                    <div className="flex flex-wrap gap-1">
                      <button
                        onClick={() => setTopCatFilter(null)}
                        className={`px-2.5 py-1 rounded-lg text-xs font-medium border transition-all ${
                          topCatFilter === null
                            ? 'bg-surface-600 border-surface-400 text-white'
                            : 'bg-surface-700 border-surface-600 text-gray-400 hover:border-surface-500'
                        }`}
                      >Todas</button>
                      {topCats.map(cat => (
                        <button
                          key={cat}
                          onClick={() => setTopCatFilter(cat === topCatFilter ? null : cat)}
                          className={`px-2.5 py-1 rounded-lg text-xs font-medium border transition-all ${
                            topCatFilter === cat
                              ? 'bg-brand-600/30 border-brand-500 text-brand-200'
                              : 'bg-surface-700 border-surface-600 text-gray-400 hover:border-surface-500'
                          }`}
                        >{cat}</button>
                      ))}
                    </div>
                  )}

                  <span className="text-[11px] text-gray-500 ml-auto">
                    {topFiltered.length} produto{topFiltered.length !== 1 ? 's' : ''}
                    {(topOrigemFilter !== 'Todos' || topCatFilter) && ` filtrado${topFiltered.length !== 1 ? 's' : ''}`}
                  </span>
                </div>
              </div>

              <div className="overflow-x-auto">
                {tableView === 'simples' ? (
                  <table className="w-full text-sm">
                    <thead className="bg-surface-800">
                      <tr className="text-left">
                        {['#', 'Produto', 'Categoria', 'Qtd', 'Comanda', 'PDV', 'Receita'].map(h => (
                          <th key={h} className="px-4 py-2.5 text-xs text-gray-500 uppercase tracking-wider font-semibold whitespace-nowrap">{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-surface-500">
                      {topFiltered.map((p, i) => {
                        const qtdShow     = topOrigemFilter === 'Comanda' ? p.qtdComandas : topOrigemFilter === 'PDV' ? p.qtdAvulsa : p.qtd
                        const receitaShow = topOrigemFilter === 'Comanda' ? p.receitaComandas : topOrigemFilter === 'PDV' ? p.receitaAvulsa : p.receita
                        return (
                          <tr key={p.nome} className="hover:bg-surface-600/20 transition-colors">
                            <td className="px-4 py-2.5 text-gray-400 text-xs font-mono">{i + 1}</td>
                            <td className="px-4 py-2.5 font-medium text-white max-w-[200px]">
                              <p className="truncate">{p.nome}</p>
                            </td>
                            <td className="px-4 py-2.5">
                              <span className="text-[10px] bg-surface-600 text-gray-300 px-2 py-0.5 rounded-full whitespace-nowrap">{p.categoria || '—'}</span>
                            </td>
                            <td className="px-4 py-2.5 text-gray-300 text-xs font-mono font-semibold">{qtdShow}x</td>
                            <td className="px-4 py-2.5 text-brand-400 text-xs font-mono">
                              {p.qtdComandas > 0 ? `${p.qtdComandas}x` : <span className="text-gray-600">—</span>}
                            </td>
                            <td className="px-4 py-2.5 text-amber-400 text-xs font-mono">
                              {p.qtdAvulsa > 0 ? `${p.qtdAvulsa}x` : <span className="text-gray-600">—</span>}
                            </td>
                            <td className="px-4 py-2.5 font-mono text-emerald-400 font-bold text-sm">
                              {fmt(receitaShow)}
                              {topOrigemFilter === 'Todos' && p.receitaComandas > 0 && p.receitaAvulsa > 0 && (
                                <p className="text-[10px] text-gray-500 font-normal">
                                  <span className="text-brand-400/70">{fmt(p.receitaComandas)}</span> · <span className="text-amber-400/70">{fmt(p.receitaAvulsa)}</span>
                                </p>
                              )}
                            </td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                ) : (
                  <table className="w-full text-sm">
                    <thead className="bg-surface-800">
                      <tr className="text-left">
                        {['#', 'Produto', 'Qtd', 'Preço Médio', 'Custo Médio', 'Margem Atual', 'Sugestão', 'Ação'].map(h => (
                          <th key={h} className="px-4 py-2.5 text-xs text-gray-500 uppercase tracking-wider font-semibold whitespace-nowrap">{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-surface-500">
                      {topFiltered.map((p, i) => {
                        const qtdShow    = topOrigemFilter === 'Comanda' ? p.qtdComandas : topOrigemFilter === 'PDV' ? p.qtdAvulsa : p.qtd
                        const recShow    = topOrigemFilter === 'Comanda' ? p.receitaComandas : topOrigemFilter === 'PDV' ? p.receitaAvulsa : p.receita
                        const precoMedio  = qtdShow > 0 ? recShow / qtdShow : 0
                        const custoMedio  = p.qtd > 0 ? p.custo / p.qtd : 0
                        const margemAtual = precoMedio > 0 && custoMedio > 0
                          ? ((precoMedio - custoMedio) / precoMedio) * 100
                          : null
                        const precoSugerido = custoMedio > 0 ? custoMedio / (1 - targetPct / 100) : null
                        const diff = precoSugerido !== null ? precoSugerido - precoMedio : null
                        return (
                          <tr key={p.nome} className="hover:bg-white/5 transition-colors">
                            <td className="px-4 py-2.5 text-gray-400 text-xs font-mono">{i + 1}</td>
                            <td className="px-4 py-2.5 font-medium text-white max-w-[180px]">
                              <p className="truncate">{p.nome}</p>
                              <div className="flex items-center gap-2 mt-0.5">
                                <span className="text-[10px] bg-surface-600 text-gray-400 px-1.5 py-0 rounded">{p.categoria || '—'}</span>
                                {p.qtdComandas > 0 && <span className="text-[10px] text-brand-400/70">{p.qtdComandas}x cmd</span>}
                                {p.qtdAvulsa > 0   && <span className="text-[10px] text-amber-400/70">{p.qtdAvulsa}x pdv</span>}
                              </div>
                            </td>
                            <td className="px-4 py-2.5 text-gray-400 text-xs font-mono">{qtdShow}x</td>
                            <td className="px-4 py-2.5 font-mono text-gray-200 font-semibold text-xs">
                              {precoMedio > 0 ? fmt(precoMedio) : '—'}
                            </td>
                            <td className="px-4 py-2.5 font-mono text-red-400 text-xs">
                              {custoMedio > 0 ? fmt(custoMedio) : <span className="text-gray-600">—</span>}
                            </td>
                            <td className="px-4 py-2.5">
                              {margemAtual !== null ? (
                                <div className="flex items-center gap-2">
                                  <div className="w-12 h-1.5 bg-surface-600 rounded-full overflow-hidden">
                                    <div className={`h-full rounded-full ${margemAtual >= targetPct ? 'bg-emerald-500' : margemAtual >= 0 ? 'bg-purple-500' : 'bg-red-500'}`}
                                      style={{ width: `${Math.min(100, Math.abs(margemAtual))}%` }} />
                                  </div>
                                  <span className={`text-xs font-mono font-bold ${margemAtual >= targetPct ? 'text-emerald-400' : margemAtual >= 0 ? 'text-purple-400' : 'text-red-400'}`}>
                                    {margemAtual.toFixed(0)}%
                                  </span>
                                </div>
                              ) : <span className="text-gray-600 text-xs">—</span>}
                            </td>
                            <td className="px-4 py-2.5 font-mono text-xs">
                              {precoSugerido !== null
                                ? <span className="text-brand-400 font-semibold">{fmt(precoSugerido)}</span>
                                : <span className="text-gray-600">—</span>}
                            </td>
                            <td className="px-4 py-2.5">
                              {diff !== null ? (
                                Math.abs(diff) < 0.50 ? (
                                  <span className="flex items-center gap-1 text-xs text-emerald-400"><Minus className="w-3 h-3" /> Ok</span>
                                ) : diff > 0 ? (
                                  <span className="flex items-center gap-1 text-xs text-red-400 font-semibold"><ArrowUp className="w-3 h-3" /> +{fmt(diff)}</span>
                                ) : (
                                  <span className="flex items-center gap-1 text-xs text-emerald-400"><ArrowDown className="w-3 h-3" /> {fmt(diff)}</span>
                                )
                              ) : <span className="text-gray-600 text-xs">—</span>}
                            </td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                )}
                {/* Legenda */}
                <div className="px-4 py-3 border-t border-surface-600 flex flex-wrap gap-4 text-[11px] text-gray-500">
                  <span><span className="text-brand-400 font-bold">Comanda</span> = mesas · <span className="text-amber-400 font-bold">PDV</span> = balcão avulso</span>
                  {tableView === 'analise' && <>
                    <span><span className="text-brand-400 font-bold">Sugestão</span> = Custo Médio ÷ (1 − {targetPct}%)</span>
                    <span><ArrowUp className="w-3 h-3 text-red-400 inline" /> subir preço · <ArrowDown className="w-3 h-3 text-emerald-400 inline" /> pode baixar · <Minus className="w-3 h-3 text-emerald-400 inline" /> preço ok</span>
                  </>}
                </div>
              </div>
            </div>
          )}

          {/* Aviso sem custo */}
          {d.custo === 0 && d.receita > 0 && (
            <div className="flex items-start gap-3 rounded-xl bg-yellow-500/10 border border-yellow-500/20 p-4 text-sm text-yellow-300">
              <AlertCircle className="w-5 h-5 shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold">Preço de custo não cadastrado</p>
                <p className="text-yellow-400/70 mt-0.5">Cadastre o <strong>Preço de custo</strong> nos produtos em <a href="/admin/estoque" className="underline">Estoque</a> para ver a margem real.</p>
              </div>
            </div>
          )}

          {d.receita === 0 && (
            <div className="flex items-center gap-3 rounded-xl bg-surface-700 border border-surface-500 p-6 text-sm text-gray-400">
              <DollarSign className="w-5 h-5 text-gray-400" />
              Nenhuma venda registrada no período selecionado.
            </div>
          )}
        </>
      ) : null}
    </div>
  )
}
