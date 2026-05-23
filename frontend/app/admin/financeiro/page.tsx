'use client'
import { useEffect, useState, useCallback } from 'react'
import { analyticsApi, FinanceiroDto } from '@/lib/api'
import toast from 'react-hot-toast'
import { TrendingUp, TrendingDown, DollarSign, AlertCircle, RefreshCw, Printer } from 'lucide-react'

// ── Helpers ───────────────────────────────────────────────────────────────────

function toDateInput(d: Date) {
  return d.toISOString().split('T')[0]
}

function fmt(value: number) {
  return `R$ ${value.toFixed(2).replace('.', ',').replace(/\B(?=(\d{3})+(?!\d))/g, '.')}`
}

function KpiCard({
  label, value, sub, color = 'brand',
}: { label: string; value: string; sub?: string; color?: string }) {
  const colors: Record<string, string> = {
    brand:   'text-brand-400',
    green:   'text-emerald-400',
    red:     'text-red-400',
    yellow:  'text-yellow-400',
    gray:    'text-gray-400',
  }
  return (
    <div className="card flex flex-col gap-1">
      <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">{label}</p>
      <p className={`text-2xl font-bold font-mono ${colors[color] ?? colors.brand}`}>{value}</p>
      {sub && <p className="text-xs text-gray-500">{sub}</p>}
    </div>
  )
}

// ── Mini bar chart ─────────────────────────────────────────────────────────────

function MiniBar({ dias }: { dias: FinanceiroDto['diaDia'] }) {
  if (dias.length === 0) return null
  const maxVal = Math.max(...dias.map(d => d.receita), 1)

  return (
    <div className="card">
      <h3 className="text-sm font-semibold text-gray-300 mb-4">Receita por dia</h3>
      <div className="flex items-end gap-1 h-28 overflow-x-auto pb-1">
        {dias.map(d => (
          <div key={d.dia} className="flex flex-col items-center gap-1 flex-1 min-w-[24px]">
            <div className="w-full flex flex-col justify-end" style={{ height: '80px' }}>
              {/* Custo */}
              {d.custo > 0 && (
                <div
                  className="w-full bg-red-500/30 rounded-t-sm"
                  style={{ height: `${Math.round((d.custo / maxVal) * 80)}px` }}
                  title={`Custo: ${fmt(d.custo)}`}
                />
              )}
              {/* Receita acima do custo */}
              <div
                className="w-full bg-brand-500 rounded-t-sm"
                style={{ height: `${Math.max(2, Math.round(((d.receita - d.custo) / maxVal) * 80))}px` }}
                title={`Receita: ${fmt(d.receita)}`}
              />
            </div>
            <span className="text-[9px] text-gray-600 rotate-45 origin-left whitespace-nowrap">{d.dia}</span>
          </div>
        ))}
      </div>
      <div className="flex items-center gap-4 mt-3 text-xs text-gray-500">
        <span><span className="inline-block w-3 h-3 rounded-sm bg-brand-500 mr-1.5 align-middle" />Margem</span>
        <span><span className="inline-block w-3 h-3 rounded-sm bg-red-500/30 mr-1.5 align-middle" />Custo</span>
      </div>
    </div>
  )
}

// ── Preset de período ──────────────────────────────────────────────────────────

type Preset = 'hoje' | '7d' | 'mes' | 'custom'

