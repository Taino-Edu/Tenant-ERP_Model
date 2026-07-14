'use client'
// Modal de detalhe de um dia do gráfico. Extraído de financeiro/page.tsx.
import { useEffect, useState } from 'react'
import { X } from 'lucide-react'
import { FinanceiroDto } from '@/lib/api'
import { fmt } from './financeiro-shared'

export function DayDetailModal({ day, onClose }: {
  day: FinanceiroDto['diaDia'][0]
  onClose: () => void
}) {
  const margem    = day.receita - day.custo
  const margemPct = day.receita > 0 && day.custo > 0 ? (margem / day.receita) * 100 : 0
  const custoPct  = day.receita > 0 && day.custo > 0 ? (day.custo / day.receita) * 100 : 0
  const r = 38, circ = 2 * Math.PI * r
  const dash = (Math.min(100, Math.max(0, custoPct)) / 100) * circ

  const [animated, setAnimated] = useState(false)
  useEffect(() => { const id = requestAnimationFrame(() => setAnimated(true)); return () => cancelAnimationFrame(id) }, [])

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [onClose])

  const dayLabel = (() => {
    try {
      return new Date(day.dia + 'T12:00:00').toLocaleDateString('pt-BR', {
        weekday: 'long', day: '2-digit', month: 'long',
      })
    } catch { return day.dia }
  })()

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm"
      onClick={e => { if (e.target === e.currentTarget) onClose() }}
    >
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-sm max-h-[85vh] overflow-y-auto shadow-2xl">
        {/* Header */}
        <div className="px-5 py-4 border-b border-surface-600 bg-gradient-to-r from-brand-600/10 to-transparent flex items-center justify-between sticky top-0">
          <div>
            <p className="text-[10px] text-gray-500 uppercase tracking-wider font-semibold">Resumo do dia</p>
            <h3 className="font-semibold text-white capitalize mt-0.5">{dayLabel}</h3>
          </div>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-300 transition-colors p-1">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-5 space-y-4">
          {/* Donut + valores */}
          <div className="flex items-center gap-5">
            <div className="relative shrink-0">
              <svg width="96" height="96" viewBox="0 0 96 96">
                {/* Track */}
                <circle cx="48" cy="48" r={r} fill="none" stroke="#42B6EE"
                  strokeWidth="11" opacity="0.9" />
                {/* Custo overlay */}
                {day.custo > 0 && (
                  <circle cx="48" cy="48" r={r} fill="none"
                    stroke="rgba(239,68,68,0.6)" strokeWidth="11"
                    strokeDasharray={`${animated ? dash : 0} ${circ}`}
                    strokeDashoffset={circ / 4} strokeLinecap="round"
                    style={{ transition: 'stroke-dasharray 0.9s cubic-bezier(0.4,0,0.2,1)' }}
                  />
                )}
                <text x="48" y="44" textAnchor="middle" fontSize="15" fontWeight="bold" fill="white">
                  {day.custo > 0 ? `${margemPct.toFixed(0)}%` : '—'}
                </text>
                <text x="48" y="58" textAnchor="middle" fontSize="8" fill="#9ca3af">
                  {day.custo > 0 ? 'margem' : 'sem custo'}
                </text>
              </svg>
            </div>

            <div className="flex-1 space-y-3 min-w-0">
              <div>
                <p className="text-[10px] text-gray-500 uppercase tracking-wider font-semibold">Receita</p>
                <p className="text-2xl font-black font-mono text-emerald-400 leading-tight">{fmt(day.receita)}</p>
              </div>
              {day.custo > 0 && (
                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <p className="text-[10px] text-gray-500 uppercase tracking-wider">Custo</p>
                    <p className="text-sm font-bold font-mono text-red-400">{fmt(day.custo)}</p>
                  </div>
                  <div>
                    <p className="text-[10px] text-gray-500 uppercase tracking-wider">Margem</p>
                    <p className={`text-sm font-bold font-mono ${margem >= 0 ? 'text-brand-400' : 'text-red-400'}`}>
                      {fmt(margem)}
                    </p>
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Barra custo vs margem */}
          {day.custo > 0 && (
            <div className="space-y-1.5">
              <div className="h-2.5 bg-surface-700 rounded-full overflow-hidden flex gap-px">
                <div
                  className="h-full bg-brand-500 rounded-l-full"
                  style={{
                    width: animated ? `${margemPct}%` : '0%',
                    transition: 'width 0.8s cubic-bezier(0.4,0,0.2,1) 0.1s',
                  }}
                />
                <div
                  className="h-full bg-red-500/60 rounded-r-full"
                  style={{
                    width: animated ? `${custoPct}%` : '0%',
                    transition: 'width 0.8s cubic-bezier(0.4,0,0.2,1) 0.15s',
                  }}
                />
              </div>
              <div className="flex justify-between text-[10px]">
                <span className="text-brand-400 font-semibold">Margem {margemPct.toFixed(1)}%</span>
                <span className="text-red-400 font-semibold">Custo {custoPct.toFixed(1)}%</span>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

// ── Mini filtro de período (próximo ao gráfico) ────────────────────────────────
