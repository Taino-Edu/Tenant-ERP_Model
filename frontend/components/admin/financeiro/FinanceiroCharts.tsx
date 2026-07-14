'use client'
// Gráficos e filtros do financeiro: barra dia-a-dia, pizza por forma de
// pagamento, donut de margem e filtro rápido de datas. Extraído de page.tsx.
import { useEffect, useState } from 'react'
import { BarChart2 } from 'lucide-react'
import { FinanceiroDto, FormaPagamentoTotalDto } from '@/lib/api'
import { fmt, fmtShort, FORMA_LABELS, type Preset } from './financeiro-shared'

export function DateQuickFilter({ preset, onPreset, inicio, fim }: {
  preset: Preset
  onPreset: (p: Preset) => void
  inicio: string
  fim: string
}) {
  const LABELS: Record<Preset, string> = { hoje: 'Hoje', '7d': '7 dias', mes: 'Este mês', custom: 'Personalizado' }
  return (
    <div className="flex items-center gap-2 flex-wrap">
      <span className="text-[10px] text-gray-600 uppercase tracking-wider font-semibold shrink-0">Período:</span>
      {(['hoje', '7d', 'mes'] as Preset[]).map(p => (
        <button
          key={p}
          onClick={() => onPreset(p)}
          className={`px-2.5 py-1 rounded-lg text-xs font-medium border transition-all ${
            preset === p
              ? 'bg-brand-600/25 border-brand-500/60 text-brand-200'
              : 'bg-surface-700 border-surface-600 text-gray-400 hover:border-surface-500 hover:text-gray-200'
          }`}
        >
          {LABELS[p]}
        </button>
      ))}
      {preset === 'custom' && (
        <span className="text-xs text-gray-500 font-mono bg-surface-700 border border-surface-600 px-2 py-1 rounded-lg">
          {inicio.slice(5).replace('-', '/')} → {fim.slice(5).replace('-', '/')}
        </span>
      )}
      <span className="text-[10px] text-gray-600 ml-auto hidden sm:inline">
        ↑ filtro completo no topo
      </span>
    </div>
  )
}

