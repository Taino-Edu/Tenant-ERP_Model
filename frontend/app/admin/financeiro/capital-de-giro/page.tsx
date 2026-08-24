'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import {
  AlertTriangle, ArrowRight, CalendarClock, CircleDollarSign,
  Package, RefreshCw, RefreshCcw, WalletCards,
} from 'lucide-react'
import toast from 'react-hot-toast'
import PageHeader from '@/components/admin/PageHeader'
import { FinanceiroSubnav } from '@/components/admin/financeiro/FinanceiroSubnav'
import { MetricHelp } from '@/components/admin/financeiro/MetricHelp'
import { fmt, getRange, type Preset } from '@/components/admin/financeiro/financeiro-shared'
import { analyticsApi, getErrorMessage, type CapitalGiroDto } from '@/lib/api'

export default function CapitalGiroPage() {
  const [preset, setPreset] = useState<Preset>('mes')
  const [inicio, setInicio] = useState(getRange('mes').inicio)
  const [fim, setFim] = useState(getRange('mes').fim)
  const [data, setData] = useState<CapitalGiroDto | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const response = await analyticsApi.capitalGiro(inicio, fim)
      setData(response.data)
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível calcular o capital de giro'))
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

  const priorities = useMemo(() => {
    if (!data) return []
    const items: { title: string; detail: string; href: string; tone: string }[] = []
    if (data.vencidoReceber > 0) items.push({
      title: 'Cobrar valores vencidos', detail: `${fmt(data.vencidoReceber)} já passaram do vencimento.`,
      href: '/admin/crediario', tone: 'text-red-300',
    })
    if (data.vencePagar7Dias > 0) items.push({
      title: 'Preparar pagamentos dos próximos 7 dias', detail: `${fmt(data.vencePagar7Dias)} vencem em breve.`,
      href: '/admin/contas-receber', tone: 'text-amber-300',
    })
    if (data.produtosSemCusto > 0) items.push({
      title: 'Corrigir custos do estoque', detail: `${data.produtosSemCusto} produto${data.produtosSemCusto === 1 ? '' : 's'} com saldo não entram corretamente no valor imobilizado.`,
      href: '/admin/estoque', tone: 'text-amber-300',
    })
    if (data.coberturaEstoqueDias !== null && data.coberturaEstoqueDias > 90) items.push({
      title: 'Revisar excesso de cobertura', detail: `O estoque atual equivale a ${data.coberturaEstoqueDias.toFixed(1)} dias do CMV deste período.`,
      href: '/admin/estoque', tone: 'text-brand-300',
    })
    return items
  }, [data])

  return (
    <div className="space-y-5 p-4 sm:p-6">
      <PageHeader
        icon={RefreshCcw}
        title="Capital de Giro"
        description="Recursos presos na operação e tempo para o dinheiro voltar ao caixa"
        backHref="/admin/financeiro"
        actions={<button type="button" onClick={load} disabled={loading} className="btn-secondary" title="Atualizar dados"><RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} /></button>}
      />
      <FinanceiroSubnav />

      <section className="card space-y-3" aria-label="Período do cálculo">
        <div className="chip-row w-full">
          {(['hoje', '7d', 'mes', 'custom'] as Preset[]).map(item => (
            <button type="button" key={item} onClick={() => applyPreset(item)} className={`min-h-10 rounded-md px-3 text-sm font-medium ${preset === item ? 'bg-brand-600 text-white' : 'bg-surface-700 text-gray-400 hover:text-white'}`}>
              {{ hoje: 'Hoje', '7d': '7 dias', mes: 'Este mês', custom: 'Personalizado' }[item]}
            </button>
          ))}
        </div>
        <div className="grid grid-cols-1 gap-3 xs:grid-cols-2 sm:max-w-md">
          <label className="text-xs font-semibold text-gray-400">Início<input type="date" value={inicio} onChange={event => { setPreset('custom'); setInicio(event.target.value) }} className="input mt-1 w-full" /></label>
          <label className="text-xs font-semibold text-gray-400">Fim<input type="date" value={fim} onChange={event => { setPreset('custom'); setFim(event.target.value) }} className="input mt-1 w-full" /></label>
        </div>
      </section>

      {loading ? <div className="flex justify-center py-20"><RefreshCw className="h-7 w-7 animate-spin text-brand-400" /></div> : data && <>
        <section className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <CapitalCard title="Estoque imobilizado" value={fmt(data.estoqueImobilizado)} icon={Package} tone="text-amber-300">
            <p>Custo atual multiplicado pela quantidade disponível. Produtos sem custo reduzem artificialmente este valor.</p>
          </CapitalCard>
          <CapitalCard title="Contas a receber" value={fmt(data.contasReceber)} icon={WalletCards} tone="text-emerald-400">
            <p>Soma do saldo dos crediários abertos com outros lançamentos de entrada pendentes ou vencidos.</p>
          </CapitalCard>
          <CapitalCard title="Contas a pagar" value={fmt(data.contasPagar)} icon={CalendarClock} tone="text-red-300">
            <p>Lançamentos de saída pendentes ou vencidos. Valores pagos e cancelados não entram.</p>
          </CapitalCard>
          <CapitalCard title="Necessidade de capital" value={fmt(data.necessidadeCapitalGiro)} icon={CircleDollarSign} tone={data.necessidadeCapitalGiro > 0 ? 'text-brand-300' : 'text-emerald-400'}>
            <p>Quanto a operação mantém aplicado em estoque e recebíveis, descontando o financiamento dado pelos fornecedores.</p>
            <p className="font-mono text-xs text-brand-300">estoque + receber - pagar</p>
          </CapitalCard>
        </section>

        <section className="card">
          <div className="mb-5 flex items-center gap-1">
            <h2 className="text-base font-bold text-white">Ciclo financeiro estimado</h2>
            <MetricHelp title="Por que o ciclo é estimado?">
              <p>O sistema usa os saldos atuais e o ritmo de receita, CMV e compras do período selecionado.</p>
              <p>Como ainda não existem snapshots diários de estoque, a cobertura usa o estoque de hoje em vez do estoque médio histórico.</p>
              <p className="font-mono text-xs text-brand-300">ciclo = dias em estoque + prazo de recebimento - prazo de pagamento</p>
            </MetricHelp>
          </div>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-[1fr_auto_1fr_auto_1fr_auto_1fr] md:items-stretch">
            <CycleStage label="Estoque" value={days(data.coberturaEstoqueDias)} detail={`CMV: ${fmt(data.cmvPeriodo)}`} />
            <ArrowRight className="hidden h-5 w-5 self-center text-gray-600 md:block" />
            <CycleStage label="Recebimento" value={days(data.prazoMedioRecebimentoDias)} detail={`A receber: ${fmt(data.contasReceber)}`} />
            <span className="hidden self-center text-xl text-gray-600 md:block">−</span>
            <CycleStage label="Pagamento" value={days(data.prazoMedioPagamentoDias)} detail={`Compras: ${fmt(data.comprasEstoquePeriodo)}`} />
            <span className="hidden self-center text-xl text-gray-600 md:block">=</span>
            <CycleStage label="Ciclo financeiro" value={days(data.cicloFinanceiroDias)} detail={data.cicloFinanceiroDias === null ? 'Cadastre compras para completar' : 'Tempo estimado financiando a operação'} highlight />
          </div>
        </section>

        <section className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <div className="card">
            <h2 className="mb-4 text-base font-bold text-white">Pressão de curto prazo</h2>
            <dl className="space-y-3 text-sm">
              <BalanceRow label="Recebimentos vencidos" value={data.vencidoReceber} tone="text-red-300" />
              <BalanceRow label="Pagamentos vencidos" value={data.vencidoPagar} tone="text-red-300" />
              <BalanceRow label="Pagamentos nos próximos 7 dias" value={data.vencePagar7Dias} tone="text-amber-300" />
              <BalanceRow label="Crediário dentro do contas a receber" value={data.receberCrediario} tone="text-gray-200" />
              <BalanceRow label="Outros valores a receber" value={data.receberOutros} tone="text-gray-200" />
            </dl>
          </div>
          <div className="card">
            <h2 className="mb-4 text-base font-bold text-white">Prioridades sugeridas</h2>
            {priorities.length === 0 ? <p className="py-8 text-center text-sm text-gray-500">Nenhuma pressão imediata identificada com os dados atuais.</p> : (
              <div className="divide-y divide-surface-500">
                {priorities.map(item => <Link key={item.title} href={item.href} className="flex items-start gap-3 py-3 first:pt-0 last:pb-0 hover:bg-surface-700/40">
                  <AlertTriangle className={`mt-0.5 h-4 w-4 shrink-0 ${item.tone}`} />
                  <span className="min-w-0 flex-1"><strong className="block text-sm text-white">{item.title}</strong><span className="text-xs leading-5 text-gray-400">{item.detail}</span></span>
                  <ArrowRight className="h-4 w-4 shrink-0 text-gray-600" />
                </Link>)}
              </div>
            )}
          </div>
        </section>
        <p className="text-xs text-gray-500">Saldos atualizados agora; velocidades calculadas sobre {data.diasPeriodo} dia{data.diasPeriodo === 1 ? '' : 's'} do período selecionado.</p>
      </>}
    </div>
  )
}

