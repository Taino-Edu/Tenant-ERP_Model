'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  AlertTriangle, CalendarRange, CircleDollarSign, RefreshCw,
  Save, ShieldAlert, TrendingDown, WalletCards,
} from 'lucide-react'
import toast from 'react-hot-toast'
import PageHeader from '@/components/admin/PageHeader'
import { FinanceiroSubnav } from '@/components/admin/financeiro/FinanceiroSubnav'
import { MetricHelp } from '@/components/admin/financeiro/MetricHelp'
import { calculateCashProjection, type CashProjectionDay } from '@/components/admin/financeiro/cash-projection'
import { fmt } from '@/components/admin/financeiro/financeiro-shared'
import { analyticsApi, financialConfigApi, getErrorMessage, type AgendaCaixaDto } from '@/lib/api'

const HORIZONS = [7, 15, 30, 60] as const

export default function ProjecaoCaixaPage() {
  const [horizon, setHorizon] = useState<number>(30)
  const [initialBalance, setInitialBalance] = useState(0)
  const [dailyGeneration, setDailyGeneration] = useState(0)
  const [minimumReserve, setMinimumReserve] = useState(0)
  const [data, setData] = useState<AgendaCaixaDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const response = await analyticsApi.agendaCaixa(horizon)
      setData(response.data)
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível projetar o caixa'))
    } finally {
      setLoading(false)
    }
  }, [horizon])

  useEffect(() => { load() }, [load])
  useEffect(() => {
    financialConfigApi.get().then(response => {
      setDailyGeneration(response.data.expectedDailyNetCash)
      setMinimumReserve(response.data.minimumCashReserve)
    }).catch(error => toast.error(getErrorMessage(error, 'Não foi possível carregar as premissas financeiras')))
  }, [])

  async function saveAssumptions() {
    setSaving(true)
    try {
      const current = await financialConfigApi.get()
      await financialConfigApi.save({
        cardFeePercent: current.data.cardFeePercent,
        commissionPercent: current.data.commissionPercent,
        freightPercent: current.data.freightPercent,
        expectedDailyNetCash: dailyGeneration,
        minimumCashReserve: minimumReserve,
      })
      toast.success('Premissas de caixa salvas')
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível salvar as premissas'))
    } finally {
      setSaving(false)
    }
  }

  const projection = useMemo(() => data
    ? calculateCashProjection(data.dias, initialBalance, dailyGeneration, minimumReserve)
    : null, [dailyGeneration, data, initialBalance, minimumReserve])
  const totalReceber = data?.dias.reduce((total, day) => total + day.receberCrediario + day.receberOutros, 0) ?? 0
  const totalPagar = data?.dias.reduce((total, day) => total + day.pagar, 0) ?? 0

  return <div className="space-y-5 p-4 sm:p-6">
    <PageHeader icon={CalendarRange} title="Projeção de Caixa" description="Agenda de vencimentos e antecipação dos dias em que o caixa pode ficar pressionado" backHref="/admin/financeiro" actions={<button type="button" onClick={load} disabled={loading} className="btn-secondary" title="Atualizar dados"><RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} /></button>} />
    <FinanceiroSubnav />

    <section className="card space-y-5">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div><div className="flex items-center gap-1"><h2 className="text-base font-bold text-white">Premissas do cenário</h2><MetricHelp title="Como a projeção é formada"><p>Recebimentos e pagamentos vêm dos títulos abertos e vencimentos cadastrados.</p><p>O sistema não possui conciliação bancária; por isso o saldo disponível é informado manualmente.</p><p>A geração líquida diária e a reserva mínima podem ser salvas como padrão da loja.</p></MetricHelp></div><p className="mt-1 text-sm text-gray-400">Salvar não movimenta contas; o saldo de hoje permanece sempre manual.</p></div>
        <div className="flex flex-col gap-3 sm:flex-row"><div className="chip-row">{HORIZONS.map(days => <button type="button" key={days} onClick={() => setHorizon(days)} className={`min-h-10 rounded-md px-3 text-sm font-semibold ${horizon === days ? 'bg-brand-600 text-white' : 'bg-surface-700 text-gray-400 hover:text-white'}`}>{days} dias</button>)}</div><button type="button" onClick={saveAssumptions} disabled={saving} className="btn-secondary shrink-0"><Save className={`h-4 w-4 ${saving ? 'animate-pulse' : ''}`} />Salvar padrões</button></div>
      </div>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <MoneyInput label="Saldo disponível hoje" value={initialBalance} onChange={setInitialBalance} allowNegative />
        <MoneyInput label="Geração líquida esperada por dia" value={dailyGeneration} onChange={setDailyGeneration} allowNegative />
        <MoneyInput label="Reserva mínima desejada" value={minimumReserve} onChange={value => setMinimumReserve(Math.max(0, value))} />
      </div>
    </section>

    {loading ? <div className="flex justify-center py-20"><RefreshCw className="h-7 w-7 animate-spin text-brand-400" /></div> : data && projection && <>
      <section className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <ProjectionCard title="Saldo no fim" value={fmt(projection.saldoFinal)} tone={projection.saldoFinal >= minimumReserve ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-600 dark:text-red-300'} icon={CircleDollarSign}><p>Saldo acumulado ao final do horizonte, incluindo agenda confirmada e a geração diária informada.</p></ProjectionCard>
        <ProjectionCard title="Menor saldo" value={fmt(projection.menorSaldo)} tone={projection.menorSaldo >= minimumReserve ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-600 dark:text-red-300'} icon={TrendingDown}><p>Menor saldo encontrado ao percorrer os dias em ordem.</p></ProjectionCard>
        <ProjectionCard title="Necessidade de caixa" value={fmt(projection.necessidadeCaixa)} tone={projection.necessidadeCaixa > 0 ? 'text-red-600 dark:text-red-300' : 'text-emerald-700 dark:text-emerald-400'} icon={ShieldAlert}><p>Valor necessário para que o menor saldo não fique abaixo da reserva mínima escolhida.</p></ProjectionCard>
        <ProjectionCard title="Primeiro dia de risco" value={projection.primeiraDataRisco ? formatDate(projection.primeiraDataRisco) : 'Sem risco'} tone={projection.primeiraDataRisco ? 'text-amber-700 dark:text-amber-300' : 'text-emerald-700 dark:text-emerald-400'} icon={CalendarRange}><p>Primeira data em que o saldo projetado fica abaixo da reserva mínima.</p></ProjectionCard>
      </section>

      {(data.recebimentosSemData > 0 || data.pagamentosSemData > 0) && <div className="flex items-start gap-2 rounded-md border border-amber-500/40 bg-amber-500/10 p-3 text-sm text-amber-800 dark:text-amber-200"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" /><span><strong>Existem títulos fora da projeção por falta de vencimento.</strong> A receber: {fmt(data.recebimentosSemData)}; a pagar: {fmt(data.pagamentosSemData)}.</span></div>}

      <section className="card space-y-4">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between"><div><div className="flex items-center gap-1"><h2 className="text-base font-bold text-white">Trajetória do saldo</h2><MetricHelp title="Cenário confirmado e cenário esperado"><p>A linha soma o saldo inicial, os vencimentos confirmados e a geração líquida diária informada.</p><p>Contas vencidas são colocadas no primeiro dia por prudência. Títulos sem data ficam fora da linha e aparecem como alerta.</p></MetricHelp></div><p className="mt-1 text-sm text-gray-400">A linha tracejada representa a reserva mínima.</p></div><div className="flex gap-4 text-xs text-gray-500"><span>Receber: <strong className="font-mono text-emerald-700 dark:text-emerald-400">{fmt(totalReceber)}</strong></span><span>Pagar: <strong className="font-mono text-red-600 dark:text-red-300">{fmt(totalPagar)}</strong></span></div></div>
        <CashChart days={projection.dias} reserve={minimumReserve} />
      </section>

      <section className="card space-y-4">
        <div><h2 className="text-base font-bold text-white">Agenda diária</h2><p className="mt-1 text-sm text-gray-400">Dias sem movimentação confirmada permanecem visíveis para acompanhar o saldo acumulado.</p></div>
        <div className="table-scroll"><table className="w-full min-w-[820px] text-sm"><thead className="bg-surface-800 text-left text-xs uppercase text-gray-500"><tr><th className="px-3 py-3">Data</th><th className="px-3 py-3">Crediário</th><th className="px-3 py-3">Outros recebimentos</th><th className="px-3 py-3">Pagamentos</th><th className="px-3 py-3">Geração esperada</th><th className="px-3 py-3">Saldo projetado</th></tr></thead><tbody className="divide-y divide-surface-500">{projection.dias.map(day => <tr key={day.data} className={day.abaixoReserva ? 'bg-red-500/5' : ''}><td className="px-3 py-3 font-semibold text-white">{formatDate(day.data)}</td><td className="px-3 py-3 font-mono text-emerald-700 dark:text-emerald-400">{fmt(day.receberCrediario)}</td><td className="px-3 py-3 font-mono text-emerald-700 dark:text-emerald-400">{fmt(day.receberOutros)}</td><td className="px-3 py-3 font-mono text-red-600 dark:text-red-300">{fmt(day.pagar)}</td><td className="px-3 py-3 font-mono text-brand-300">{fmt(day.geracaoEsperada)}</td><td className={`px-3 py-3 font-mono font-bold ${day.abaixoReserva ? 'text-red-600 dark:text-red-300' : 'text-white'}`}>{fmt(day.saldoProjetado)}</td></tr>)}</tbody></table></div>
      </section>
      <p className="text-xs text-gray-500">Projeção gerencial, não saldo bancário conciliado. Cadastre vencimentos e revise títulos sem data para aumentar a cobertura do cenário.</p>
    </>}
  </div>
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString('pt-BR', { timeZone: 'UTC', day: '2-digit', month: '2-digit', year: '2-digit' })
}

function MoneyInput({ label, value, onChange, allowNegative = false }: { label: string; value: number; onChange: (value: number) => void; allowNegative?: boolean }) {
  return <label className="text-xs font-semibold text-gray-400">{label}<span className="relative mt-1 block"><span className="pointer-events-none absolute left-3 top-2.5 text-sm text-gray-500">R$</span><input aria-label={label} type="number" min={allowNegative ? undefined : 0} step="0.01" value={value} onChange={event => onChange(Number(event.target.value) || 0)} className="input w-full pl-9" /></span></label>
}

function ProjectionCard({ title, value, tone, icon: Icon, children }: { title: string; value: string; tone: string; icon: typeof CalendarRange; children: React.ReactNode }) {
  return <article className="card min-w-0"><div className="flex min-h-8 items-start justify-between gap-1"><div className="flex min-w-0 items-start gap-2"><Icon className="mt-0.5 h-4 w-4 shrink-0 text-gray-500" /><p className="text-[11px] font-semibold uppercase leading-4 text-gray-500 sm:text-xs">{title}</p></div><MetricHelp title={title}>{children}</MetricHelp></div><p className={`mt-2 break-words font-mono text-xl font-bold sm:text-2xl ${tone}`}>{value}</p></article>
}

function CashChart({ days, reserve }: { days: CashProjectionDay[]; reserve: number }) {
  const values = days.map(day => day.saldoProjetado)
  const min = Math.min(0, reserve, ...values)
  const max = Math.max(0, reserve, ...values)
  const span = Math.max(1, max - min)
  const y = (value: number) => 18 + ((max - value) / span) * 174
  const x = (index: number) => days.length <= 1 ? 0 : (index / (days.length - 1)) * 1000
  const points = values.map((value, index) => `${x(index)},${y(value)}`).join(' ')
  const risk = values.some(value => value < reserve)

  return <div className="h-56 w-full overflow-hidden rounded-md border border-surface-500 bg-surface-700/40 p-2" role="img" aria-label="Gráfico da trajetória do saldo projetado">
    <svg viewBox="0 0 1000 210" className="h-full w-full" preserveAspectRatio="none">
      <line x1="0" x2="1000" y1={y(reserve)} y2={y(reserve)} className="stroke-amber-500/70" strokeWidth="2" strokeDasharray="10 8" vectorEffect="non-scaling-stroke" />
      <polyline points={points} fill="none" className={risk ? 'stroke-red-500' : 'stroke-emerald-500'} strokeWidth="3" vectorEffect="non-scaling-stroke" />
      {days.map((day, index) => <circle key={day.data} cx={x(index)} cy={y(day.saldoProjetado)} r="4" className={day.abaixoReserva ? 'fill-red-500' : 'fill-emerald-500'} vectorEffect="non-scaling-stroke"><title>{formatDate(day.data)}: {fmt(day.saldoProjetado)}</title></circle>)}
    </svg>
  </div>
}
