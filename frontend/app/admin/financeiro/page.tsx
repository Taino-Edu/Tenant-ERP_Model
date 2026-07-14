'use client'
import { useEffect, useState, useCallback, useRef, useMemo } from 'react'
import { analyticsApi, vendaAvulsaApi, FinanceiroDto, FormaPagamentoTotalDto, PagamentoCrediarioPeriodoDto, FechamentoPeriodoDto, getErrorMessage } from '@/lib/api'
import { gerarRelatorioPDF } from '@/lib/relatorio'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import PageHeader from '@/components/admin/PageHeader'
import StatCard from '@/components/admin/StatCard'
import toast from 'react-hot-toast'
import {
  TrendingUp, TrendingDown, DollarSign, AlertCircle,
  RefreshCw, Printer, Package, ShoppingBag, BarChart2,
  Banknote, CreditCard, QrCode, Receipt, ChevronDown, ChevronUp,
  Store, ShoppingCart, X, Search, Star, Wallet, Filter,
  FileText, Lightbulb, ArrowUp, ArrowDown, Minus,
} from 'lucide-react'
import { fmt, toDateInput, getRange, FORMA_LABELS, FORMA_ICONS, type Preset } from '@/components/admin/financeiro/financeiro-shared'
import { CurvaABCSection } from '@/components/admin/financeiro/CurvaABCSection'
import { KpiChartModal, kpiTrend, prevPeriodLabel, type ChartPoint } from '@/components/admin/financeiro/KpiChartModal'
import { FormasPagamentoSection } from '@/components/admin/financeiro/FormasPagamentoSection'
import { DayDetailModal } from '@/components/admin/financeiro/DayDetailModal'
import { DateQuickFilter, BarChart, DayPieChart, MargemDonut } from '@/components/admin/financeiro/FinanceiroCharts'

