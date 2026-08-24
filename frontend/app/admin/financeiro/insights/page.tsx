'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import {
  AlertTriangle, ArrowRight, BadgeCheck, CircleAlert, CircleDollarSign,
  Lightbulb, RefreshCw, Sparkles, TrendingDown, TrendingUp,
  type LucideIcon,
} from 'lucide-react'
import toast from 'react-hot-toast'
import PageHeader from '@/components/admin/PageHeader'
import { FinanceiroSubnav } from '@/components/admin/financeiro/FinanceiroSubnav'
import { MetricHelp } from '@/components/admin/financeiro/MetricHelp'
import {
  buildFinancialInsights, previousPeriod,
  type FinancialInsight, type InsightSeverity,
} from '@/components/admin/financeiro/financial-insights'
import { fmt, getRange, type Preset } from '@/components/admin/financeiro/financeiro-shared'
import {
  analyticsApi, getErrorMessage,
  type CapitalGiroDto, type EstoqueInteligenteDto, type FinanceiroDto,
} from '@/lib/api'

type Filter = 'all' | InsightSeverity

const SEVERITY: Record<InsightSeverity, { label: string; icon: LucideIcon; className: string; border: string }> = {
  urgent: { label: 'Urgente', icon: CircleAlert, className: 'bg-red-500/15 text-red-300', border: 'border-red-500/35' },
  attention: { label: 'Atenção', icon: AlertTriangle, className: 'bg-amber-500/15 text-amber-300', border: 'border-amber-500/30' },
  opportunity: { label: 'Oportunidade', icon: Lightbulb, className: 'bg-blue-500/15 text-blue-300', border: 'border-blue-500/30' },
  positive: { label: 'Positivo', icon: BadgeCheck, className: 'bg-emerald-500/15 text-emerald-300', border: 'border-emerald-500/30' },
}

interface LoadedData {
  current: FinanceiroDto
  previous: FinanceiroDto
  capital: CapitalGiroDto
  stock: EstoqueInteligenteDto
}

