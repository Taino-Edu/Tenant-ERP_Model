'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import { AlertTriangle, Boxes, PackageCheck, PackageSearch, RefreshCw, Search, TrendingUp } from 'lucide-react'
import toast from 'react-hot-toast'
import PageHeader from '@/components/admin/PageHeader'
import { FinanceiroSubnav } from '@/components/admin/financeiro/FinanceiroSubnav'
import { MetricHelp } from '@/components/admin/financeiro/MetricHelp'
import { fmt, getRange, type Preset } from '@/components/admin/financeiro/financeiro-shared'
import { analyticsApi, getErrorMessage, type EstoqueInteligenteDto, type EstoqueProdutoInsightDto } from '@/lib/api'

type StatusFilter = 'todos' | EstoqueProdutoInsightDto['situacao']

const STATUS: Record<EstoqueProdutoInsightDto['situacao'], { label: string; className: string }> = {
  ruptura: { label: 'Ruptura', className: 'bg-red-500/15 text-red-300' },
  baixo: { label: 'Cobertura baixa', className: 'bg-amber-500/15 text-amber-300' },
  excesso: { label: 'Excesso', className: 'bg-blue-500/15 text-blue-300' },
  sem_movimento: { label: 'Sem movimento', className: 'bg-surface-500 text-gray-300' },
  sem_custo: { label: 'Sem custo', className: 'bg-purple-500/15 text-purple-300' },
  saudavel: { label: 'Saudável', className: 'bg-emerald-500/15 text-emerald-300' },
}

