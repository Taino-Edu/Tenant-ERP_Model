'use client'
import { useEffect, useState, useCallback } from 'react'
import { analyticsApi, FinanceiroDto } from '@/lib/api'
import toast from 'react-hot-toast'
import {
  TrendingUp, TrendingDown, DollarSign, AlertCircle,
  RefreshCw, Printer, Package, ShoppingBag, BarChart2,
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

// ── KPI Card ──────────────────────────────────────────────────────────────────
function KpiCard({ label, value, sub, color = 'brand', icon: Icon }: {
  label: string; value: string; sub?: string
  color?: string; icon: React.ElementType
}) {
  const colors: Record<string, string> = {
    brand: 'text-brand-400',   green: 'text-emerald-400',
    red:   'text-red-400',     yellow: 'text-yellow-400',
  }
  const bgs: Record<string, string> = {
    brand: 'bg-brand-600/15',  green: 'bg-emerald-500/15',
    red:   'bg-red-500/15',    yellow: 'bg-yellow-500/15',
  }
  return (
    <div className="card flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">{label}</p>
        <div className={`w-8 h-8 rounded-lg flex items-center justify-center ${bgs[color]}`}>
          <Icon className={`w-4 h-4 ${colors[color]}`} />
        </div>
      </div>
      <p className={`text-2xl font-bold font-mono ${colors[color] ?? colors.brand}`}>{value}</p>
      {sub && <p className="text-xs text-gray-500">{sub}</p>}
    </div>
  )
}

// ── Gráfico SVG de barras ─────────────────────────────────────────────────────
function BarChart({ dias }: { dias: FinanceiroDto['diaDia'] }) {
  const [tooltip, setTooltip] = useState<{ x: number; y: number; d: FinanceiroDto['diaDia'][0] } | null>(null)
  if (dias.length === 0) return null

  const W = 700, H = 180, PAD = { top: 16, right: 8, bottom: 32, left: 56 }
  const chartW = W - PAD.left - PAD.right
  const chartH = H - PAD.top  - PAD.bottom
  const maxVal = Math.max(...dias.map(d => d.receita), 1)
  const barW   = Math.max(8, (chartW / dias.length) - 4)

  // Grid lines
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
          {/* Grid */}
          {gridLines.map(g => (
            <g key={g.val}>
              <line x1={PAD.left} y1={g.y} x2={W - PAD.right} y2={g.y} stroke="#32323f" strokeWidth="1" />
              <text x={PAD.left - 6} y={g.y + 4} textAnchor="end" fontSize="9" fill="#6b7280">
                {fmtShort(g.val)}
              </text>
            </g>
          ))}

          {/* Barras */}
          {dias.map((d, i) => {
            const x       = PAD.left + (chartW / dias.length) * i + (chartW / dias.length - barW) / 2
            const recH    = Math.max(2, (d.receita / maxVal) * chartH)
            const custoH  = d.custo > 0 ? Math.min((d.custo / maxVal) * chartH, recH) : 0
            const margemH = recH - custoH

            return (
              <g
                key={d.dia}
                onMouseEnter={e => setTooltip({ x: e.clientX, y: e.clientY, d })}
                onMouseLeave={() => setTooltip(null)}
                className="cursor-pointer"
              >
                {/* Custo */}
                {custoH > 0 && (
                  <rect
                    x={x} y={PAD.top + chartH - recH}
                    width={barW} height={custoH}
                    fill="rgba(239,68,68,0.4)" rx="2"
                  />
                )}
                {/* Margem */}
                <rect
                  x={x} y={PAD.top + chartH - recH}
                  width={barW} height={margemH}
                  fill="#7839F3" rx="2"
                  style={{ transition: 'opacity 0.1s' }}
                />
                {/* Label dia */}
                <text
                  x={x + barW / 2} y={H - 4}
                  textAnchor="middle" fontSize="8" fill="#6b7280"
                >
                  {d.dia.slice(5)}
                </text>
              </g>
            )
          })}
        </svg>
      </div>

      {/* Tooltip */}
      {tooltip && (
        <div
          className="fixed z-50 bg-surface-700 border border-surface-500 rounded-lg px-3 py-2 text-xs shadow-xl pointer-events-none"
          style={{ left: tooltip.x + 12, top: tooltip.y - 48 }}
        >
          <p className="font-semibold text-white mb-1">{tooltip.d.dia}</p>
          <p className="text-emerald-400">Receita: {fmt(tooltip.d.receita)}</p>
          {tooltip.d.custo > 0 && <p className="text-red-400">Custo: {fmt(tooltip.d.custo)}</p>}
          <p className="text-brand-400">Margem: {fmt(tooltip.d.receita - tooltip.d.custo)}</p>
        </div>
      )}
    </div>
  )
}

