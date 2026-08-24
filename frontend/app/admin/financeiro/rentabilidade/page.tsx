'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import { AlertTriangle, Calculator, RefreshCw, Search, Tags, TrendingUp } from 'lucide-react'
import PageHeader from '@/components/admin/PageHeader'
import { FinanceiroSubnav } from '@/components/admin/financeiro/FinanceiroSubnav'
import { MetricHelp } from '@/components/admin/financeiro/MetricHelp'
import { calculateProfitability } from '@/components/admin/financeiro/profitability'
import { fmt, getRange, type Preset } from '@/components/admin/financeiro/financeiro-shared'
import { analyticsApi, getErrorMessage, type FinanceiroDto } from '@/lib/api'
import toast from 'react-hot-toast'

type SortKey = 'receita' | 'margem' | 'markup' | 'gap'

export default function RentabilidadePage() {
  const [preset, setPreset] = useState<Preset>('mes')
  const [inicio, setInicio] = useState(getRange('mes').inicio)
  const [fim, setFim] = useState(getRange('mes').fim)
  const [targetMargin, setTargetMargin] = useState(40)
  const [query, setQuery] = useState('')
  const [sort, setSort] = useState<SortKey>('receita')
  const [data, setData] = useState<FinanceiroDto | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const response = await analyticsApi.financeiro(inicio, fim)
      setData(response.data)
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível carregar a rentabilidade'))
    } finally {
      setLoading(false)
    }
  }, [inicio, fim])

  useEffect(() => { load() }, [load])

  function applyPreset(next: Preset) {
    setPreset(next)
    if (next !== 'custom') {
      const range = getRange(next)
      setInicio(range.inicio)
      setFim(range.fim)
    }
  }

  const rows = useMemo(() => {
    const normalized = query.trim().toLocaleLowerCase('pt-BR')
    return (data?.topProdutos ?? [])
      .map(product => calculateProfitability(product, targetMargin))
      .filter(product => !normalized || `${product.nome} ${product.categoria}`.toLocaleLowerCase('pt-BR').includes(normalized))
      .sort((a, b) => {
        if (sort === 'margem') return (b.grossMarginPercent ?? -Infinity) - (a.grossMarginPercent ?? -Infinity)
        if (sort === 'markup') return (b.markupPercent ?? -Infinity) - (a.markupPercent ?? -Infinity)
        if (sort === 'gap') return (b.priceGap ?? -Infinity) - (a.priceGap ?? -Infinity)
        return b.receita - a.receita
      })
  }, [data, query, sort, targetMargin])

  const productsWithoutCost = rows.filter(row => !row.hasCost).length
  const belowTarget = rows.filter(row => row.grossMarginPercent !== null && row.grossMarginPercent < targetMargin).length
  const weightedMarkup = data && data.custo > 0 ? (data.margem / data.custo) * 100 : null

  return (
    <div className="space-y-5 p-4 sm:p-6">
      <PageHeader
        icon={Tags}
        title="Preço e Rentabilidade"
        description="Margem, markup e simulação de preço por produto"
        backHref="/admin/financeiro"
        actions={
          <button type="button" onClick={load} disabled={loading} className="btn-secondary" title="Atualizar dados">
            <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
          </button>
        }
      />
      <FinanceiroSubnav />

      <section className="card space-y-4" aria-label="Filtros da analise">
        <div className="space-y-3">
          <div className="chip-row w-full">
            {(['hoje', '7d', 'mes', 'custom'] as Preset[]).map(item => (
              <button
                type="button"
                key={item}
                onClick={() => applyPreset(item)}
                className={`min-h-10 rounded-md px-3 text-sm font-medium ${preset === item ? 'bg-brand-600 text-white' : 'bg-surface-700 text-gray-400 hover:text-white'}`}
              >
                {{ hoje: 'Hoje', '7d': '7 dias', mes: 'Este mês', custom: 'Personalizado' }[item]}
              </button>
            ))}
          </div>
          <div className="grid grid-cols-1 gap-3 xs:grid-cols-2 sm:max-w-md">
            <label className="min-w-0 text-xs font-semibold text-gray-400">
              Início
              <input type="date" value={inicio} onChange={event => { setPreset('custom'); setInicio(event.target.value) }} className="input mt-1 w-full" />
            </label>
            <label className="min-w-0 text-xs font-semibold text-gray-400">
              Fim
              <input type="date" value={fim} onChange={event => { setPreset('custom'); setFim(event.target.value) }} className="input mt-1 w-full" />
            </label>
          </div>
        </div>
      </section>

      <section className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <MetricCard title="Margem bruta" value={data ? `${data.margemPercent.toFixed(1)}%` : '--'} tone="text-emerald-400">
          <p>Percentual da receita que sobra depois do custo dos produtos vendidos.</p>
          <p className="font-mono text-xs text-brand-300">(receita - CMV) / receita x 100</p>
        </MetricCard>
        <MetricCard title="Markup medio" value={weightedMarkup === null ? '--' : `${weightedMarkup.toFixed(1)}%`} tone="text-brand-300">
          <p>Quanto foi acrescentado sobre o custo. Markup e margem usam bases diferentes e não devem ser confundidos.</p>
          <p className="font-mono text-xs text-brand-300">(receita - CMV) / CMV x 100</p>
        </MetricCard>
        <MetricCard title="Margem bruta em R$" value={data ? fmt(data.margem) : '--'} tone="text-white">
          <p>Receita do período menos o custo congelado dos itens vendidos. Ainda não desconta despesas operacionais e financeiras.</p>
        </MetricCard>
        <MetricCard title="Abaixo da meta" value={loading ? '--' : String(belowTarget)} tone={belowTarget > 0 ? 'text-amber-300' : 'text-emerald-400'}>
          <p>Produtos vendidos cuja margem bruta ficou abaixo da meta selecionada nesta tela.</p>
        </MetricCard>
      </section>

      <section className="card space-y-4">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <div className="flex items-center gap-1">
              <Calculator className="h-5 w-5 text-brand-400" />
              <h2 className="text-base font-bold text-white">Simulador de margem bruta</h2>
              <MetricHelp title="Como funciona o preço sugerido">
                <p>O preço sugerido usa o custo médio observado nas vendas do período e a margem bruta desejada.</p>
                <p className="font-mono text-xs text-brand-300">preço = custo / (1 - margem desejada)</p>
                <p>Ele não inclui automaticamente impostos, taxas de cartão, comissões ou frete. Esses custos entrarão na etapa de margem de contribuição.</p>
              </MetricHelp>
            </div>
            <p className="mt-1 text-sm text-gray-400">A meta altera apenas a simulação, sem mudar o cadastro dos produtos.</p>
          </div>
          <label className="w-full max-w-sm text-sm font-semibold text-gray-300">
            Meta de margem: <span className="font-mono text-brand-300">{targetMargin}%</span>
            <input
              aria-label="Meta de margem bruta"
              type="range"
              min="5"
              max="80"
              step="1"
              value={targetMargin}
              onChange={event => setTargetMargin(Number(event.target.value))}
              className="mt-2 w-full accent-[rgb(var(--brand-500))]"
            />
          </label>
        </div>

        {productsWithoutCost > 0 && (
          <div className="flex items-start gap-2 rounded-md border border-amber-500/30 bg-amber-500/10 p-3 text-sm text-amber-200">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            {productsWithoutCost} produto{productsWithoutCost === 1 ? '' : 's'} sem custo no histórico. Cadastre ou corrija o custo para liberar margem, markup e sugestão.
          </div>
        )}

        <div className="flex flex-col gap-3 sm:flex-row">
          <label className="relative flex-1">
            <span className="sr-only">Buscar produto</span>
            <Search className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-gray-500" />
            <input value={query} onChange={event => setQuery(event.target.value)} placeholder="Buscar produto ou categoria" className="input w-full pl-9" />
          </label>
          <select value={sort} onChange={event => setSort(event.target.value as SortKey)} className="input sm:w-52" aria-label="Ordenar produtos">
            <option value="receita">Maior receita</option>
            <option value="margem">Maior margem</option>
            <option value="markup">Maior markup</option>
            <option value="gap">Maior reajuste sugerido</option>
          </select>
        </div>

        {loading ? (
          <div className="flex justify-center py-16"><RefreshCw className="h-6 w-6 animate-spin text-brand-400" /></div>
        ) : rows.length === 0 ? (
          <div className="py-14 text-center text-sm text-gray-500">Nenhum produto vendido neste periodo.</div>
        ) : (
          <div className="table-scroll">
            <table className="w-full min-w-[940px] text-sm">
              <thead className="bg-surface-800 text-left text-xs uppercase text-gray-500">
                <tr>
                  <th className="px-3 py-3">Produto</th><th className="px-3 py-3">Vendidos</th><th className="px-3 py-3">Receita</th>
                  <th className="px-3 py-3">Preço médio</th><th className="px-3 py-3">Custo médio</th><th className="px-3 py-3">Margem</th>
                  <th className="px-3 py-3">Markup</th><th className="px-3 py-3">Preço sugerido</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-surface-500">
                {rows.map(row => (
                  <tr key={row.nome} className="hover:bg-surface-700/60">
                    <td className="px-3 py-3"><p className="max-w-[220px] truncate font-semibold text-white">{row.nome}</p><p className="text-xs text-gray-500">{row.categoria || 'Sem categoria'}</p></td>
                    <td className="px-3 py-3 font-mono text-gray-300">{row.qtd}</td>
                    <td className="px-3 py-3 font-mono font-semibold text-emerald-400">{fmt(row.receita)}</td>
                    <td className="px-3 py-3 font-mono text-gray-300">{fmt(row.averagePrice)}</td>
                    <td className="px-3 py-3 font-mono text-gray-300">{row.hasCost ? fmt(row.averageCost) : <span className="text-amber-300">Não informado</span>}</td>
                    <td className={`px-3 py-3 font-mono font-bold ${row.grossMarginPercent !== null && row.grossMarginPercent >= targetMargin ? 'text-emerald-400' : 'text-amber-300'}`}>{row.grossMarginPercent === null ? '--' : `${row.grossMarginPercent.toFixed(1)}%`}</td>
                    <td className="px-3 py-3 font-mono text-brand-300">{row.markupPercent === null ? '--' : `${row.markupPercent.toFixed(1)}%`}</td>
                    <td className="px-3 py-3"><p className="font-mono font-bold text-white">{row.suggestedPrice === null ? '--' : fmt(row.suggestedPrice)}</p>{row.priceGap !== null && <p className={`text-xs ${row.priceGap > 0.01 ? 'text-amber-300' : 'text-emerald-400'}`}>{row.priceGap > 0.01 ? `+ ${fmt(row.priceGap)}` : 'Meta atendida'}</p>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <p className="text-xs text-gray-500">Exibindo os produtos de maior receita retornados pelo Financeiro no período selecionado.</p>
      </section>
    </div>
  )
}

function MetricCard({ title, value, tone, children }: { title: string; value: string; tone: string; children: React.ReactNode }) {
  return (
    <article className="card min-w-0">
      <div className="flex items-center justify-between gap-1">
        <p className="text-[11px] font-semibold uppercase leading-4 text-gray-500 sm:text-xs">{title}</p>
        <MetricHelp title={title}>{children}</MetricHelp>
      </div>
      <p className={`mt-2 break-words font-mono text-xl font-bold sm:text-2xl ${tone}`}>{value}</p>
    </article>
  )
}