export default function FinanceiroInsightsPage() {
  const [preset, setPreset] = useState<Preset>('mes')
  const [inicio, setInicio] = useState(getRange('mes').inicio)
  const [fim, setFim] = useState(getRange('mes').fim)
  const [filter, setFilter] = useState<Filter>('all')
  const [data, setData] = useState<LoadedData | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    const previous = previousPeriod(inicio, fim)
    try {
      const [currentResponse, previousResponse, capitalResponse, stockResponse] = await Promise.all([
        analyticsApi.financeiro(inicio, fim),
        analyticsApi.financeiro(previous.inicio, previous.fim),
        analyticsApi.capitalGiro(inicio, fim),
        analyticsApi.estoqueInteligente(inicio, fim),
      ])
      setData({
        current: currentResponse.data,
        previous: previousResponse.data,
        capital: capitalResponse.data,
        stock: stockResponse.data,
      })
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível montar os insights financeiros'))
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

  const summary = useMemo(() => data ? buildFinancialInsights(data) : null, [data])
  const visibleInsights = useMemo(() => (summary?.insights ?? []).filter(item => filter === 'all' || item.severity === filter), [filter, summary])

  return <div className="space-y-5 p-4 sm:p-6">
    <PageHeader icon={Sparkles} title="Insights Financeiros" description="Prioridades explicadas e próximas ações para proteger caixa, margem e estoque" backHref="/admin/financeiro" actions={<button type="button" onClick={load} disabled={loading} className="btn-secondary" title="Atualizar dados"><RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} /></button>} />
    <FinanceiroSubnav />

    <section className="card space-y-3" aria-label="Período dos insights">
      <div className="chip-row w-full">{(['hoje', '7d', 'mes', 'custom'] as Preset[]).map(item => <button type="button" key={item} onClick={() => applyPreset(item)} className={`min-h-10 rounded-md px-3 text-sm font-medium ${preset === item ? 'bg-brand-600 text-white' : 'bg-surface-700 text-gray-400 hover:text-white'}`}>{{ hoje: 'Hoje', '7d': '7 dias', mes: 'Este mês', custom: 'Personalizado' }[item]}</button>)}</div>
      <div className="grid grid-cols-1 gap-3 xs:grid-cols-2 sm:max-w-md">
        <label className="text-xs font-semibold text-gray-400">Início<input type="date" value={inicio} onChange={event => { setPreset('custom'); setInicio(event.target.value) }} className="input mt-1 w-full" /></label>
        <label className="text-xs font-semibold text-gray-400">Fim<input type="date" value={fim} onChange={event => { setPreset('custom'); setFim(event.target.value) }} className="input mt-1 w-full" /></label>
      </div>
    </section>

    {loading ? <div className="flex justify-center py-20"><RefreshCw className="h-7 w-7 animate-spin text-brand-400" /></div> : summary && data && <>
      <section className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <SummaryCard title="Ação imediata" value={String(summary.urgentCount)} tone={summary.urgentCount > 0 ? 'text-red-300' : 'text-emerald-400'} icon={CircleAlert} help="Quantidade de sinais urgentes: resultado negativo, títulos vencidos ou risco de ruptura." />
        <SummaryCard title="Pontos de atenção" value={String(summary.attentionCount)} tone={summary.attentionCount > 0 ? 'text-amber-300' : 'text-emerald-400'} icon={AlertTriangle} help="Pendências que podem distorcer a análise ou pressionar o caixa, mas não são tratadas como vencidas." />
        <SummaryCard title="Resultado líquido" value={fmt(data.current.resultadoLiquido)} tone={data.current.resultadoLiquido >= 0 ? 'text-emerald-400' : 'text-red-300'} icon={CircleDollarSign} help="Resultado do período depois de CMV, despesas operacionais, resultado financeiro e impostos sobre lucro." />
        <SummaryCard title="Capital com baixa saída" value={fmt(summary.lowMovementCapital)} tone={summary.lowMovementCapital > 0 ? 'text-blue-300' : 'text-emerald-400'} icon={Lightbulb} help="Custo atual de produtos sem movimento no período ou classificados com excesso de cobertura." />
      </section>

      <section className="grid grid-cols-1 gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(260px,1fr)]">
        <div className="min-w-0 space-y-4">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div><h2 className="text-base font-bold text-white">Fila de decisões</h2><p className="mt-1 text-sm text-gray-400">Ordenada primeiro pela urgência e depois pelo valor conhecido.</p></div>
            <div className="chip-row" aria-label="Filtrar insights">{(['all', 'urgent', 'attention', 'opportunity', 'positive'] as Filter[]).map(item => <button type="button" key={item} onClick={() => setFilter(item)} className={`min-h-9 rounded-md px-3 text-xs font-semibold ${filter === item ? 'bg-brand-600 text-white' : 'bg-surface-700 text-gray-400 hover:text-white'}`}>{item === 'all' ? 'Todos' : SEVERITY[item].label}</button>)}</div>
          </div>

          {visibleInsights.length === 0 ? <div className="border-y border-surface-500 py-14 text-center text-sm text-gray-500">Nenhum insight encontrado neste filtro.</div> : <div className="grid grid-cols-1 gap-3 xl:grid-cols-2">{visibleInsights.map(insight => <InsightCard key={insight.id} insight={insight} />)}</div>}
        </div>

        <aside className="space-y-4">
          <section className="card">
            <div className="flex items-center gap-1"><h2 className="text-base font-bold text-white">Comparação equivalente</h2><MetricHelp title="Comparação do período"><p>O sistema compara o intervalo selecionado com os dias imediatamente anteriores, usando exatamente a mesma duração.</p><p>Quando o período anterior é zero, a variação percentual não é exibida para evitar uma comparação infinita.</p></MetricHelp></div>
            <dl className="mt-4 divide-y divide-surface-500">
              <ComparisonRow label="Receita" current={data.current.receita} previous={data.previous.receita} variation={summary.revenueVariation} />
              <ComparisonRow label="Resultado líquido" current={data.current.resultadoLiquido} previous={data.previous.resultadoLiquido} variation={summary.resultVariation} />
              <ComparisonRow label="Margem bruta" current={data.current.margemPercent} previous={data.previous.margemPercent} variation={percentagePointVariation(data.current.margemPercent, data.previous.margemPercent)} percentagePoints />
            </dl>
          </section>

          <section className="card">
            <div className="flex items-center gap-1"><h2 className="text-base font-bold text-white">Qualidade da leitura</h2><MetricHelp title="Limites dos insights"><p>Os insights usam somente registros existentes; eles não substituem conciliação bancária nem previsão contábil.</p><p>Estoque usa o saldo atual. Sem histórico diário, cobertura, ciclo e GMROI continuam identificados como estimativas.</p></MetricHelp></div>
            <ul className="mt-4 space-y-3 text-sm text-gray-400">
              <QualityItem ok={data.current.lancamentosNaoClassificados === 0} text={data.current.lancamentosNaoClassificados === 0 ? 'Lançamentos do período classificados' : `${fmt(data.current.lancamentosNaoClassificados)} sem classificação na DRE`} />
              <QualityItem ok={data.stock.produtosSemCusto === 0} text={data.stock.produtosSemCusto === 0 ? 'Produtos em estoque com custo informado' : `${data.stock.produtosSemCusto} produto(s) com custo ausente`} />
              <QualityItem ok={data.capital.comprasEstoquePeriodo > 0} text={data.capital.comprasEstoquePeriodo > 0 ? 'Compras disponíveis para estimar prazo de pagamento' : 'Sem compras no período para estimar pagamento'} />
            </ul>
          </section>
        </aside>
      </section>
      <p className="text-xs text-gray-500">Recomendações geradas por regras financeiras visíveis e dados do sistema. Nenhuma ação altera preços, estoque, contas ou cobranças automaticamente.</p>
    </>}
  </div>
}