// ── Donut de distribuição ─────────────────────────────────────────────────────
function MargemDonut({ receita, custo }: { receita: number; custo: number }) {
  if (receita <= 0) return null
  const pct      = Math.min(100, custo > 0 ? (custo / receita) * 100 : 0)
  const r        = 42
  const circ     = 2 * Math.PI * r
  const dash     = (pct / 100) * circ

  return (
    <div className="card flex flex-col items-center justify-center gap-3 py-6">
      <h3 className="text-sm font-semibold text-gray-300 self-start">Custo vs Receita</h3>
      <svg width="120" height="120" viewBox="0 0 120 120">
        <circle cx="60" cy="60" r={r} fill="none" stroke="#7839F3" strokeWidth="14" />
        <circle
          cx="60" cy="60" r={r} fill="none"
          stroke="rgba(239,68,68,0.5)" strokeWidth="14"
          strokeDasharray={`${dash} ${circ - dash}`}
          strokeDashoffset={circ / 4}
          strokeLinecap="round"
        />
        <text x="60" y="56" textAnchor="middle" fontSize="14" fontWeight="bold" fill="white">
          {(100 - pct).toFixed(0)}%
        </text>
        <text x="60" y="70" textAnchor="middle" fontSize="9" fill="#9ca3af">margem</text>
      </svg>
      <div className="flex gap-4 text-xs">
        <span className="flex items-center gap-1.5 text-brand-400">
          <span className="w-2.5 h-2.5 rounded-full bg-brand-500" />Margem {(100 - pct).toFixed(1)}%
        </span>
        <span className="flex items-center gap-1.5 text-red-400">
          <span className="w-2.5 h-2.5 rounded-full bg-red-500/60" />Custo {pct.toFixed(1)}%
        </span>
      </div>
    </div>
  )
}

// ── Preset de período ──────────────────────────────────────────────────────────
type Preset = 'hoje' | '7d' | 'mes' | 'custom'