function days(value: number | null) { return value === null ? 'Sem base' : `${value.toFixed(1)} dias` }

function CapitalCard({ title, value, icon: Icon, tone, children }: { title: string; value: string; icon: typeof Package; tone: string; children: React.ReactNode }) {
  return <article className="card min-w-0"><div className="flex min-h-8 items-start justify-between gap-1"><div className="flex min-w-0 items-start gap-2"><Icon className="mt-0.5 h-4 w-4 shrink-0 text-gray-500" /><p className="text-[11px] font-semibold uppercase leading-4 text-gray-500 sm:text-xs">{title}</p></div><MetricHelp title={title}>{children}</MetricHelp></div><p className={`mt-2 break-words font-mono text-xl font-bold sm:text-2xl ${tone}`}>{value}</p></article>
}

function CycleStage({ label, value, detail, highlight = false }: { label: string; value: string; detail: string; highlight?: boolean }) {
  return <div className={`rounded-md border p-4 ${highlight ? 'border-brand-500 bg-brand-600/10' : 'border-surface-500 bg-surface-700/50'}`}><p className="text-xs font-semibold uppercase text-gray-500">{label}</p><p className={`mt-1 font-mono text-lg font-bold ${highlight ? 'text-brand-300' : 'text-white'}`}>{value}</p><p className="mt-1 text-xs text-gray-500">{detail}</p></div>
}

function BalanceRow({ label, value, tone }: { label: string; value: number; tone: string }) {
  return <div className="flex items-center justify-between gap-3"><dt className="text-gray-400">{label}</dt><dd className={`shrink-0 font-mono font-semibold ${tone}`}>{fmt(value)}</dd></div>
}
