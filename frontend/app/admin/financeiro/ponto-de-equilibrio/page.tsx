'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  AlertTriangle, BadgeDollarSign, CircleDollarSign, Gauge, Percent,
  RefreshCw, Save, ShieldCheck, TrendingDown,
} from 'lucide-react'
import toast from 'react-hot-toast'
import PageHeader from '@/components/admin/PageHeader'
import { FinanceiroSubnav } from '@/components/admin/financeiro/FinanceiroSubnav'
import { MetricHelp } from '@/components/admin/financeiro/MetricHelp'
import { calculateContribution } from '@/components/admin/financeiro/contribution'
import { fmt, getRange, type Preset } from '@/components/admin/financeiro/financeiro-shared'
import { analyticsApi, financialConfigApi, getErrorMessage, type FinanceiroDto } from '@/lib/api'

export default function PontoEquilibrioPage() {
  const [preset, setPreset] = useState<Preset>('mes')
  const [inicio, setInicio] = useState(getRange('mes').inicio)
  const [fim, setFim] = useState(getRange('mes').fim)
  const [data, setData] = useState<FinanceiroDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [fixedExpenses, setFixedExpenses] = useState(0)
  const [cardFee, setCardFee] = useState(0)
  const [commission, setCommission] = useState(0)
  const [freight, setFreight] = useState(0)
  const [discount, setDiscount] = useState(0)
  const [saving, setSaving] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [response, config] = await Promise.all([
        analyticsApi.financeiro(inicio, fim),
        financialConfigApi.get(),
      ])
      setData(response.data)
      setFixedExpenses(response.data.despesasOperacionais)
      setCardFee(config.data.cardFeePercent)
      setCommission(config.data.commissionPercent)
      setFreight(config.data.freightPercent)
      setDiscount(0)
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível calcular o ponto de equilíbrio'))
    } finally {
      setLoading(false)
    }
  }, [inicio, fim])

  useEffect(() => { load() }, [load])

  async function saveAssumptions() {
    setSaving(true)
    try {
      const current = await financialConfigApi.get()
      await financialConfigApi.save({
        cardFeePercent: cardFee,
        commissionPercent: commission,
        freightPercent: freight,
        expectedDailyNetCash: current.data.expectedDailyNetCash,
        minimumCashReserve: current.data.minimumCashReserve,
      })
      toast.success('Premissas financeiras salvas')
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível salvar as premissas'))
    } finally {
      setSaving(false)
    }
  }

  function applyPreset(next: Preset) {
    setPreset(next)
    if (next !== 'custom') {
      const range = getRange(next)
      setInicio(range.inicio)
      setFim(range.fim)
    }
  }

  const result = useMemo(() => data ? calculateContribution({
    revenue: data.receita,
    cmv: data.custo,
    salesTax: data.impostosSobreVendas,
    fixedExpenses,
    cardFeePercent: cardFee,
    commissionPercent: commission,
    freightPercent: freight,
    discountPercent: discount,
  }) : null, [cardFee, commission, data, discount, fixedExpenses, freight])

  const hasRevenue = Boolean(data && data.receita > 0)

  return <div className="space-y-5 p-4 sm:p-6">
    <PageHeader icon={Gauge} title="Margem e Ponto de Equilíbrio" description="Quanto cada venda ajuda a pagar a estrutura e até onde o desconto continua sustentável" backHref="/admin/financeiro" actions={<button type="button" onClick={load} disabled={loading} className="btn-secondary" title="Atualizar dados"><RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} /></button>} />
    <FinanceiroSubnav />

    <section className="card space-y-3" aria-label="Período do cálculo">
      <div className="chip-row w-full">{(['hoje', '7d', 'mes', 'custom'] as Preset[]).map(item => <button type="button" key={item} onClick={() => applyPreset(item)} className={`min-h-10 rounded-md px-3 text-sm font-medium ${preset === item ? 'bg-brand-600 text-white' : 'bg-surface-700 text-gray-400 hover:text-white'}`}>{{ hoje: 'Hoje', '7d': '7 dias', mes: 'Este mês', custom: 'Personalizado' }[item]}</button>)}</div>
      <div className="grid grid-cols-1 gap-3 xs:grid-cols-2 sm:max-w-md">
        <label className="text-xs font-semibold text-gray-400">Início<input type="date" value={inicio} onChange={event => { setPreset('custom'); setInicio(event.target.value) }} className="input mt-1 w-full" /></label>
        <label className="text-xs font-semibold text-gray-400">Fim<input type="date" value={fim} onChange={event => { setPreset('custom'); setFim(event.target.value) }} className="input mt-1 w-full" /></label>
      </div>
    </section>

    {loading ? <div className="flex justify-center py-20"><RefreshCw className="h-7 w-7 animate-spin text-brand-400" /></div> : data && result && <>
      {!hasRevenue && <div className="flex items-start gap-2 rounded-md border border-amber-500/40 bg-amber-500/10 p-4 text-sm text-amber-800 dark:text-amber-200"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />Não há receita no período selecionado. As premissas podem ser preenchidas, mas margem e ponto de equilíbrio precisam de vendas para formar uma base.</div>}

      <section className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <MetricCard title="Margem de contribuição" value={percent(result.contributionMarginPercent)} tone={result.contributionMargin >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-600 dark:text-red-300'} icon={Percent}><p>Percentual da receita que sobra depois de CMV, impostos e demais custos variáveis.</p><p className="font-mono text-xs text-brand-300">contribuição / receita após desconto</p></MetricCard>
        <MetricCard title="Contribuição em R$" value={fmt(result.contributionMargin)} tone={result.contributionMargin >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-600 dark:text-red-300'} icon={BadgeDollarSign}><p>Valor disponível para pagar despesas fixas e, depois delas, formar lucro.</p></MetricCard>
        <MetricCard title="Ponto de equilíbrio" value={result.breakEvenRevenue === null ? 'Sem base' : fmt(result.breakEvenRevenue)} tone="text-sky-700 dark:text-brand-300" icon={Gauge}><p>Receita necessária para a contribuição cobrir as despesas fixas informadas.</p><p className="font-mono text-xs text-brand-300">fixas / margem de contribuição %</p></MetricCard>
        <MetricCard title="Margem de segurança" value={result.safetyMargin === null ? 'Sem base' : fmt(result.safetyMargin)} tone={result.safetyMargin !== null && result.safetyMargin >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-600 dark:text-red-300'} icon={ShieldCheck}><p>Quanto a receita simulada está acima ou abaixo do ponto de equilíbrio.</p></MetricCard>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[minmax(0,1.25fr)_minmax(320px,0.75fr)]">
        <div className="card space-y-5">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><div className="flex items-center gap-1"><h2 className="text-base font-bold text-white">Premissas do cálculo</h2><MetricHelp title="O que é conhecido e o que é estimado"><p>Receita, CMV e impostos sobre vendas vêm da DRE do período.</p><p>Como o cadastro atual não distingue despesas fixas de variáveis, as despesas operacionais são carregadas inicialmente como base fixa e podem ser ajustadas.</p><p>Taxas de cartão, comissões e frete podem ser salvas como padrão da loja.</p></MetricHelp></div><p className="mt-1 text-sm text-gray-400">Salvar grava apenas os percentuais padrão; não modifica lançamentos nem a despesa fixa desta simulação.</p></div><button type="button" onClick={saveAssumptions} disabled={saving} className="btn-secondary shrink-0"><Save className={`h-4 w-4 ${saving ? 'animate-pulse' : ''}`} />Salvar padrões</button></div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <KnownValue label="Receita do período" value={data.receita} />
            <KnownValue label="CMV" value={data.custo} />
            <KnownValue label="Impostos sobre vendas" value={data.impostosSobreVendas} detail={`${result.knownTaxPercent.toFixed(2)}% da receita`} />
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <MoneyInput label="Despesas fixas estimadas" value={fixedExpenses} onChange={setFixedExpenses} />
            <PercentInput label="Taxas de cartão" value={cardFee} onChange={setCardFee} />
            <PercentInput label="Comissões" value={commission} onChange={setCommission} />
            <PercentInput label="Frete e outros variáveis" value={freight} onChange={setFreight} />
          </div>

          {data.lancamentosNaoClassificados > 0 && <div className="flex items-start gap-2 rounded-md border border-amber-500/40 bg-amber-500/10 p-3 text-sm text-amber-800 dark:text-amber-200"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />Existem {fmt(data.lancamentosNaoClassificados)} em lançamentos sem classificação. Eles não entram automaticamente como fixos nem variáveis.</div>}
        </div>

        <div className="card space-y-4">
          <div className="flex items-center gap-1"><h2 className="text-base font-bold text-white">Estrutura da contribuição</h2><MetricHelp title="Leitura da contribuição"><p>O CMV permanece em reais porque a simulação mantém a mesma quantidade vendida.</p><p>Impostos, cartão, comissão e frete variam junto com a receita após desconto.</p></MetricHelp></div>
          <dl className="divide-y divide-surface-500 text-sm">
            <ValueRow label="Receita após desconto" value={result.discountedRevenue} />
            <ValueRow label="CMV" value={-data.custo} negative />
            <ValueRow label={`Variáveis (${result.totalVariablePercent.toFixed(2)}%)`} value={-result.variableExpenses} negative />
            <ValueRow label="Margem de contribuição" value={result.contributionMargin} strong />
            <ValueRow label="Despesas fixas estimadas" value={-fixedExpenses} negative />
            <ValueRow label="Resultado operacional simulado" value={result.projectedOperatingResult} strong />
          </dl>
        </div>
      </section>

      <section className="card space-y-5">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div><div className="flex items-center gap-1"><TrendingDown className="h-5 w-5 text-brand-400" /><h2 className="text-base font-bold text-white">Simulador de desconto</h2><MetricHelp title="Limites do desconto"><p>O desconto é aplicado sobre toda a receita mantendo a mesma quantidade e o mesmo CMV.</p><p>O limite de contribuição mostra quando a venda deixa de pagar até os custos variáveis. O limite de equilíbrio também exige cobertura das despesas fixas informadas.</p><p>Esta é uma simulação agregada; produtos possuem custos e margens diferentes.</p></MetricHelp></div><p className="mt-1 text-sm text-gray-400">Simula o impacto no período sem alterar preços ou vendas.</p></div>
          <label className="w-full max-w-md text-sm font-semibold text-gray-300">Desconto simulado: <span className="font-mono text-brand-300">{discount.toFixed(1)}%</span><input aria-label="Desconto simulado" type="range" min="0" max="50" step="0.5" value={discount} onChange={event => setDiscount(Number(event.target.value))} className="mt-2 w-full accent-[rgb(var(--brand-500))]" /></label>
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <SimulationValue label="Receita simulada" value={fmt(result.discountedRevenue)} />
          <SimulationValue label="Resultado operacional" value={fmt(result.projectedOperatingResult)} tone={result.projectedOperatingResult >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-600 dark:text-red-300'} />
          <SimulationValue label="Limite para contribuir" value={percent(result.maxDiscountContributionPercent)} />
          <SimulationValue label="Limite para equilibrar" value={percent(result.maxDiscountBreakEvenPercent)} tone={result.maxDiscountBreakEvenPercent !== null && discount <= result.maxDiscountBreakEvenPercent ? 'text-emerald-700 dark:text-emerald-400' : 'text-amber-700 dark:text-amber-300'} />
        </div>
        {result.maxDiscountBreakEvenPercent !== null && discount > result.maxDiscountBreakEvenPercent && <div className="flex items-start gap-2 rounded-md border border-red-500/40 bg-red-500/10 p-3 text-sm font-medium text-red-700 dark:text-red-200"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />Com as premissas atuais, este desconto leva o período abaixo do ponto de equilíbrio.</div>}
      </section>
      <p className="text-xs text-gray-500">A análise usa a composição agregada do período. Para decisões por item, confirme também o custo e a margem individual em Preço e Rentabilidade.</p>
    </>}
  </div>
}

function percent(value: number | null) { return value === null || !Number.isFinite(value) ? 'Sem base' : `${value.toFixed(1)}%` }

function MetricCard({ title, value, tone, icon: Icon, children }: { title: string; value: string; tone: string; icon: typeof Gauge; children: React.ReactNode }) {
  return <article className="card min-w-0"><div className="flex min-h-8 items-start justify-between gap-1"><div className="flex min-w-0 items-start gap-2"><Icon className="mt-0.5 h-4 w-4 shrink-0 text-gray-500" /><p className="text-[11px] font-semibold uppercase leading-4 text-gray-500 sm:text-xs">{title}</p></div><MetricHelp title={title}>{children}</MetricHelp></div><p className={`mt-2 break-words font-mono text-xl font-bold sm:text-2xl ${tone}`}>{value}</p></article>
}

function KnownValue({ label, value, detail }: { label: string; value: number; detail?: string }) {
  return <div className="border-l-2 border-surface-500 pl-3"><p className="text-xs font-semibold uppercase text-gray-500">{label}</p><p className="mt-1 font-mono font-bold text-white">{fmt(value)}</p>{detail && <p className="text-xs text-gray-600">{detail}</p>}</div>
}

function MoneyInput({ label, value, onChange }: { label: string; value: number; onChange: (value: number) => void }) {
  return <label className="text-xs font-semibold text-gray-400">{label}<span className="relative mt-1 block"><span className="pointer-events-none absolute left-3 top-2.5 text-sm text-gray-500">R$</span><input aria-label={label} type="number" min="0" step="0.01" value={value} onChange={event => onChange(Math.max(0, Number(event.target.value) || 0))} className="input w-full pl-9" /></span></label>
}

function PercentInput({ label, value, onChange }: { label: string; value: number; onChange: (value: number) => void }) {
  return <label className="text-xs font-semibold text-gray-400">{label}<span className="relative mt-1 block"><input aria-label={label} type="number" min="0" max="100" step="0.1" value={value} onChange={event => onChange(Math.min(100, Math.max(0, Number(event.target.value) || 0)))} className="input w-full pr-8" /><span className="pointer-events-none absolute right-3 top-2.5 text-sm text-gray-500">%</span></span></label>
}

function ValueRow({ label, value, negative = false, strong = false }: { label: string; value: number; negative?: boolean; strong?: boolean }) {
  return <div className="flex items-center justify-between gap-3 py-3 first:pt-0 last:pb-0"><dt className={strong ? 'font-semibold text-white' : 'text-gray-400'}>{label}</dt><dd className={`shrink-0 font-mono ${strong ? 'font-bold text-white' : negative ? 'text-red-600 dark:text-red-300' : 'text-gray-200'}`}>{value > 0 && !negative ? '+' : ''}{fmt(value)}</dd></div>
}

function SimulationValue({ label, value, tone = 'text-white' }: { label: string; value: string; tone?: string }) {
  return <div className="rounded-md border border-surface-500 bg-surface-700/50 p-4"><p className="text-xs font-semibold uppercase text-gray-500">{label}</p><p className={`mt-2 break-words font-mono text-lg font-bold ${tone}`}>{value}</p></div>
}