// ── Gráfico SVG de barras animado ─────────────────────────────────────────────
export function BarChart({ dias, onDayClick }: {
  dias: FinanceiroDto['diaDia']
  onDayClick: (d: FinanceiroDto['diaDia'][0]) => void
}) {
  const [hovered, setHovered]   = useState<number | null>(null)
  const [mounted, setMounted]   = useState(false)

  useEffect(() => {
    const id = requestAnimationFrame(() => setMounted(true))
    return () => cancelAnimationFrame(id)
  }, [dias])

  if (dias.length === 0) return null

  const W = 700, H = 180, PAD = { top: 16, right: 8, bottom: 32, left: 56 }
  const chartW = W - PAD.left - PAD.right
  const chartH = H - PAD.top  - PAD.bottom
  const maxVal = Math.max(...dias.map(d => d.receita), 1)
  const barW   = Math.max(8, (chartW / dias.length) - 4)
  const slotPx = chartW / dias.length
  const labelStep = slotPx < 28 ? Math.ceil(28 / slotPx) : 1

  return (
    <div className="card relative">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
          <BarChart2 className="w-4 h-4 text-brand-400" /> Receita por dia
        </h3>
        <div className="flex items-center gap-4 text-xs text-gray-500">
          <span><span className="inline-block w-3 h-2 rounded-sm bg-brand-500 mr-1.5 align-middle" />Margem</span>
          <span><span className="inline-block w-3 h-2 rounded-sm bg-red-500/50 mr-1.5 align-middle" />Custo</span>
          <span className="text-[10px] text-gray-600 hidden sm:inline">clique para detalhar</span>
        </div>
      </div>
      <div className="overflow-x-auto">
        <svg viewBox={`0 0 ${W} ${H}`} className="w-full" style={{ minWidth: Math.max(400, dias.length * 28) }}>
          {/* Grid */}
          {[0.25, 0.5, 0.75, 1].map(f => {
            const gy = PAD.top + chartH * (1 - f)
            return (
              <g key={f}>
                <line x1={PAD.left} y1={gy} x2={W - PAD.right} y2={gy} stroke="#32323f" strokeWidth="1" />
                <text x={PAD.left - 6} y={gy + 4} textAnchor="end" fontSize="9" fill="#6b7280">
                  {fmtShort(maxVal * f)}
                </text>
              </g>
            )
          })}

          {/* Barras */}
          {dias.map((d, i) => {
            const slotW   = chartW / dias.length
            const x       = PAD.left + slotW * i + (slotW - barW) / 2
            const recH    = Math.max(2, (d.receita / maxVal) * chartH)
            const custoH  = d.custo > 0 ? Math.min((d.custo / maxVal) * chartH, recH - 1) : 0
            const margemH = recH - custoH
            const hasData = d.receita > 0
            const isHov   = hovered === i
            const showLabel = i === 0 || i === dias.length - 1 || i % labelStep === 0
            const delay   = `${i * 0.018}s`

            return (
              <g
                key={d.dia}
                onClick={hasData ? () => onDayClick(d) : undefined}
                onMouseEnter={hasData ? () => setHovered(i) : undefined}
                onMouseLeave={hasData ? () => setHovered(null) : undefined}
                className={hasData ? 'cursor-pointer' : ''}
              >
                {/* Hover highlight */}
                {isHov && (
                  <rect
                    x={x - 4} y={PAD.top} width={barW + 8} height={chartH}
                    fill="white" opacity="0.04" rx="4"
                  />
                )}

                {!hasData && (
                  <rect x={x + barW * 0.25} y={PAD.top + chartH - 1} width={barW * 0.5} height={1} fill="#32323f" />
                )}

                {/* Custo (vermelho) */}
                {hasData && custoH > 0 && (
                  <rect
                    x={x} y={PAD.top + chartH - recH}
                    width={barW} height={custoH}
                    fill={isHov ? 'rgba(239,68,68,0.75)' : 'rgba(239,68,68,0.5)'}
                    rx="2"
                    style={{
                      transformBox: 'fill-box',
                      transformOrigin: 'bottom',
                      transform: mounted ? 'scaleY(1)' : 'scaleY(0)',
                      transition: `transform 0.55s cubic-bezier(0.34,1.56,0.64,1) ${delay}`,
                    }}
                  />
                )}

                {/* Margem (brand azul) */}
                {hasData && (
                  <rect
                    x={x} y={PAD.top + chartH - recH + custoH}
                    width={barW} height={margemH}
                    fill={isHov ? '#64c8f5' : '#42B6EE'}
                    rx="2"
                    style={{
                      transformBox: 'fill-box',
                      transformOrigin: 'bottom',
                      transform: mounted ? 'scaleY(1)' : 'scaleY(0)',
                      transition: `transform 0.55s cubic-bezier(0.34,1.56,0.64,1) ${delay}`,
                      filter: isHov ? 'drop-shadow(0 0 4px rgba(66,182,238,0.5))' : undefined,
                    }}
                  />
                )}

                {/* Label de data */}
                {showLabel && (
                  <text
                    x={x + barW / 2} y={H - 4}
                    textAnchor="middle" fontSize="8"
                    fill={isHov ? '#d1d5db' : hasData ? '#9ca3af' : '#4b5563'}
                  >
                    {d.dia.slice(5)}
                  </text>
                )}

                {/* Tooltip inline ao hover */}
                {isHov && hasData && (() => {
                  const boxH = d.custo > 0 ? 30 : 20
                  const rawTy = PAD.top + chartH - recH - 8
                  const ty    = Math.max(PAD.top + boxH + 2, rawTy)
                  const tx    = Math.min(Math.max(x + barW / 2, PAD.left + 54), W - PAD.right - 54)
                  return (
                    <g style={{ pointerEvents: 'none' }}>
                      <rect x={tx - 54} y={ty - boxH} width="108" height={boxH} rx="5"
                        fill="#1e1e2e" stroke="#3f3f52" strokeWidth="1" opacity="0.97" />
                      <text x={tx} y={ty - boxH + 12} textAnchor="middle" fontSize="8.5" fill="#10b981" fontWeight="bold">
                        {fmt(d.receita)}
                      </text>
                      {d.custo > 0 && (
                        <text x={tx} y={ty - boxH + 24} textAnchor="middle" fontSize="7.5" fill="#9ca3af">
                          custo {fmt(d.custo)} · {((d.receita - d.custo) / d.receita * 100).toFixed(0)}% margem
                        </text>
                      )}
                    </g>
                  )
                })()}
              </g>
            )
          })}
        </svg>
      </div>
    </div>
  )
}

// ── Gráfico de pizza para análise de 1 dia ────────────────────────────────────
const FORMA_CORES: Record<string, string> = {
  Dinheiro:      '#10b981',
  Pix:           '#42B6EE',
  CartaoCredito: '#a855f7',
  CartaoDebito:  '#3b82f6',
  Crediario:     '#f59e0b',
  Pontos:        '#eab308',
  Cashback:      '#ec4899',
}