export default function FinanceiroPage() {
  const { site } = useSiteConfig()
  const [preset,    setPreset]    = useState<Preset>('mes')
  const [inicio,    setInicio]    = useState(getRange('mes').inicio)
  const [fim,       setFim]       = useState(getRange('mes').fim)
  const [data,      setData]      = useState<FinanceiroDto | null>(null)
  const [loading,     setLoading]     = useState(true)
  const [exporting,   setExporting]   = useState(false)
  const [backfilling, setBackfilling] = useState(false)
  const [kpiModal,   setKpiModal]   = useState<string | null>(null)
  const [targetPct,  setTargetPct]  = useState(40)
  const [tableView,  setTableView]  = useState<'simples' | 'analise' | 'abc'>('analise')
  // Defesa: se o módulo Estoque for desabilitado enquanto a aba ABC estava
  // selecionada (ou o estado vier de um acesso anterior), volta pra Análise.
  useEffect(() => {
    if (tableView === 'abc' && !site.enabledModules.includes('estoque')) setTableView('analise')
  }, [tableView, site.enabledModules])
  const [prevData,   setPrevData]   = useState<FinanceiroDto | null>(null)
  const [filterPaymentMethod, setFilterPaymentMethod] = useState('')
  const [topOrigemFilter, setTopOrigemFilter] = useState<'Todos' | 'Comanda' | 'PDV'>('Todos')
  const [topCatFilter, setTopCatFilter] = useState<string | null>(null)
  const [metaManualInput, setMetaManualInput] = useState('')
  const [dayModal,  setDayModal]  = useState<FinanceiroDto['diaDia'][0] | null>(null)
  const iniRef    = useRef(inicio)
  const fimRef    = useRef(fim)
  const loadIdRef = useRef(0)

  // Mapeia um snapshot congelado (FechamentoPeriodoDto) pro formato de
  // FinanceiroDto que a UI de tendência já consome — só os campos que a
  // comparação de KPI realmente lê (receita/custo/margemPercent) vêm
  // preenchidos; crediarios (saldo atual, não é valor de período) e
  // pagamentosPorForma (não gravados no fechamento) ficam vazios, então o
  // badge de "Ticket médio" simplesmente não aparece pra uma comparação vinda
  // de snapshot — degradação graciosa (pctChange já trata prev=0 como "sem
  // comparação"), não um bug.
  function fechamentoToFinanceiro(f: FechamentoPeriodoDto): FinanceiroDto {
    return {
      receita: f.receita,
      receitaComandas: f.receitaComandas,
      receitaAvulsa: f.receitaAvulsa,
      custo: f.custo,
      margem: f.margem,
      margemPercent: f.custo > 0 ? Math.round((f.margem / f.custo) * 100 * 10) / 10 : 0,
      crediarios: 0,
      recebidoCrediario: 0,
      diaDia: [],
      topProdutos: [],
      pagamentosPorForma: [],
      pagamentosCrediarioPeriodo: [],
    }
  }

  // Compara qualquer preset com o período equivalente imediatamente anterior:
  // "mês" com o mês calendário anterior (como já era), "hoje" com ontem,
  // "7d"/custom com uma janela do mesmo tamanho logo antes. Pra "mês" e "hoje"
  // (que caem certinho numa janela Mes/Dia que o fechamento formal também
  // fecha), tenta o snapshot congelado primeiro — mais rápido e é o número
  // "oficial" se aquele período já foi fechado; sem snapshot (período ainda
  // não fechado), cai pro cálculo ao vivo, do jeito que sempre funcionou.
  const loadPrevPeriod = useCallback(async (p: Preset, currentIni: string, currentFim: string) => {
    let prevIni: string, prevFim: string
    let tipoFechamento: 'Dia' | 'Mes' | null = null

    if (p === 'mes') {
      const iniDate     = new Date(currentIni + 'T12:00:00')
      const prevFimDate = new Date(iniDate.getFullYear(), iniDate.getMonth(), 0)
      const prevIniDate = new Date(prevFimDate.getFullYear(), prevFimDate.getMonth(), 1)
      prevIni = toDateInput(prevIniDate)
      prevFim = toDateInput(prevFimDate)
      tipoFechamento = 'Mes'
    } else if (p === 'hoje') {
      const ontem = new Date(currentIni + 'T12:00:00')
      ontem.setDate(ontem.getDate() - 1)
      prevIni = prevFim = toDateInput(ontem)
      tipoFechamento = 'Dia'
    } else {
      // '7d' ou 'custom': janela imediatamente anterior, do mesmo tamanho —
      // não cai numa janela Dia/Semana/Mes fixa, então não tem snapshot pra
      // consultar, vai direto pro cálculo ao vivo.
      const iniDate      = new Date(currentIni + 'T12:00:00')
      const fimDate      = new Date(currentFim  + 'T12:00:00')
      const lengthMs     = fimDate.getTime() - iniDate.getTime()
      const prevFimDate  = new Date(iniDate)
      prevFimDate.setDate(prevFimDate.getDate() - 1)
      const prevIniDate  = new Date(prevFimDate.getTime() - lengthMs)
      prevIni = toDateInput(prevIniDate)
      prevFim = toDateInput(prevFimDate)
    }

    if (tipoFechamento) {
      try {
        const snap = await analyticsApi.getFechamento(tipoFechamento, prevIni, prevFim)
        setPrevData(fechamentoToFinanceiro(snap.data))
        return
      } catch { /* ainda não fechado — cai pro cálculo ao vivo abaixo */ }
    }

    try {
      const res = await analyticsApi.financeiro(prevIni, prevFim)
      setPrevData(res.data)
    } catch { setPrevData(null) }
  }, [])

  const load = useCallback(async (ini: string, f: string, pmFilter?: string) => {
    const id = ++loadIdRef.current
    setLoading(true)
    try {
      const res = await analyticsApi.financeiro(ini, f, pmFilter || undefined)
      if (id === loadIdRef.current) setData(res.data)
    } catch (err) {
      if (id === loadIdRef.current) toast.error(getErrorMessage(err, 'Erro ao carregar dados financeiros'))
    } finally {
      if (id === loadIdRef.current) setLoading(false)
    }
  }, [])

  useEffect(() => { load(inicio, fim); loadPrevPeriod(preset, inicio, fim) }, []) // eslint-disable-line

  function applyPreset(p: Preset) {
    setPreset(p)
    if (p !== 'custom') {
      const { inicio: ini, fim: f } = getRange(p)
      setInicio(ini); setFim(f)
      iniRef.current = ini; fimRef.current = f
      load(ini, f, filterPaymentMethod)
      loadPrevPeriod(p, ini, f)
    }
  }

  function applyCustom() {
    setPreset('custom')
    load(inicio, fim, filterPaymentMethod)
    loadPrevPeriod('custom', inicio, fim)
  }

  const d = data

  // ── Dados dos modais de KPI ────────────────────────────────────────────────
  const totalTx = d ? d.pagamentosPorForma.reduce((s, f) => s + f.quantidade, 0) : 0
  const ticketMedio = totalTx > 0 && d ? d.receita / totalTx : 0

  // ── Meta de faturamento ───────────────────────────────────────────────────
  const metaAuto    = d && d.custo > 0 ? Math.round(d.custo / (1 - targetPct / 100) * 100) / 100 : 0
  const metaFinal   = metaManualInput ? (parseFloat(metaManualInput.replace(',', '.')) || 0) : metaAuto
  const metaPct     = metaFinal > 0 && d ? Math.min(100, (d.receita / metaFinal) * 100) : 0

  // ── Top Produtos filtrados ────────────────────────────────────────────────
  const topCats = useMemo(() => {
    if (!d) return [] as string[]
    return [...new Set(d.topProdutos.map(p => p.categoria).filter(Boolean))] as string[]
  }, [d])

  const topFiltered = useMemo((): typeof d extends null ? [] : NonNullable<typeof d>['topProdutos'] => {
    if (!d) return []
    return d.topProdutos.filter(p => {
      if (topOrigemFilter === 'Comanda' && p.receitaComandas === 0 && p.qtdComandas === 0) return false
      if (topOrigemFilter === 'PDV'     && p.receitaAvulsa   === 0 && p.qtdAvulsa   === 0) return false
      if (topCatFilter && p.categoria !== topCatFilter) return false
      return true
    })
  }, [d, topOrigemFilter, topCatFilter])

  const kpiModais: Record<string, {
    title: string; color: string; totalLabel: string
    points: ChartPoint[]; extra?: React.ReactNode
  }> = d ? {
    receita: {
      title: 'Receita Total — Evolução Diária',
      color: 'green',
      totalLabel: fmt(d.receita),
      points: d.diaDia.map(x => ({ label: x.dia, value: x.receita })),
    },
    custo: {
      title: 'Custo Estimado — Evolução Diária',
      color: 'red',
      totalLabel: fmt(d.custo),
      points: d.diaDia.map(x => ({ label: x.dia, value: x.custo })),
      extra: d.topProdutos.filter(p => p.custo > 0).length > 0 ? (
        <div className="space-y-2">
          <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">Top custos por produto</p>
          {d.topProdutos.filter(p => p.custo > 0).slice(0, 6).map(p => (
            <div key={p.nome} className="flex items-center gap-2">
              <div className="flex-1 min-w-0">
                <p className="text-xs text-white truncate">{p.nome}</p>
                <div className="h-1 bg-surface-600 rounded-full overflow-hidden mt-1">
                  <div className="h-full bg-red-500/60 rounded-full"
                    style={{ width: `${Math.min(100, (p.custo / (d.topProdutos[0]?.custo || 1)) * 100)}%` }} />
                </div>
              </div>
              <span className="text-xs font-mono text-red-400 shrink-0">{fmt(p.custo)}</span>
            </div>
          ))}
        </div>
      ) : undefined,
    },
    margem: {
      title: 'Margem Bruta — Evolução Diária',
      color: d.margem >= 0 ? 'brand' : 'red',
      totalLabel: `${fmt(d.margem)} (${d.margemPercent.toFixed(1)}%)`,
      points: d.diaDia.map(x => ({ label: x.dia, value: Math.max(0, x.receita - x.custo) })),
    },
    ticket: {
      title: 'Ticket Médio — Distribuição por Pagamento',
      color: 'brand',
      totalLabel: fmt(ticketMedio),
      points: d.diaDia.map(x => ({ label: x.dia, value: x.receita })),
      extra: (
        <div className="space-y-2">
          <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">{totalTx} transações no período</p>
          {d.pagamentosPorForma.slice(0, 5).map(f => (
            <div key={f.forma} className="flex items-center justify-between text-xs">
              <span className="text-gray-300">{FORMA_LABELS[f.forma] ?? f.forma}</span>
              <span className="font-mono text-white">{f.quantidade}× · {fmt(f.total)}</span>
            </div>
          ))}
        </div>
      ),
    },
    crediarios: {
      title: 'Crediário — Recebimentos no Período',
      color: 'yellow',
      totalLabel: d.recebidoCrediario > 0 ? fmt(d.recebidoCrediario) : 'R$ 0,00',
      points: [],
      extra: (
        <div className="space-y-3">
          {/* Saldo em aberto */}
          <div className="bg-amber-500/10 border border-amber-500/20 rounded-xl p-3 flex items-center justify-between">
            <span className="text-xs text-amber-300">Saldo total em aberto</span>
            <span className="text-sm font-bold font-mono text-amber-400">{fmt(d.crediarios)}</span>
          </div>
          {/* Lista de pagamentos no período */}
          {d.pagamentosCrediarioPeriodo.length === 0 ? (
            <p className="text-xs text-gray-500 text-center py-2">Nenhum pagamento de crediário neste período.</p>
          ) : (
            <div className="space-y-1 max-h-52 overflow-y-auto pr-1">
              <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold mb-1">
                {d.pagamentosCrediarioPeriodo.length} pagamento{d.pagamentosCrediarioPeriodo.length !== 1 ? 's' : ''} recebido{d.pagamentosCrediarioPeriodo.length !== 1 ? 's' : ''}
              </p>
              {d.pagamentosCrediarioPeriodo.map((p: PagamentoCrediarioPeriodoDto, i: number) => (
                <div key={i} className="flex items-center justify-between bg-surface-700 rounded-lg px-3 py-2 text-xs">
                  <div className="min-w-0">
                    <p className="text-white font-medium truncate">{p.clienteNome}</p>
                    <p className="text-gray-500">
                      {p.formaPagamento} · {new Date(p.createdAt).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })}
                      {p.observacao && <span className="ml-1 text-amber-500">{p.observacao}</span>}
                    </p>
                  </div>
                  <span className="font-bold font-mono text-emerald-400 shrink-0 ml-3">{fmt(p.valorEmReais)}</span>
                </div>
              ))}
            </div>
          )}
          <a href="/admin/crediario" className="block text-center text-xs text-brand-400 hover:text-brand-300 transition-colors">
            Gerenciar crediários →
          </a>
        </div>
      ),
    },
  } : {}

  return (
    <div className="p-4 sm:p-6 space-y-4 sm:space-y-6 print:p-0">

      {/* Modal KPI */}
      {kpiModal && kpiModais[kpiModal] && (
        <KpiChartModal {...kpiModais[kpiModal]} onClose={() => setKpiModal(null)} />
      )}

      {/* Modal de detalhe do dia */}
      {dayModal && <DayDetailModal day={dayModal} onClose={() => setDayModal(null)} />}

      <div className="print:hidden">
        <PageHeader
          icon={TrendingUp}
          title="Controle Financeiro"
          description="Receita, custo e margem do período"
          actions={<>
            <button
              onClick={async () => {
                if (!d) return
                setExporting(true)
                try { await gerarRelatorioPDF(d, { inicio, fim }, site.siteName) }
                catch { toast.error('Erro ao gerar PDF') }
                finally { setExporting(false) }
              }}
              disabled={!d || exporting}
              className="btn-secondary text-sm print:hidden"
            >
              <Printer className="w-4 h-4" />
              <span className="hidden sm:inline">{exporting ? 'Gerando...' : 'Exportar PDF'}</span>
            </button>
            <button
              onClick={async () => {
                if (backfilling) return
                setBackfilling(true)
                try {
                  const r = await vendaAvulsaApi.backfillCosts()
                  toast.success(r.data.mensagem)
                  load(inicio, fim)
                } catch (err) {
                  toast.error(getErrorMessage(err, 'Erro ao corrigir custos históricos'))
                } finally {
                  setBackfilling(false)
                }
              }}
              disabled={backfilling}
              title="Preenche custo zero em vendas avulsas antigas usando o custo atual dos produtos"
              className="btn-secondary text-sm print:hidden"
            >
              <RefreshCw className={`w-4 h-4 ${backfilling ? 'animate-spin' : ''}`} />
              <span className="hidden sm:inline">{backfilling ? 'Corrigindo...' : 'Corrigir custos'}</span>
            </button>
            <button onClick={() => load(inicio, fim, filterPaymentMethod)} disabled={loading} className="btn-secondary text-sm">
              <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            </button>
          </>}
        />
      </div>

      {/* ── Filtros ── sticky no topo */}
      <div className="sticky top-0 z-10 print:hidden">
        <div className="card flex flex-col sm:flex-row sm:flex-wrap gap-3 sm:items-center border-surface-500 shadow-xl overflow-hidden">
          {/* Presets */}
          <div className="flex gap-1.5 flex-wrap">
            {(['hoje', '7d', 'mes', 'custom'] as Preset[]).map(p => (
              <button key={p} onClick={() => applyPreset(p)}
                className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-all ${
                  preset === p ? 'bg-brand-600 text-white shadow-lg shadow-brand-600/20' : 'bg-surface-700 text-gray-400 hover:text-white hover:bg-surface-500'
                }`}
              >
                {{ hoje: 'Hoje', '7d': '7 dias', mes: 'Este mês', custom: 'Personalizado' }[p]}
              </button>
            ))}
          </div>

          {/* Filtro de forma de pagamento */}
          <div className="flex gap-1.5 overflow-x-auto pb-0.5 scrollbar-none">
            {['', 'Pix', 'Dinheiro', 'CartaoCredito', 'CartaoDebito', 'Crediario'].map(pm => (
              <button
                key={pm || 'all'}
                onClick={() => {
                  setFilterPaymentMethod(pm)
                  load(inicio, fim, pm)
                }}
                className={`flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-medium border transition-all ${
                  filterPaymentMethod === pm
                    ? 'bg-brand-600/30 border-brand-500 text-brand-200'
                    : 'bg-surface-700 border-surface-600 text-gray-400 hover:border-surface-500 hover:text-gray-200'
                }`}
              >
                {pm ? FORMA_ICONS[pm] : null}
                {pm ? (FORMA_LABELS[pm] ?? pm) : 'Todos'}
              </button>
            ))}
          </div>

          {/* Período atual */}
          {preset !== 'custom' && (
            <span className="text-xs text-gray-500 ml-1">
              {inicio === fim ? inicio : `${inicio} → ${fim}`}
            </span>
          )}

          {/* Custom inline — sem scroll */}
          {preset === 'custom' && (
            <div className="flex items-center gap-2 flex-wrap">
              <input type="date" className="input py-1.5 text-sm w-full sm:w-36"
                value={inicio} max={fim}
                onChange={e => setInicio(e.target.value)} />
              <span className="text-gray-500 text-sm">até</span>
              <input type="date" className="input py-1.5 text-sm w-full sm:w-36"
                value={fim} min={inicio} max={toDateInput(new Date())}
                onChange={e => setFim(e.target.value)} />
              <button
                onClick={applyCustom}
                disabled={loading}
                className="btn-primary text-sm py-1.5 min-w-[90px]"
              >
                {loading ? <RefreshCw className="w-4 h-4 animate-spin" /> : null}
                {loading ? 'Carregando…' : 'Aplicar'}
              </button>
            </div>
          )}

          {/* Loading indicator inline */}
          {loading && preset !== 'custom' && (
            <RefreshCw className="w-4 h-4 animate-spin text-brand-400 ml-auto" />
          )}
        </div>
      </div>

      {loading ? (
        <div className="flex justify-center py-20">
          <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>
      ) : d ? (
        <>
          {/* KPIs — clicáveis */}
          {(() => {
            function pctChange(cur: number, prev: number): number | null {
              if (!prevData || prev === 0) return null
              return ((cur - prev) / prev) * 100
            }
            const prevTx = prevData ? prevData.pagamentosPorForma.reduce((s, f) => s + f.quantidade, 0) : 0
            const prevTicket = prevTx > 0 && prevData ? prevData.receita / prevTx : 0
            const prevLabel = prevPeriodLabel(preset)
            return (
              <div className="grid grid-cols-2 lg:grid-cols-5 gap-4">
                <StatCard label="Receita total"      value={fmt(d.receita)}     sub={`${d.diaDia.length} dias`}                      tone="success" icon={TrendingUp}   onClick={() => setKpiModal('receita')}    trend={kpiTrend(pctChange(d.receita,    prevData?.receita    ?? 0), prevLabel)} />
                <StatCard label="Custo estimado"     value={fmt(d.custo)}       sub="Clique para detalhar por produto"               tone="danger"  icon={ShoppingBag}  onClick={() => setKpiModal('custo')}      trend={kpiTrend(pctChange(d.custo,      prevData?.custo      ?? 0), prevLabel)} />
                <StatCard label="Margem média"        value={`${d.margemPercent.toFixed(1)}%`} sub={`${fmt(d.margem)} sobre custo`} tone={d.margem >= 0 ? 'brand' : 'danger'} icon={d.margem >= 0 ? TrendingUp : TrendingDown} onClick={() => setKpiModal('margem')} trend={kpiTrend(pctChange(d.margemPercent, prevData?.margemPercent ?? 0), prevLabel)} />
                <StatCard label="Ticket médio"       value={fmt(ticketMedio)}   sub={`${totalTx} transação${totalTx !== 1 ? 'ões' : ''}`}  tone="brand"  icon={CreditCard}   onClick={() => setKpiModal('ticket')}     trend={kpiTrend(pctChange(ticketMedio,  prevTicket), prevLabel)} />
                <StatCard label="Crediários abertos" value={fmt(d.crediarios)}  sub={d.recebidoCrediario > 0 ? `Recebido no período: ${fmt(d.recebidoCrediario)}` : 'A receber · clique para detalhar'} tone="warning" icon={AlertCircle}  onClick={() => setKpiModal('crediarios')} trend={kpiTrend(pctChange(d.crediarios, prevData?.crediarios ?? 0), prevLabel)} />
              </div>
            )
          })()}

          {/* Breakdown Comandas vs Avulsas */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="card flex items-center gap-4">
              <div className="w-10 h-10 rounded-xl bg-brand-600/15 flex items-center justify-center shrink-0">
                <ShoppingBag className="w-5 h-5 text-brand-400" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">Comandas (mesas)</p>
                <p className="text-xl font-bold font-mono text-brand-400">{fmt(d.receitaComandas)}</p>
                {d.receita > 0 && (
                  <p className="text-xs text-gray-400 mt-0.5">{((d.receitaComandas / d.receita) * 100).toFixed(1)}% do total</p>
                )}
              </div>
            </div>
            <div className="card flex items-center gap-4">
              <div className="w-10 h-10 rounded-xl bg-emerald-500/15 flex items-center justify-center shrink-0">
                <Package className="w-5 h-5 text-emerald-400" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">Venda Avulsa (balcão)</p>
                <p className="text-xl font-bold font-mono text-emerald-400">{fmt(d.receitaAvulsa)}</p>
                {d.receita > 0 && (
                  <p className="text-xs text-gray-400 mt-0.5">{((d.receitaAvulsa / d.receita) * 100).toFixed(1)}% do total</p>
                )}
              </div>
            </div>
          </div>

          {/* ── DRE ─────────────────────────────────────────────────────── */}
          {d.receita > 0 && (
            <div className="card p-0 overflow-hidden">
              <div className="px-5 py-4 border-b border-surface-500 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <FileText className="w-4 h-4 text-brand-400" />
                  <h3 className="text-sm font-semibold text-gray-300">DRE — Demonstração do Resultado</h3>
                </div>
                <span className="text-[11px] text-gray-500">{inicio === fim ? inicio : `${inicio} → ${fim}`}</span>
              </div>

              <div className="p-5 space-y-0">
                {/* Receita Bruta */}
                <div className="flex items-center justify-between py-2.5 border-b border-surface-600">
                  <div>
                    <p className="text-sm font-semibold text-gray-200">Receita Bruta</p>
                    <div className="flex gap-4 mt-1">
                      <span className="text-xs text-gray-500">Comandas: <span className="text-gray-300">{fmt(d.receitaComandas)}</span></span>
                      <span className="text-xs text-gray-500">Avulsas: <span className="text-gray-300">{fmt(d.receitaAvulsa)}</span></span>
                    </div>
                  </div>
                  <span className="text-emerald-400 font-bold font-mono text-lg">{fmt(d.receita)}</span>
                </div>

                {/* CMV */}
                {d.custo > 0 ? (
                  <>
                    <div className="flex items-center justify-between py-2.5 border-b border-surface-600">
                      <p className="text-sm text-gray-400">(−) CMV — Custo das Mercadorias Vendidas</p>
                      <span className="text-red-400 font-mono">({fmt(d.custo)})</span>
                    </div>

                    {/* Lucro Bruto */}
                    <div className="flex items-center justify-between py-3 border-b-2 border-surface-400">
                      <p className="text-base font-black text-white">LUCRO BRUTO</p>
                      <div className="text-right">
                        <span className={`font-black font-mono text-xl ${d.margem >= 0 ? 'text-brand-400' : 'text-red-400'}`}>
                          {fmt(d.margem)}
                        </span>
                        <span className="text-xs text-gray-500 ml-2 font-mono">({d.margemPercent.toFixed(1)}%)</span>
                      </div>
                    </div>

                    {/* Crediários */}
                    {d.crediarios > 0 && (
                      <>
                        <div className="flex items-center justify-between py-2.5 border-b border-surface-600">
                          <p className="text-sm text-gray-400">(−) Crediários em Aberto</p>
                          <span className="text-amber-400 font-mono">({fmt(d.crediarios)})</span>
                        </div>
                        <div className="flex items-center justify-between py-2.5 border-b border-surface-600">
                          <p className="text-sm text-gray-300">Resultado Estimado</p>
                          <span className={`font-semibold font-mono ${d.margem - d.crediarios >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                            {fmt(d.margem - d.crediarios)}
                          </span>
                        </div>
                      </>
                    )}

                    {/* Projeção */}
                    {preset === 'mes' && d.projecao && (
                      <div className="flex items-center justify-between py-3 mt-1 rounded-xl bg-brand-500/8 px-4 -mx-0">
                        <div>
                          <p className="text-xs font-semibold text-brand-300">📈 Projeção para o mês completo</p>
                          {d.projecao.metodo === 'ponderado' && (
                            <p className="text-[11px] text-gray-500 mt-0.5">baseado nas últimas semanas, por dia da semana</p>
                          )}
                        </div>
                        <span className="font-black font-mono text-brand-400 text-lg">{fmt(d.projecao.valorProjetado)}</span>
                      </div>
                    )}
                  </>
                ) : (
                  <p className="py-3 text-xs text-yellow-400/80">
                    Cadastre o preço de custo nos produtos para ver Lucro Bruto e CMV.
                  </p>
                )}
              </div>
            </div>
          )}

          {/* ── Meta de Faturamento ─────────────────────────────────────────── */}
          {d.receita > 0 && (
            <div className="card p-0 overflow-hidden">
              <div className="px-5 py-4 border-b border-surface-500 flex items-center justify-between flex-wrap gap-3">
                <div className="flex items-center gap-2">
                  <TrendingUp className="w-4 h-4 text-emerald-400" />
                  <h3 className="text-sm font-semibold text-gray-300">Meta de Faturamento</h3>
                </div>
                {/* Seletor margem alvo */}
                <div className="flex items-center gap-1.5">
                  <span className="text-xs text-gray-500">Margem alvo:</span>
                  {[30, 40, 50, 60].map(pct => (
                    <button key={pct} onClick={() => setTargetPct(pct)}
                      className={`px-2.5 py-1 rounded-lg text-xs font-bold border transition-all ${
                        targetPct === pct
                          ? 'bg-brand-500/20 border-brand-500/60 text-brand-300'
                          : 'bg-surface-700 border-surface-600 text-gray-400 hover:text-gray-200'
                      }`}>
                      {pct}%
                    </button>
                  ))}
                </div>
              </div>
              <div className="p-5 space-y-4">
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                  {/* Meta automática */}
                  <div className="bg-surface-800 rounded-xl p-4 border border-surface-600">
                    <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold mb-1">Meta Automática</p>
                    <p className="text-lg font-bold font-mono text-brand-400">{metaAuto > 0 ? fmt(metaAuto) : '—'}</p>
                    <p className="text-[10px] text-gray-500 mt-1">Custo ÷ (1 − {targetPct}%) · baseada no CMV do período</p>
                  </div>
                  {/* Meta manual */}
                  <div className="bg-surface-800 rounded-xl p-4 border border-surface-600">
                    <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold mb-1">Meta Manual</p>
                    <div className="flex items-center gap-2">
                      <span className="text-gray-400 text-sm">R$</span>
                      <input
                        className="input py-1 text-sm font-mono flex-1 min-w-0"
                        placeholder="Ex: 10000"
                        value={metaManualInput}
                        onChange={e => setMetaManualInput(e.target.value)}
                        type="number" min="0" step="0.01"
                      />
                      {metaManualInput && (
                        <button onClick={() => setMetaManualInput('')} className="text-gray-500 hover:text-gray-300 transition-colors">
                          <X className="w-4 h-4" />
                        </button>
                      )}
                    </div>
                    <p className="text-[10px] text-gray-500 mt-1">
                      {metaManualInput ? 'Meta manual ativa' : 'Vazio = usa meta automática'}
                    </p>
                  </div>
                  {/* Realizado */}
                  <div className="bg-surface-800 rounded-xl p-4 border border-surface-600">
                    <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold mb-1">Realizado</p>
                    <p className={`text-lg font-bold font-mono ${metaPct >= 100 ? 'text-emerald-400' : metaPct >= 75 ? 'text-brand-400' : metaPct >= 50 ? 'text-yellow-400' : 'text-red-400'}`}>
                      {fmt(d.receita)}
                    </p>
                    <p className="text-[10px] text-gray-500 mt-1">
                      Falta {metaFinal > d.receita ? fmt(metaFinal - d.receita) : 'Meta atingida!'}
                    </p>
                  </div>
                </div>

                {/* Barra de progresso */}
                {metaFinal > 0 && (
                  <div className="space-y-2">
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-gray-400">Progresso em direção à meta de {fmt(metaFinal)}</span>
                      <span className={`font-bold font-mono ${metaPct >= 100 ? 'text-emerald-400' : metaPct >= 75 ? 'text-brand-400' : metaPct >= 50 ? 'text-yellow-400' : 'text-red-400'}`}>
                        {metaPct.toFixed(1)}%
                      </span>
                    </div>
                    <div className="h-3 bg-surface-700 rounded-full overflow-hidden">
                      <div
                        className={`h-full rounded-full transition-all ${metaPct >= 100 ? 'bg-emerald-500' : metaPct >= 75 ? 'bg-brand-500' : metaPct >= 50 ? 'bg-yellow-500' : 'bg-red-500'}`}
                        style={{ width: `${Math.min(100, metaPct)}%` }}
                      />
                    </div>
                    <div className="flex justify-between text-[10px] text-gray-500">
                      <span>R$ 0</span>
                      {metaPct < 90 && <span style={{ marginLeft: `${Math.max(0, metaPct - 5)}%` }}>{fmt(d.receita)}</span>}
                      <span>{fmt(metaFinal)}</span>
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Gráfico + Donut — pizza para 1 dia, barras para múltiplos */}
          <div className="space-y-2">
            {/* Mini filtro de período — comodidade para quem está rolando a página */}
            <div className="card py-2.5 px-4">
              <DateQuickFilter
                preset={preset}
                onPreset={applyPreset}
                inicio={inicio}
                fim={fim}
              />
            </div>

            {inicio === fim ? (
              <div className="grid grid-cols-1 lg:grid-cols-4 gap-4">
                <div className="lg:col-span-3">
                  <DayPieChart formas={d.pagamentosPorForma} receita={d.receita} custo={d.custo} date={inicio} />
                </div>
                <MargemDonut receita={d.receita} custo={d.custo} />
              </div>
            ) : (
              <div className="grid grid-cols-1 lg:grid-cols-4 gap-4">
                <div className="lg:col-span-3">
                  <BarChart dias={d.diaDia} onDayClick={setDayModal} />
                </div>
                <MargemDonut receita={d.receita} custo={d.custo} />
              </div>
            )}
          </div>

          {/* Formas de pagamento */}
          {d.pagamentosPorForma.length > 0 && (
            <FormasPagamentoSection formas={d.pagamentosPorForma} />
          )}

          {/* Top produtos */}
          {d.topProdutos.length > 0 && (
            <div className="card p-0 overflow-hidden">
              {/* Header */}
              <div className="px-5 py-4 border-b border-surface-500 space-y-3">
                <div className="flex items-center justify-between flex-wrap gap-3">
                  <div className="flex items-center gap-2">
                    <Lightbulb className="w-4 h-4 text-yellow-400" />
                    <h3 className="text-sm font-semibold text-gray-300">
                      {tableView === 'analise' ? 'Top Produtos — Rentabilidade & Sugestão de Preço'
                        : tableView === 'abc'  ? 'Top Produtos — Curva ABC & Peso por Categoria'
                        : 'Top Produtos — Resumo de Vendas'}
                    </h3>
                  </div>
                  <div className="flex items-center gap-3 flex-wrap">
                    {/* Toggle Simples / Análise / Curva ABC */}
                    <div className="flex rounded-lg overflow-hidden border border-surface-600 text-xs font-semibold">
                      <button
                        onClick={() => setTableView('simples')}
                        className={`px-3 py-1.5 transition-colors ${tableView === 'simples' ? 'bg-brand-600/30 text-brand-300' : 'text-gray-400 hover:text-gray-200'}`}
                      >Simples</button>
                      <button
                        onClick={() => setTableView('analise')}
                        className={`px-3 py-1.5 transition-colors border-l border-surface-600 ${tableView === 'analise' ? 'bg-brand-500/20 text-brand-300' : 'text-gray-400 hover:text-gray-200'}`}
                      >Análise</button>
                      {site.enabledModules.includes('estoque') && (
                        <button
                          onClick={() => setTableView('abc')}
                          className={`px-3 py-1.5 transition-colors border-l border-surface-600 flex items-center gap-1 ${tableView === 'abc' ? 'bg-emerald-500/20 text-emerald-300' : 'text-gray-400 hover:text-gray-200'}`}
                        >
                          <span className="text-emerald-400 font-black text-[10px]">ABC</span>
                          Curva
                        </button>
                      )}
                    </div>
                  </div>
                </div>

                {/* Filtro por origem */}
                <div className="flex flex-wrap gap-2 items-center">
                  <div className="flex rounded-lg overflow-hidden border border-surface-600 text-xs font-semibold">
                    {(['Todos', 'Comanda', 'PDV'] as const).map(o => (
                      <button
                        key={o}
                        onClick={() => setTopOrigemFilter(o)}
                        className={`flex items-center gap-1.5 px-3 py-1.5 transition-colors ${o !== 'Todos' ? 'border-l border-surface-600' : ''} ${
                          topOrigemFilter === o ? 'bg-brand-500/20 text-brand-300' : 'text-gray-400 hover:text-gray-200'
                        }`}
                      >
                        {o === 'Comanda' && <ShoppingCart className="w-3 h-3" />}
                        {o === 'PDV'     && <Store        className="w-3 h-3" />}
                        {o}
                      </button>
                    ))}
                  </div>

                  {/* Chips de categoria */}
                  {topCats.length > 0 && (
                    <div className="flex flex-wrap gap-1">
                      <button
                        onClick={() => setTopCatFilter(null)}
                        className={`px-2.5 py-1 rounded-lg text-xs font-medium border transition-all ${
                          topCatFilter === null
                            ? 'bg-surface-600 border-surface-400 text-white'
                            : 'bg-surface-700 border-surface-600 text-gray-400 hover:border-surface-500'
                        }`}
                      >Todas</button>
                      {topCats.map(cat => (
                        <button
                          key={cat}
                          onClick={() => setTopCatFilter(cat === topCatFilter ? null : cat)}
                          className={`px-2.5 py-1 rounded-lg text-xs font-medium border transition-all ${
                            topCatFilter === cat
                              ? 'bg-brand-600/30 border-brand-500 text-brand-200'
                              : 'bg-surface-700 border-surface-600 text-gray-400 hover:border-surface-500'
                          }`}
                        >{cat}</button>
                      ))}
                    </div>
                  )}

                  <span className="text-[11px] text-gray-500 ml-auto">
                    {topFiltered.length} produto{topFiltered.length !== 1 ? 's' : ''}
                    {(topOrigemFilter !== 'Todos' || topCatFilter) && ` filtrado${topFiltered.length !== 1 ? 's' : ''}`}
                  </span>
                </div>
              </div>

              <div className={tableView === 'abc' ? 'p-4' : 'overflow-x-auto'}>
                {tableView === 'abc' ? (
                  <CurvaABCSection produtos={topFiltered} targetPct={targetPct} />
                ) : tableView === 'simples' ? (
                  <table className="w-full text-sm">
                    <thead className="bg-surface-800">
                      <tr className="text-left">
                        {['#', 'Produto', 'Categoria', 'Qtd', 'Comanda', 'PDV', 'Receita'].map(h => (
                          <th key={h} className="px-4 py-2.5 text-xs text-gray-500 uppercase tracking-wider font-semibold whitespace-nowrap">{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-surface-500">
                      {topFiltered.map((p, i) => {
                        const qtdShow     = topOrigemFilter === 'Comanda' ? p.qtdComandas : topOrigemFilter === 'PDV' ? p.qtdAvulsa : p.qtd
                        const receitaShow = topOrigemFilter === 'Comanda' ? p.receitaComandas : topOrigemFilter === 'PDV' ? p.receitaAvulsa : p.receita
                        return (
                          <tr key={p.nome} className="hover:bg-surface-500/20 transition-colors">
                            <td className="px-4 py-2.5 text-gray-400 text-xs font-mono">{i + 1}</td>
                            <td className="px-4 py-2.5 font-medium text-white max-w-[200px]">
                              <p className="truncate">{p.nome}</p>
                            </td>
                            <td className="px-4 py-2.5">
                              <span className="text-[10px] bg-surface-600 text-gray-300 px-2 py-0.5 rounded-full whitespace-nowrap">{p.categoria || '—'}</span>
                            </td>
                            <td className="px-4 py-2.5 text-gray-300 text-xs font-mono font-semibold">{qtdShow}x</td>
                            <td className="px-4 py-2.5 text-brand-400 text-xs font-mono">
                              {p.qtdComandas > 0 ? `${p.qtdComandas}x` : <span className="text-gray-600">—</span>}
                            </td>
                            <td className="px-4 py-2.5 text-amber-400 text-xs font-mono">
                              {p.qtdAvulsa > 0 ? `${p.qtdAvulsa}x` : <span className="text-gray-600">—</span>}
                            </td>
                            <td className="px-4 py-2.5 font-mono text-emerald-400 font-bold text-sm">
                              {fmt(receitaShow)}
                              {topOrigemFilter === 'Todos' && p.receitaComandas > 0 && p.receitaAvulsa > 0 && (
                                <p className="text-[10px] text-gray-500 font-normal">
                                  <span className="text-brand-400/70">{fmt(p.receitaComandas)}</span> · <span className="text-amber-400/70">{fmt(p.receitaAvulsa)}</span>
                                </p>
                              )}
                            </td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                ) : (
                  <table className="w-full text-sm">
                    <thead className="bg-surface-800">
                      <tr className="text-left">
                        {['#', 'Produto', 'Qtd', 'Preço Médio', 'Custo Médio', 'Margem Atual', 'Sugestão', 'Ação'].map(h => (
                          <th key={h} className="px-4 py-2.5 text-xs text-gray-500 uppercase tracking-wider font-semibold whitespace-nowrap">{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-surface-500">
                      {topFiltered.map((p, i) => {
                        const qtdShow    = topOrigemFilter === 'Comanda' ? p.qtdComandas : topOrigemFilter === 'PDV' ? p.qtdAvulsa : p.qtd
                        const recShow    = topOrigemFilter === 'Comanda' ? p.receitaComandas : topOrigemFilter === 'PDV' ? p.receitaAvulsa : p.receita
                        const precoMedio  = qtdShow > 0 ? recShow / qtdShow : 0
                        const custoMedio  = p.qtd > 0 ? p.custo / p.qtd : 0
                        const margemAtual = precoMedio > 0 && custoMedio > 0
                          ? ((precoMedio - custoMedio) / precoMedio) * 100
                          : null
                        const precoSugerido = custoMedio > 0 ? custoMedio / (1 - targetPct / 100) : null
                        const diff = precoSugerido !== null ? precoSugerido - precoMedio : null
                        return (
                          <tr key={p.nome} className="hover:bg-surface-700 transition-colors">
                            <td className="px-4 py-2.5 text-gray-400 text-xs font-mono">{i + 1}</td>
                            <td className="px-4 py-2.5 font-medium text-white max-w-[180px]">
                              <p className="truncate">{p.nome}</p>
                              <div className="flex items-center gap-2 mt-0.5">
                                <span className="text-[10px] bg-surface-600 text-gray-400 px-1.5 py-0 rounded">{p.categoria || '—'}</span>
                                {p.qtdComandas > 0 && <span className="text-[10px] text-brand-400/70">{p.qtdComandas}x cmd</span>}
                                {p.qtdAvulsa > 0   && <span className="text-[10px] text-amber-400/70">{p.qtdAvulsa}x pdv</span>}
                              </div>
                            </td>
                            <td className="px-4 py-2.5 text-gray-400 text-xs font-mono">{qtdShow}x</td>
                            <td className="px-4 py-2.5 font-mono text-gray-200 font-semibold text-xs">
                              {precoMedio > 0 ? fmt(precoMedio) : '—'}
                            </td>
                            <td className="px-4 py-2.5 font-mono text-red-400 text-xs">
                              {custoMedio > 0 ? fmt(custoMedio) : <span className="text-gray-600">—</span>}
                            </td>
                            <td className="px-4 py-2.5">
                              {margemAtual !== null ? (
                                <div className="flex items-center gap-2">
                                  <div className="w-12 h-1.5 bg-surface-600 rounded-full overflow-hidden">
                                    <div className={`h-full rounded-full ${margemAtual >= targetPct ? 'bg-emerald-500' : margemAtual >= 0 ? 'bg-purple-500' : 'bg-red-500'}`}
                                      style={{ width: `${Math.min(100, Math.abs(margemAtual))}%` }} />
                                  </div>
                                  <span className={`text-xs font-mono font-bold ${margemAtual >= targetPct ? 'text-emerald-400' : margemAtual >= 0 ? 'text-purple-400' : 'text-red-400'}`}>
                                    {margemAtual.toFixed(0)}%
                                  </span>
                                </div>
                              ) : <span className="text-gray-600 text-xs">—</span>}
                            </td>
                            <td className="px-4 py-2.5 font-mono text-xs">
                              {precoSugerido !== null
                                ? <span className="text-brand-400 font-semibold">{fmt(precoSugerido)}</span>
                                : <span className="text-gray-600">—</span>}
                            </td>
                            <td className="px-4 py-2.5">
                              {diff !== null ? (
                                Math.abs(diff) < 0.50 ? (
                                  <span className="flex items-center gap-1 text-xs text-emerald-400"><Minus className="w-3 h-3" /> Ok</span>
                                ) : diff > 0 ? (
                                  <span className="flex items-center gap-1 text-xs text-red-400 font-semibold"><ArrowUp className="w-3 h-3" /> +{fmt(diff)}</span>
                                ) : (
                                  <span className="flex items-center gap-1 text-xs text-emerald-400"><ArrowDown className="w-3 h-3" /> {fmt(diff)}</span>
                                )
                              ) : <span className="text-gray-600 text-xs">—</span>}
                            </td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                )}
                {/* Legenda */}
                <div className="px-4 py-3 border-t border-surface-600 flex flex-wrap gap-4 text-[11px] text-gray-500">
                  <span><span className="text-brand-400 font-bold">Comanda</span> = mesas · <span className="text-amber-400 font-bold">PDV</span> = balcão avulso</span>
                  {tableView === 'analise' && <>
                    <span><span className="text-brand-400 font-bold">Sugestão</span> = Custo Médio ÷ (1 − {targetPct}%)</span>
                    <span><ArrowUp className="w-3 h-3 text-red-400 inline" /> subir preço · <ArrowDown className="w-3 h-3 text-emerald-400 inline" /> pode baixar · <Minus className="w-3 h-3 text-emerald-400 inline" /> preço ok</span>
                  </>}
                </div>
              </div>
            </div>
          )}

          {/* Aviso sem custo */}
          {d.custo === 0 && d.receita > 0 && (
            <div className="flex items-start gap-3 rounded-xl bg-yellow-500/10 border border-yellow-500/20 p-4 text-sm text-yellow-300">
              <AlertCircle className="w-5 h-5 shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold">Preço de custo não cadastrado</p>
                <p className="text-yellow-400/70 mt-0.5">Cadastre o <strong>Preço de custo</strong> nos produtos em <a href="/admin/estoque" className="underline">Estoque</a> para ver a margem real.</p>
              </div>
            </div>
          )}

          {d.receita === 0 && (
            <div className="flex items-center gap-3 rounded-xl bg-surface-700 border border-surface-500 p-6 text-sm text-gray-400">
              <DollarSign className="w-5 h-5 text-gray-400" />
              Nenhuma venda registrada no período selecionado.
            </div>
          )}
        </>
      ) : null}
    </div>
  )
}