export default function EstoqueInteligentePage() {
  const [preset, setPreset] = useState<Preset>('mes')
  const [inicio, setInicio] = useState(getRange('mes').inicio)
  const [fim, setFim] = useState(getRange('mes').fim)
  const [targetDays, setTargetDays] = useState(30)
  const [query, setQuery] = useState('')
  const [status, setStatus] = useState<StatusFilter>('todos')
  const [data, setData] = useState<EstoqueInteligenteDto | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const response = await analyticsApi.estoqueInteligente(inicio, fim)
      setData(response.data)
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível analisar o estoque'))
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
    return (data?.produtos ?? []).filter(product =>
      (status === 'todos' || product.situacao === status) &&
      (!normalized || `${product.nome} ${product.categoria}`.toLocaleLowerCase('pt-BR').includes(normalized)))
  }, [data, query, status])

  const capitalParado = useMemo(() => (data?.produtos ?? [])
    .filter(product => product.situacao === 'excesso' || product.situacao === 'sem_movimento')
    .reduce((total, product) => total + product.valorEstoque, 0), [data])

  return <div className="space-y-5 p-4 sm:p-6">
    <PageHeader icon={PackageSearch} title="Estoque Inteligente" description="Cobertura, retorno e capital parado por produto" backHref="/admin/financeiro" actions={<button type="button" onClick={load} disabled={loading} className="btn-secondary" title="Atualizar dados"><RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} /></button>} />
    <FinanceiroSubnav />

    <section className="card space-y-3" aria-label="Período da análise">
      <div className="chip-row w-full">{(['hoje', '7d', 'mes', 'custom'] as Preset[]).map(item => <button type="button" key={item} onClick={() => applyPreset(item)} className={`min-h-10 rounded-md px-3 text-sm font-medium ${preset === item ? 'bg-brand-600 text-white' : 'bg-surface-700 text-gray-400 hover:text-white'}`}>{{ hoje: 'Hoje', '7d': '7 dias', mes: 'Este mês', custom: 'Personalizado' }[item]}</button>)}</div>
      <div className="grid grid-cols-1 gap-3 xs:grid-cols-2 sm:max-w-md">
        <label className="text-xs font-semibold text-gray-400">Início<input type="date" value={inicio} onChange={event => { setPreset('custom'); setInicio(event.target.value) }} className="input mt-1 w-full" /></label>
        <label className="text-xs font-semibold text-gray-400">Fim<input type="date" value={fim} onChange={event => { setPreset('custom'); setFim(event.target.value) }} className="input mt-1 w-full" /></label>
      </div>
    </section>

    {loading ? <div className="flex justify-center py-20"><RefreshCw className="h-7 w-7 animate-spin text-brand-400" /></div> : data && <>
      <section className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StockCard title="Valor em estoque" value={fmt(data.valorTotalEstoque)} tone="text-white" icon={Boxes}><p>Custo atual multiplicado pelo estoque disponível de todos os produtos ativos.</p></StockCard>
        <StockCard title="Capital com baixa saída" value={fmt(capitalParado)} tone="text-blue-300" icon={PackageCheck}><p>Valor atual de produtos sem movimento no período ou com mais de 90 dias de cobertura.</p></StockCard>
        <StockCard title="GMROI estimado" value={data.gmroiEstimado === null ? 'Sem base' : `${data.gmroiEstimado.toFixed(2)}x`} tone="text-emerald-400" icon={TrendingUp}><p>Margem bruta gerada no período dividida pelo valor do estoque atual. Acima de 1x significa que a margem do período superou o capital hoje em estoque.</p></StockCard>
        <StockCard title="Risco de ruptura" value={String(data.produtosRiscoRuptura)} tone={data.produtosRiscoRuptura > 0 ? 'text-red-300' : 'text-emerald-400'} icon={AlertTriangle}><p>Produtos zerados com venda ou com até 14 dias de cobertura, incluindo itens abaixo do estoque mínimo.</p></StockCard>
      </section>

      <section className="card space-y-4">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div><div className="flex items-center gap-1"><h2 className="text-base font-bold text-white">Plano de estoque por produto</h2><MetricHelp title="Como ler a cobertura"><p>Cobertura é o estoque atual dividido pela venda média diária no período selecionado.</p><p>Sem vendas no período, o sistema mostra “Sem movimento” em vez de inventar uma duração.</p><p>A reposição sugerida completa a meta escolhida e não considera prazo do fornecedor nesta etapa.</p></MetricHelp></div><p className="mt-1 text-sm text-gray-400">A sugestão não altera o estoque nem cria uma compra.</p></div>
          <label className="w-full max-w-sm text-sm font-semibold text-gray-300">Meta de cobertura: <span className="font-mono text-brand-300">{targetDays} dias</span><input aria-label="Meta de cobertura do estoque" type="range" min="7" max="90" value={targetDays} onChange={event => setTargetDays(Number(event.target.value))} className="mt-2 w-full accent-[rgb(var(--brand-500))]" /></label>
        </div>

        {data.produtosSemCusto > 0 && <div className="flex items-start gap-2 rounded-md border border-purple-500/30 bg-purple-500/10 p-3 text-sm text-purple-200"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{data.produtosSemCusto} produto{data.produtosSemCusto === 1 ? '' : 's'} com estoque e custo zerado. O capital imobilizado e o GMROI ficam subestimados.</div>}

        <div className="flex flex-col gap-3 sm:flex-row">
          <label className="relative flex-1"><span className="sr-only">Buscar produto</span><Search className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-gray-500" /><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Buscar produto ou categoria" className="input w-full pl-9" /></label>
          <select value={status} onChange={event => setStatus(event.target.value as StatusFilter)} className="input sm:w-52" aria-label="Filtrar situação do estoque"><option value="todos">Todas as situações</option>{Object.entries(STATUS).map(([value, item]) => <option key={value} value={value}>{item.label}</option>)}</select>
        </div>

        {rows.length === 0 ? <div className="py-14 text-center text-sm text-gray-500">Nenhum produto encontrado neste filtro.</div> : <div className="table-scroll"><table className="w-full min-w-[1020px] text-sm"><thead className="bg-surface-800 text-left text-xs uppercase text-gray-500"><tr><th className="px-3 py-3">Produto</th><th className="px-3 py-3">Situação</th><th className="px-3 py-3">Estoque</th><th className="px-3 py-3">Vendido</th><th className="px-3 py-3">Cobertura</th><th className="px-3 py-3">Capital</th><th className="px-3 py-3">GMROI</th><th className="px-3 py-3">Reposição sugerida</th></tr></thead><tbody className="divide-y divide-surface-500">{rows.map(product => {
          const suggested = product.vendaMediaDiaria > 0 ? Math.max(0, Math.ceil(product.vendaMediaDiaria * targetDays - product.estoqueAtual)) : 0
          return <tr key={product.productId} className="hover:bg-surface-700/60"><td className="px-3 py-3"><Link href="/admin/estoque" className="block max-w-[230px] truncate font-semibold text-white hover:text-brand-300">{product.nome}</Link><p className="text-xs text-gray-500">{product.categoria || 'Sem categoria'}</p></td><td className="px-3 py-3"><span className={`rounded px-2 py-1 text-xs font-semibold ${STATUS[product.situacao].className}`}>{STATUS[product.situacao].label}</span></td><td className="px-3 py-3 font-mono text-gray-200">{product.estoqueAtual}<span className="text-xs text-gray-600"> / mín. {product.estoqueMinimo}</span></td><td className="px-3 py-3 font-mono text-gray-200">{product.quantidadeVendida}</td><td className="px-3 py-3 font-mono text-gray-200">{product.coberturaDias === null ? 'Sem movimento' : `${product.coberturaDias.toFixed(1)} dias`}</td><td className="px-3 py-3 font-mono text-amber-300">{fmt(product.valorEstoque)}</td><td className="px-3 py-3 font-mono text-emerald-400">{product.gmroiEstimado === null ? '--' : `${product.gmroiEstimado.toFixed(2)}x`}</td><td className="px-3 py-3 font-mono font-bold text-brand-300">{suggested > 0 ? `+ ${suggested} un.` : 'Sem compra'}</td></tr>
        })}</tbody></table></div>}
        <p className="text-xs text-gray-500">Vendas e margens consideram {data.diasPeriodo} dia{data.diasPeriodo === 1 ? '' : 's'}; estoque e custo representam o saldo atual.</p>
      </section>
    </>}
  </div>
}

function StockCard({ title, value, tone, icon: Icon, children }: { title: string; value: string; tone: string; icon: typeof Boxes; children: React.ReactNode }) {
  return <article className="card min-w-0"><div className="flex min-h-8 items-start justify-between gap-1"><div className="flex min-w-0 items-start gap-2"><Icon className="mt-0.5 h-4 w-4 shrink-0 text-gray-500" /><p className="text-[11px] font-semibold uppercase leading-4 text-gray-500 sm:text-xs">{title}</p></div><MetricHelp title={title}>{children}</MetricHelp></div><p className={`mt-2 break-words font-mono text-xl font-bold sm:text-2xl ${tone}`}>{value}</p></article>
}