function InsightCard({ insight }: { insight: FinancialInsight }) {
  const config = SEVERITY[insight.severity]
  const Icon = config.icon
  return <article className={`card flex min-w-0 flex-col border ${config.border}`}>
    <div className="flex items-start justify-between gap-3"><span className={`inline-flex items-center gap-1.5 rounded px-2 py-1 text-xs font-semibold ${config.className}`}><Icon className="h-3.5 w-3.5" />{config.label}</span><span className="text-xs font-semibold uppercase text-gray-600">{insight.category}</span></div>
    <h3 className="mt-3 text-sm font-bold text-white">{insight.title}</h3>
    <p className="mt-1 flex-1 text-sm leading-6 text-gray-400">{insight.detail}</p>
    {insight.impactValue !== undefined && <p className="mt-3 font-mono text-lg font-bold text-white">{fmt(insight.impactValue)}</p>}
    <div className="mt-4 flex items-center justify-between gap-3 border-t border-surface-500 pt-3"><MetricHelp title={`Por que apareceu: ${insight.title}`}><p>{insight.rationale}</p></MetricHelp><Link href={insight.href} className="btn-secondary min-h-9 text-xs">{insight.actionLabel}<ArrowRight className="h-4 w-4" /></Link></div>
  </article>
}

function SummaryCard({ title, value, tone, icon: Icon, help }: { title: string; value: string; tone: string; icon: LucideIcon; help: string }) {
  return <article className="card min-w-0"><div className="flex min-h-8 items-start justify-between gap-1"><div className="flex min-w-0 items-start gap-2"><Icon className="mt-0.5 h-4 w-4 shrink-0 text-gray-500" /><p className="text-[11px] font-semibold uppercase leading-4 text-gray-500 sm:text-xs">{title}</p></div><MetricHelp title={title}><p>{help}</p></MetricHelp></div><p className={`mt-2 break-words font-mono text-xl font-bold sm:text-2xl ${tone}`}>{value}</p></article>
}

function ComparisonRow({ label, current, previous, variation, percentagePoints = false }: { label: string; current: number; previous: number; variation: number | null; percentagePoints?: boolean }) {
  const positive = variation !== null && variation >= 0
  const Icon = positive ? TrendingUp : TrendingDown
  return <div className="py-3 first:pt-0 last:pb-0"><dt className="text-xs font-semibold uppercase text-gray-500">{label}</dt><dd className="mt-1 flex items-end justify-between gap-3"><span><strong className="block font-mono text-sm text-white">{percentagePoints ? `${current.toFixed(1)}%` : fmt(current)}</strong><span className="text-xs text-gray-600">antes: {percentagePoints ? `${previous.toFixed(1)}%` : fmt(previous)}</span></span>{variation === null ? <span className="text-xs text-gray-500">Sem base</span> : <span className={`inline-flex items-center gap-1 font-mono text-xs font-semibold ${positive ? 'text-emerald-400' : 'text-red-300'}`}><Icon className="h-3.5 w-3.5" />{positive ? '+' : ''}{variation.toFixed(1)}{percentagePoints ? ' p.p.' : '%'}</span>}</dd></div>
}

function QualityItem({ ok, text }: { ok: boolean; text: string }) {
  const Icon = ok ? BadgeCheck : AlertTriangle
  return <li className="flex items-start gap-2"><Icon className={`mt-0.5 h-4 w-4 shrink-0 ${ok ? 'text-emerald-400' : 'text-amber-300'}`} /><span>{text}</span></li>
}

function percentagePointVariation(current: number, previous: number) {
  return current - previous
}
