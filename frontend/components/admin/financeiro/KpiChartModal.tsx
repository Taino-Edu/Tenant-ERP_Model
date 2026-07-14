'use client'
// Modal de gráfico de KPI + helpers de tendência. Extraído de financeiro/page.tsx.
import { useEffect } from 'react'
import { X } from 'lucide-react'
import { fmtShort, type Preset } from './financeiro-shared'

export interface ChartPoint { label: string; value: number }

export function KpiChartModal({
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

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [onClose])

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
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-lg max-h-[85vh] overflow-y-auto shadow-2xl">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-surface-600 sticky top-0 bg-surface-800">
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

// KPI card unificado em components/admin/StatCard.tsx — mapeamento de tone:
// green→success, red→danger, yellow→warning, brand→brand.
export function kpiTrend(change: number | null, label: string): { value: number; label: string } | undefined {
  return change != null ? { value: change, label } : undefined
}

// Rótulo do período de comparação, de acordo com o preset ativo — cada preset
// compara com um período anterior diferente (mês com mês, hoje com ontem etc.)
export function prevPeriodLabel(p: Preset): string {
  if (p === 'mes')  return 'vs mês ant.'
  if (p === 'hoje') return 'vs ontem'
  return 'vs período ant.'
}

// ── Formas de pagamento com filtros ───────────────────────────────────────────