function getRange(preset: Preset) {
  const now  = new Date()
  const hoje = toDateInput(now)
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
  const [preset,  setPreset]  = useState<Preset>('mes')
  const [inicio,  setInicio]  = useState(getRange('mes').inicio)
  const [fim,     setFim]     = useState(getRange('mes').fim)
  const [data,    setData]    = useState<FinanceiroDto | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async (ini: string, f: string) => {
    setLoading(true)
    try   { const res = await analyticsApi.financeiro(ini, f); setData(res.data) }
    catch { toast.error('Erro ao carregar dados financeiros') }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load(inicio, fim) }, []) // eslint-disable-line

  function applyPreset(p: Preset) {
    setPreset(p)
    if (p !== 'custom') {
      const { inicio: ini, fim: f } = getRange(p)
      setInicio(ini); setFim(f); load(ini, f)
    }
  }

  const d = data

  return (
    <div className="p-6 space-y-6 print:p-0">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3 print:hidden">
        <div>
          <h1 className="text-2xl font-bold text-white">Controle Financeiro</h1>
          <p className="text-gray-400 text-sm mt-0.5">Receita, custo e margem do período</p>
        </div>
        <div className="flex gap-2">
          <button onClick={() => window.print()} disabled={!d} className="btn-secondary text-sm print:hidden">
            <Printer className="w-4 h-4" /> Exportar PDF
          </button>
          <button onClick={() => load(inicio, fim)} disabled={loading} className="btn-secondary text-sm">
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>
        </div>
      </div>

      {/* Filtros */}
      <div className="card flex flex-wrap gap-3 items-end print:hidden">
        <div className="flex gap-2 flex-wrap">
          {(['hoje', '7d', 'mes', 'custom'] as Preset[]).map(p => (
            <button key={p} onClick={() => applyPreset(p)}
              className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-all ${
                preset === p ? 'bg-brand-600 text-white' : 'bg-surface-700 text-gray-400 hover:text-white'
              }`}
            >
              {{ hoje: 'Hoje', '7d': '7 dias', mes: 'Este mês', custom: 'Personalizado' }[p]}
            </button>
          ))}
        </div>
        {preset === 'custom' && (
          <div className="flex items-center gap-2 flex-wrap">
            <input type="date" className="input py-1 text-sm" value={inicio} onChange={e => setInicio(e.target.value)} />
            <span className="text-gray-500 text-sm">até</span>
            <input type="date" className="input py-1 text-sm" value={fim} onChange={e => setFim(e.target.value)} />
            <button onClick={() => { setPreset('custom'); load(inicio, fim) }} className="btn-primary text-sm py-1.5">Filtrar</button>
          </div>
        )}
      </div>

      {loading ? (
        <div className="flex justify-center py-20">
          <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>
      ) : d ? (
        <>
          {/* KPIs */}
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
            <KpiCard label="Receita total"      value={fmt(d.receita)}          sub={`${d.diaDia.length} dias`}                    color="green"  icon={TrendingUp}   />
            <KpiCard label="Custo estimado"     value={fmt(d.custo)}            sub="Preço de custo dos itens"                     color="red"    icon={ShoppingBag}  />
            <KpiCard label="Margem bruta"       value={fmt(d.margem)}           sub={`${d.margemPercent.toFixed(1)}% sobre receita`} color={d.margem >= 0 ? 'brand' : 'red'} icon={d.margem >= 0 ? TrendingUp : TrendingDown} />
            <KpiCard label="Crediários abertos" value={fmt(d.crediarios)}       sub="A receber"                                    color="yellow" icon={AlertCircle}  />
          </div>

          {/* Gráfico + Donut */}
          <div className="grid grid-cols-1 lg:grid-cols-4 gap-4">
            <div className="lg:col-span-3">
              <BarChart dias={d.diaDia} />
            </div>
            <MargemDonut receita={d.receita} custo={d.custo} />
          </div>

          {/* Top produtos */}
          {d.topProdutos.length > 0 && (
            <div className="card p-0 overflow-hidden">
              <div className="px-5 py-4 border-b border-surface-500 flex items-center gap-2">
                <Package className="w-4 h-4 text-brand-400" />
                <h3 className="text-sm font-semibold text-gray-300">Top Produtos — Receita &amp; Margem</h3>
              </div>
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="bg-surface-800">
                    <tr className="text-left">
                      {['#', 'Produto', 'Qtd', 'Receita', 'Custo', 'Margem', 'Margem %'].map(h => (
                        <th key={h} className="px-4 py-2.5 text-xs text-gray-500 uppercase tracking-wider font-semibold">{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-surface-500">
                    {d.topProdutos.map((p, i) => {
                      const pct = p.custo > 0 ? ((p.margem / p.receita) * 100) : null
                      return (
                        <tr key={p.nome} className="hover:bg-surface-600/20 transition-colors">
                          <td className="px-4 py-2.5 text-gray-600 text-xs font-mono">{i + 1}</td>
                          <td className="px-4 py-2.5 font-medium text-white">{p.nome}</td>
                          <td className="px-4 py-2.5 text-gray-400">{p.qtd}x</td>
                          <td className="px-4 py-2.5 font-mono text-accent-gold font-semibold">{fmt(p.receita)}</td>
                          <td className="px-4 py-2.5 font-mono text-gray-400 text-xs">{p.custo > 0 ? fmt(p.custo) : <span className="text-gray-600">—</span>}</td>
                          <td className="px-4 py-2.5 font-mono text-xs">
                            {p.custo > 0
                              ? <span className={p.margem >= 0 ? 'text-emerald-400 font-semibold' : 'text-red-400'}>{fmt(p.margem)}</span>
                              : <span className="text-gray-600">—</span>}
                          </td>
                          <td className="px-4 py-2.5">
                            {pct !== null ? (
                              <div className="flex items-center gap-2">
                                <div className="flex-1 h-1.5 bg-surface-600 rounded-full overflow-hidden" style={{ minWidth: 48 }}>
                                  <div className={`h-full rounded-full ${pct >= 0 ? 'bg-emerald-500' : 'bg-red-500'}`} style={{ width: `${Math.min(100, Math.abs(pct))}%` }} />
                                </div>
                                <span className={`text-xs font-mono ${pct >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>{pct.toFixed(0)}%</span>
                              </div>
                            ) : <span className="text-gray-600 text-xs">—</span>}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
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
              <DollarSign className="w-5 h-5 text-gray-600" />
              Nenhuma venda registrada no período selecionado.
            </div>
          )}
        </>
      ) : null}
    </div>
  )
}