function getRange(preset: Preset): { inicio: string; fim: string } {
  const now  = new Date()
  const hoje = toDateInput(now)
  if (preset === 'hoje') return { inicio: hoje, fim: hoje }
  if (preset === '7d') {
    const ini = new Date(now); ini.setDate(ini.getDate() - 6)
    return { inicio: toDateInput(ini), fim: hoje }
  }
  // mes
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
    try {
      const res = await analyticsApi.financeiro(ini, f)
      setData(res.data)
    } catch {
      toast.error('Erro ao carregar dados financeiros')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load(inicio, fim) }, []) // eslint-disable-line react-hooks/exhaustive-deps

  function applyPreset(p: Preset) {
    setPreset(p)
    if (p !== 'custom') {
      const { inicio: ini, fim: f } = getRange(p)
      setInicio(ini); setFim(f)
      load(ini, f)
    }
  }

  function applyCustom() {
    setPreset('custom')
    load(inicio, fim)
  }

  const d = data

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-bold text-white">Controle Financeiro</h1>
          <p className="text-gray-400 text-sm mt-0.5">Receita, custo e margem do período</p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => window.print()}
            disabled={loading || !data}
            className="btn-secondary text-sm print:hidden"
          >
            <Printer className="w-4 h-4" />
            Exportar PDF
          </button>
          <button
            onClick={() => load(inicio, fim)}
            disabled={loading}
            className="btn-secondary text-sm print:hidden"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            Atualizar
          </button>
        </div>
      </div>

      {/* Filtros de período */}
      <div className="card flex flex-wrap gap-3 items-end">
        <div className="flex gap-2">
          {(['hoje', '7d', 'mes', 'custom'] as Preset[]).map(p => (
            <button
              key={p}
              onClick={() => applyPreset(p)}
              className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-all ${
                preset === p
                  ? 'bg-brand-600 text-white'
                  : 'bg-surface-700 text-gray-400 hover:text-white'
              }`}
            >
              {{ hoje: 'Hoje', '7d': '7 dias', mes: 'Este mês', custom: 'Personalizado' }[p]}
            </button>
          ))}
        </div>
        {preset === 'custom' && (
          <div className="flex items-center gap-2 flex-wrap">
            <input type="date" className="input py-1 text-sm" value={inicio} onChange={e => setInicio(e.target.value)} />
            <span className="text-gray-500">até</span>
            <input type="date" className="input py-1 text-sm" value={fim} onChange={e => setFim(e.target.value)} />
            <button onClick={applyCustom} className="btn-primary text-sm py-1.5">Filtrar</button>
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
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <KpiCard
              label="Receita total"
              value={fmt(d.receita)}
              sub="Comandas + Vendas avulsas"
              color="green"
            />
            <KpiCard
              label="Custo estimado"
              value={fmt(d.custo)}
              sub="Soma do preço de custo dos itens"
              color="red"
            />
            <KpiCard
              label="Margem bruta"
              value={fmt(d.margem)}
              sub={`${d.margemPercent.toFixed(1)}% sobre o custo`}
              color={d.margem >= 0 ? 'brand' : 'red'}
            />
            <KpiCard
              label="Crediários em aberto"
              value={fmt(d.crediarios)}
              sub="Valores a receber"
              color="yellow"
            />
          </div>

          {/* Margem visual */}
          {d.receita > 0 && (
            <div className="card">
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm text-gray-400">Divisão Receita × Custo</span>
                {d.margem >= 0
                  ? <span className="flex items-center gap-1 text-emerald-400 text-sm font-semibold"><TrendingUp className="w-4 h-4" /> Lucrativo</span>
                  : <span className="flex items-center gap-1 text-red-400 text-sm font-semibold"><TrendingDown className="w-4 h-4" /> Prejuízo</span>
                }
              </div>
              <div className="h-4 rounded-full bg-surface-700 overflow-hidden flex">
                <div
                  className="bg-red-500/70 h-full transition-all"
                  style={{ width: `${d.receita > 0 ? Math.min(100, (d.custo / d.receita) * 100) : 0}%` }}
                  title={`Custo: ${fmt(d.custo)}`}
                />
                <div className="bg-emerald-500 h-full flex-1" title={`Margem: ${fmt(d.margem)}`} />
              </div>
              <div className="flex justify-between text-xs text-gray-500 mt-1">
                <span>Custo {d.receita > 0 ? ((d.custo / d.receita) * 100).toFixed(1) : 0}%</span>
                <span>Margem {d.receita > 0 ? ((d.margem / d.receita) * 100).toFixed(1) : 0}%</span>
              </div>
            </div>
          )}

          {/* Gráfico dia a dia */}
          {d.diaDia.length > 1 && <MiniBar dias={d.diaDia} />}

          {/* Top produtos */}
          {d.topProdutos.length > 0 && (
            <div className="card p-0 overflow-hidden">
              <div className="px-4 py-3 border-b border-surface-500">
                <h3 className="text-sm font-semibold text-gray-300">Top Produtos — Receita &amp; Margem</h3>
              </div>
              <table className="w-full text-sm">
                <thead className="bg-surface-800">
                  <tr className="text-left">
                    {['Produto', 'Qtd', 'Receita', 'Custo', 'Margem'].map(h => (
                      <th key={h} className="px-4 py-2 text-xs text-gray-500 uppercase tracking-wider font-semibold">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-surface-500">
                  {d.topProdutos.map(p => (
                    <tr key={p.nome} className="hover:bg-surface-600/20 transition-colors">
                      <td className="px-4 py-2.5 font-medium text-white">{p.nome}</td>
                      <td className="px-4 py-2.5 text-gray-400">{p.qtd}</td>
                      <td className="px-4 py-2.5 font-mono text-accent-gold">{fmt(p.receita)}</td>
                      <td className="px-4 py-2.5 font-mono text-gray-400 text-xs">
                        {p.custo > 0 ? fmt(p.custo) : <span className="text-gray-600">—</span>}
                      </td>
                      <td className="px-4 py-2.5 font-mono text-xs">
                        {p.custo > 0
                          ? <span className={p.margem >= 0 ? 'text-emerald-400' : 'text-red-400'}>
                              {fmt(p.margem)}
                            </span>
                          : <span className="text-gray-600">—</span>
                        }
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Aviso se não tem custo cadastrado */}
          {d.custo === 0 && d.receita > 0 && (
            <div className="flex items-start gap-3 rounded-xl bg-yellow-500/10 border border-yellow-500/20 p-4 text-sm text-yellow-300">
              <AlertCircle className="w-5 h-5 shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold">Custo não cadastrado</p>
                <p className="text-yellow-400/70 mt-0.5">
                  Para ver a margem real, cadastre o <strong>Preço de custo</strong> nos produtos em <a href="/admin/estoque" className="underline">Estoque</a>.
                </p>
              </div>
            </div>
          )}

          {/* Receita zero */}
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