export function DayPieChart({ formas, receita, custo, date }: {
  formas: FormaPagamentoTotalDto[]
  receita: number
  custo: number
  date: string
}) {
  const [hovered, setHovered] = useState<string | null>(null)
  const total = formas.reduce((s, f) => s + f.total, 0)
  if (total === 0) return null

  const cx = 100, cy = 100, r = 78, innerR = 42

  function polarXY(angleDeg: number, radius: number) {
    const rad = (angleDeg - 90) * (Math.PI / 180)
    return { x: cx + radius * Math.cos(rad), y: cy + radius * Math.sin(rad) }
  }

  function arcPath(sa: number, ea: number, outerR: number) {
    const p1 = polarXY(sa, outerR), p2 = polarXY(ea, outerR)
    const p3 = polarXY(ea, innerR), p4 = polarXY(sa, innerR)
    const large = ea - sa > 180 ? 1 : 0
    return `M ${p1.x} ${p1.y} A ${outerR} ${outerR} 0 ${large} 1 ${p2.x} ${p2.y} L ${p3.x} ${p3.y} A ${innerR} ${innerR} 0 ${large} 0 ${p4.x} ${p4.y} Z`
  }

  let angle = 0
  const slices = formas.filter(f => f.total > 0).map(f => {
    const pct = f.total / total
    const sa  = angle, ea = angle + pct * 360
    angle = ea
    return { ...f, pct, sa, ea }
  })

  const hoveredForma = slices.find(s => s.forma === hovered)

  return (
    <div className="card">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
          <BarChart2 className="w-4 h-4 text-brand-400" /> Receita do dia · {date.slice(5).replace('-', '/')}
        </h3>
      </div>
      <div className="flex flex-col sm:flex-row items-center gap-6">
        <div className="shrink-0">
          <svg width="200" height="200" viewBox="0 0 200 200">
            {slices.map(s => {
              const isHov  = hovered === s.forma
              const outerR = isHov ? r + 7 : r
              const color  = FORMA_CORES[s.forma] ?? '#6b7280'
              // Quando o slice é 100% o arco SVG degenera (ponto inicial = final).
              // Nesse caso renderiza dois semicírculos para formar o anel completo.
              const isFull = s.ea - s.sa >= 359.99
              return isFull ? (
                <g key={s.forma}
                  onMouseEnter={() => setHovered(s.forma)}
                  onMouseLeave={() => setHovered(null)}
                  className="cursor-pointer"
                >
                  <path d={arcPath(s.sa, s.sa + 179.99, outerR)} fill={color}
                    opacity={hovered && !isHov ? 0.4 : 1}
                    style={{ filter: isHov ? 'brightness(1.15)' : undefined }} />
                  <path d={arcPath(s.sa + 180, s.sa + 359.99, outerR)} fill={color}
                    opacity={hovered && !isHov ? 0.4 : 1}
                    style={{ filter: isHov ? 'brightness(1.15)' : undefined }} />
                </g>
              ) : (
                <path
                  key={s.forma}
                  d={arcPath(s.sa, s.ea, outerR)}
                  fill={color}
                  opacity={hovered && !isHov ? 0.4 : 1}
                  onMouseEnter={() => setHovered(s.forma)}
                  onMouseLeave={() => setHovered(null)}
                  className="cursor-pointer transition-all duration-150"
                  style={{ filter: isHov ? 'brightness(1.15)' : undefined }}
                />
              )
            })}
            <text x={cx} y={cy - 10} textAnchor="middle" fontSize="9" fill="#9ca3af">
              {hoveredForma ? (FORMA_LABELS[hoveredForma.forma] ?? hoveredForma.forma) : 'Total'}
            </text>
            <text x={cx} y={cy + 6} textAnchor="middle" fontSize="13" fontWeight="bold" fill="white">
              {hoveredForma ? fmt(hoveredForma.total) : fmt(receita)}
            </text>
            {hoveredForma && (
              <text x={cx} y={cy + 20} textAnchor="middle" fontSize="9" fill="#9ca3af">
                {(hoveredForma.pct * 100).toFixed(1)}%
              </text>
            )}
          </svg>
        </div>
        <div className="flex-1 space-y-1 min-w-0 w-full">
          {slices.map(s => {
            const color = FORMA_CORES[s.forma] ?? '#6b7280'
            return (
              <div
                key={s.forma}
                className={`flex items-center justify-between gap-3 rounded-lg px-3 py-2 transition-colors cursor-default ${hovered === s.forma ? 'bg-surface-700' : 'hover:bg-surface-700/50'}`}
                onMouseEnter={() => setHovered(s.forma)}
                onMouseLeave={() => setHovered(null)}
              >
                <div className="flex items-center gap-2 min-w-0">
                  <div className="w-2.5 h-2.5 rounded-full shrink-0" style={{ background: color }} />
                  <span className="text-xs text-gray-300 truncate">{FORMA_LABELS[s.forma] ?? s.forma}</span>
                </div>
                <div className="flex items-center gap-3 shrink-0">
                  <span className="text-xs text-gray-500">{s.quantidade}×</span>
                  <span className="text-xs font-mono font-bold text-white">{fmt(s.total)}</span>
                  <span className="text-xs text-gray-500 w-10 text-right">{(s.pct * 100).toFixed(1)}%</span>
                </div>
              </div>
            )
          })}
          {custo > 0 && (
            <div className="mt-2 pt-2 border-t border-surface-600 flex items-center justify-between px-3">
              <span className="text-xs text-gray-500">Margem estimada</span>
              <span className={`text-xs font-mono font-bold ${receita > custo ? 'text-emerald-400' : 'text-red-400'}`}>
                {fmt(receita - custo)} · {receita > 0 ? (((receita - custo) / receita) * 100).toFixed(1) : 0}%
              </span>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

// ── Donut custo vs receita ─────────────────────────────────────────────────────
export function MargemDonut({ receita, custo }: { receita: number; custo: number }) {
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
