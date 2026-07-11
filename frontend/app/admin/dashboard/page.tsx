'use client'
import { useEffect, useState, useMemo } from 'react'
import { productApi, analyticsApi, lgpdAdminApi, waitListApi, Product, FinanceiroDto, ClienteInsightDto, LgpdRequestDto, DashChartScheme } from '@/lib/api'
import { usePreferences } from '@/hooks/usePreferences'
import PageHeader from '@/components/admin/PageHeader'
import StatCard from '@/components/admin/StatCard'
import Link from 'next/link'
import {
  TrendingUp, ChevronDown, ChevronUp, AlertTriangle, DollarSign, BarChart2,
  Trophy, Medal, Star, Package, Shield, LayoutDashboard, ArrowRight,
} from 'lucide-react'
import clsx from 'clsx'

const fmt = (n: number) => `R$ ${n.toFixed(2).replace('.', ',')}`

/** Data de hoje no fuso de Brasília como YYYY-MM-DD (nunca usa UTC). */
const brToday = () => new Intl.DateTimeFormat('fr-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date())

// ── Mini gráfico de barras (últimos 7 dias) ───────────────────────────────────
const CHART_SCHEMES: Record<DashChartScheme, { melhor: string; acima: string; normal: string; abaixo: string; hoje: string }> = {
  default: { melhor: 'bg-accent-gold', acima: 'bg-accent-green', normal: 'bg-brand-600', abaixo: 'bg-red-400',    hoje: 'bg-brand-400'   },
  blue:    { melhor: 'bg-cyan-300',    acima: 'bg-brand-400',    normal: 'bg-brand-600', abaixo: 'bg-blue-900',   hoje: 'bg-cyan-400'    },
  neon:    { melhor: 'bg-violet-400',  acima: 'bg-emerald-400',  normal: 'bg-fuchsia-500', abaixo: 'bg-orange-500', hoje: 'bg-violet-300' },
}

function MiniBarChart({ dias, open, onToggle, scheme }: { dias: FinanceiroDto['diaDia']; open: boolean; onToggle: () => void; scheme: DashChartScheme }) {
  const [hovered, setHovered] = useState<number | null>(null)
  if (!dias || dias.length === 0) return null

  const colors  = CHART_SCHEMES[scheme] ?? CHART_SCHEMES.default
  const maxVal  = Math.max(...dias.map(d => d.receita), 1)
  const avgVal  = dias.reduce((s, d) => s + d.receita, 0) / dias.length
  const lastIdx = dias.length - 1
  const BAR_H   = 60

  function barClass(receita: number, i: number) {
    const isToday = i === lastIdx
    const isMax   = receita === maxVal && receita > 0
    const ratio   = avgVal > 0 ? receita / avgVal : 1
    if (isToday && isMax) return colors.melhor
    if (isToday)          return colors.hoje
    if (isMax)            return colors.melhor
    if (ratio >= 1.15)    return colors.acima
    if (ratio <= 0.6)     return colors.abaixo
    return colors.normal
  }

  return (
    <div className="card">
      <button onClick={onToggle} className="w-full flex items-center justify-between">
        <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
          <BarChart2 className="w-4 h-4 text-brand-400" /> Receita — últimos 7 dias
        </h3>
        <div className="flex items-center gap-3">
          <a href="/admin/financeiro" onClick={e => e.stopPropagation()} className="text-xs text-brand-400 hover:text-brand-300 transition-colors">
            Ver relatório →
          </a>
          {open ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
        </div>
      </button>

      {open && <>
        <div className="flex items-end gap-1.5 mt-3" style={{ height: `${BAR_H}px` }}>
          {dias.map((d, i) => {
            const barH    = Math.max(3, Math.round((d.receita / maxVal) * BAR_H))
            const isHov   = hovered === i
            const isToday = i === lastIdx
            return (
              <div
                key={d.dia}
                className="flex-1 relative cursor-pointer"
                style={{ height: `${barH}px` }}
                onMouseEnter={() => setHovered(i)}
                onMouseLeave={() => setHovered(null)}
              >
                {isHov && (
                  <div className="absolute bottom-full mb-1 left-1/2 -translate-x-1/2 bg-surface-700 border border-surface-500 rounded px-2 py-1 text-xs text-white whitespace-nowrap z-10 pointer-events-none">
                    {isToday ? 'Hoje' : d.dia.slice(5).replace('-', '/')}: {fmt(d.receita)}
                  </div>
                )}
                <div className={`w-full h-full rounded-t transition-all duration-150 ${barClass(d.receita, i)} ${isHov ? 'opacity-70' : ''}`} />
              </div>
            )
          })}
        </div>
        <div className="flex gap-1.5 mt-1">
          {dias.map((d, i) => (
            <span key={d.dia} className={`flex-1 text-center text-[9px] leading-none ${i === lastIdx ? 'text-brand-400 font-semibold' : 'text-gray-500'}`}>
              {i === lastIdx ? 'hoje' : d.dia.slice(8)}
            </span>
          ))}
        </div>
      </>}
    </div>
  )
}

function usePersistentPanel(key: string, defaultOpen = true): [boolean, () => void] {
  const [open, setOpen] = useState(() => {
    if (typeof window === 'undefined') return defaultOpen
    try {
      const v = localStorage.getItem(`dash_panel_${key}`)
      return v === null ? defaultOpen : v === 'true'
    } catch { return defaultOpen }
  })
  const toggle = () => setOpen(v => {
    const next = !v
    try { localStorage.setItem(`dash_panel_${key}`, String(next)) } catch {}
    return next
  })
  return [open, toggle]
}

// ── Página principal ──────────────────────────────────────────────────────────

export default function DashboardPage() {
  const { prefs } = usePreferences()
  const dp = prefs.dashboard
  const [fin7d, setFin7d]         = useState<FinanceiroDto | null>(null)
  const [finHoje, setFinHoje]     = useState<FinanceiroDto | null>(null)
  const [lowStock, setLowStock]   = useState(0)
  const [allProducts, setAllProducts] = useState<Product[]>([])
  const [ranking, setRanking]     = useState<ClienteInsightDto[]>([])
  const [finOpen, setFinOpen] = useState(false)
  const [panelGrafico,      togglePanelGrafico]      = usePersistentPanel('grafico')
  const [panelPrevisao,     togglePanelPrevisao]     = usePersistentPanel('previsao')
  const [panelPatrimonio,   togglePanelPatrimonio]   = usePersistentPanel('patrimonio')
  const [panelClientes,     togglePanelClientes]     = usePersistentPanel('clientes')
  const [panelProdutos,     togglePanelProdutos]     = usePersistentPanel('produtos')
  const [panelLgpd,         togglePanelLgpd]         = usePersistentPanel('lgpd')
  const [panelPreVenda,     togglePanelPreVenda]     = usePersistentPanel('prevenda')
  const [pendingPreVenda, setPendingPreVenda] = useState(0)
  const [pendingLgpd, setPendingLgpd]   = useState<LgpdRequestDto[]>([])
  const [finProdutos, setFinProdutos]   = useState<FinanceiroDto | null>(null)
  const [prodDe,  setProdDe]  = useState(() => {
    const d = new Date()
    d.setDate(d.getDate() - 6)
    return new Intl.DateTimeFormat('fr-CA', { timeZone: 'America/Sao_Paulo' }).format(d)
  })
  const [prodAte, setProdAte] = useState(brToday)

  // Carrega dados financeiros e estoque baixo
  useEffect(() => {
    const hoje  = brToday()
    const ini7d = new Date(); ini7d.setDate(ini7d.getDate() - 6)
    const ini7s = new Intl.DateTimeFormat('fr-CA', { timeZone: 'America/Sao_Paulo' }).format(ini7d)

    analyticsApi.financeiro(hoje, hoje).then(r => setFinHoje(r.data)).catch(() => {})
    analyticsApi.financeiro(ini7s, hoje).then(r => { setFin7d(r.data); setFinProdutos(r.data) }).catch(() => {})
    productApi.listAdmin().then(r => {
      const prods = r.data.filter(p => p.isActive)
      setLowStock(prods.filter(p => p.isLowStock).length)
      setAllProducts(prods)
    }).catch(() => {})
    analyticsApi.clientes().then(r => setRanking(r.data.filter(c => c.gastoTotal > 0).slice(0, 5))).catch(() => {})
    lgpdAdminApi.listRequests('Pendente').then(r => setPendingLgpd(r.data)).catch(() => {})
    waitListApi.preVendaPendentesCount().then(r => setPendingPreVenda(r.data.count)).catch(() => {})
  }, [])

  async function fetchProdutos(de: string, ate: string) {
    try {
      const { data } = de || ate
        ? await analyticsApi.financeiro(de || undefined, ate || undefined)
        : await analyticsApi.financeiro()
      setFinProdutos(data)
    } catch {}
  }

  const { patrimonioCusto, patrimonioVenda, lucroEstoque, totalPecas } = useMemo(() => {
    const custo  = allProducts.reduce((s, p) => s + p.costPriceInCents  * p.stockQuantity, 0) / 100
    const venda  = allProducts.reduce((s, p) => s + p.priceInCents      * p.stockQuantity, 0) / 100
    const lucro  = venda - custo
    const pecas  = allProducts.reduce((s, p) => s + p.stockQuantity, 0)
    return { patrimonioCusto: custo, patrimonioVenda: venda, lucroEstoque: lucro, totalPecas: pecas }
  }, [allProducts])

  const prevFin = fin7d && fin7d.diaDia.length > 0 ? (() => {
    const hojeStr = brToday()
    const diaAtual = parseInt(hojeStr.slice(8))
    const now = new Date()
    const daysInMonth = new Date(now.getFullYear(), now.getMonth() + 1, 0).getDate()
    const diasRestantes = daysInMonth - diaAtual
    const n = fin7d.diaDia.length
    const mediaDiaria = fin7d.receita / n
    const projecaoMes = mediaDiaria * daysInMonth
    const margemPct = fin7d.receita > 0 ? fin7d.margem / fin7d.receita : 0
    const projecaoMargem = projecaoMes * margemPct
    const realizadoEstimado = mediaDiaria * diaAtual
    const percentual = Math.min(realizadoEstimado / projecaoMes, 1)
    return { mediaDiaria, projecaoMes, projecaoMargem, diasRestantes, daysInMonth, percentual, diaAtual, realizadoEstimado, n }
  })() : null

  return (
    <div className="p-4 sm:p-6 space-y-4 sm:space-y-5">
      <PageHeader
        icon={LayoutDashboard}
        title="Painel Geral"
        description="Visão analítica do negócio"
        actions={
          <Link href="/admin/comanda" className="btn-primary text-sm py-1.5">
            <span>Ir para Comanda</span> <ArrowRight className="w-4 h-4" />
          </Link>
        }
      />

      {/* KPIs */}
      <div className="grid grid-cols-2 gap-3 sm:gap-4">
        <StatCard icon={DollarSign} label="Receita hoje" value={finHoje ? fmt(finHoje.receita) : '—'} tone="brand" />
        <StatCard icon={AlertTriangle} label="Estoque baixo" value={lowStock} tone={lowStock > 0 ? 'danger' : 'neutral'} />
      </div>

      <div className="space-y-6">

        {/* ── Hoje ── */}
        {dp.panels.finHoje && finHoje && <p className="text-[10px] font-semibold text-gray-600 uppercase tracking-widest -mb-3">Hoje</p>}
        {dp.panels.finHoje && finHoje && (
          <div className="card">
            <button onClick={() => setFinOpen(v => !v)} className="w-full flex items-center justify-between">
              <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
                <BarChart2 className="w-4 h-4 text-brand-400" /> Detalhe financeiro hoje
              </h3>
              <div className="flex items-center gap-3">
                <span className={clsx('text-sm font-bold', finHoje.margem >= 0 ? 'text-accent-green' : 'text-red-400')}>
                  Margem {fmt(finHoje.margem)}
                </span>
                {finOpen ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
              </div>
            </button>
            {finOpen && (() => {
              const totalTx = (finHoje.pagamentosPorForma ?? []).reduce((s, f) => s + f.quantidade, 0)
              const ticketMedio = totalTx > 0 ? finHoje.receita / totalTx : 0
              return (
                <div className="mt-4 pt-4 border-t border-surface-600 animate-fade-in space-y-4">
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                    {[
                      { label: 'Receita total',  value: fmt(finHoje.receita),  color: 'text-accent-gold',  sub: finHoje.receitaComandas > 0 ? 'cmd + avulsa' : undefined },
                      { label: 'CMV / Custo',    value: fmt(finHoje.custo),    color: 'text-red-400',      sub: undefined },
                      { label: 'Margem bruta',   value: fmt(finHoje.margem),   color: finHoje.margem >= 0 ? 'text-accent-green' : 'text-red-400', sub: finHoje.receita > 0 ? `${((finHoje.margem / finHoje.receita) * 100).toFixed(1)}%` : undefined },
                      { label: 'Ticket médio',   value: fmt(ticketMedio),      color: 'text-brand-400',    sub: totalTx > 0 ? `${totalTx} transações` : undefined },
                    ].map(m => (
                      <div key={m.label} className="bg-surface-800 rounded-xl p-3">
                        <p className="text-xs text-gray-500 mb-1">{m.label}</p>
                        <p className={clsx('text-base font-bold font-mono', m.color)}>{m.value}</p>
                        {m.sub && <p className="text-[10px] text-gray-600 mt-0.5">{m.sub}</p>}
                      </div>
                    ))}
                  </div>
                  {finHoje.receitaComandas > 0 && (
                    <div className="grid grid-cols-2 gap-3">
                      <div className="bg-surface-800 rounded-xl p-3">
                        <p className="text-xs text-gray-500 mb-1">Receita comandas</p>
                        <p className="text-base font-bold font-mono text-white">{fmt(finHoje.receitaComandas)}</p>
                      </div>
                      <div className="bg-surface-800 rounded-xl p-3">
                        <p className="text-xs text-gray-500 mb-1">Receita avulsa</p>
                        <p className="text-base font-bold font-mono text-white">{fmt(finHoje.receitaAvulsa)}</p>
                      </div>
                    </div>
                  )}
                  {(finHoje.pagamentosPorForma ?? []).filter(f => f.total > 0).length > 0 && (
                    <div>
                      <p className="text-xs text-gray-600 uppercase tracking-wider mb-2">Formas de pagamento</p>
                      <div className="flex flex-wrap gap-2">
                        {finHoje.pagamentosPorForma.filter(f => f.total > 0).map(f => (
                          <div key={f.forma} className="bg-surface-800 rounded-lg px-3 py-2 text-center">
                            <p className="text-sm font-bold text-white">{fmt(f.total)}</p>
                            <p className="text-[10px] text-gray-500 mt-0.5">{f.forma}</p>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )
            })()}
          </div>
        )}

        {/* ── 7 dias / Mês ── */}
        {(dp.panels.grafico || dp.panels.previsao) && (
          <div>
            <p className="text-[10px] font-semibold text-gray-600 uppercase tracking-widest mb-3">7 dias / Mês</p>
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
              {dp.panels.grafico && fin7d && fin7d.diaDia.length > 1 && (
                <MiniBarChart dias={fin7d.diaDia} open={panelGrafico} onToggle={togglePanelGrafico} scheme={dp.chartScheme} />
              )}
              {dp.panels.previsao && prevFin && (
                <div className="card">
                  <button onClick={togglePanelPrevisao} className="w-full flex items-center justify-between">
                    <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
                      <TrendingUp className="w-4 h-4 text-brand-400" /> Previsão financeira —{' '}
                      {new Date().toLocaleString('pt-BR', { month: 'long', timeZone: 'America/Sao_Paulo' })}
                    </h3>
                    <div className="flex items-center gap-3">
                      <span className="text-xs text-gray-500">{prevFin.diasRestantes} dias restantes</span>
                      {panelPrevisao ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
                    </div>
                  </button>
                  {panelPrevisao && (
                    <div className="mt-4 pt-4 border-t border-surface-600">
                      <div className="flex flex-wrap gap-6 items-end mb-4">
                        <div>
                          <p className="text-2xl font-bold text-accent-gold">{fmt(prevFin.projecaoMes)}</p>
                          <p className="text-xs text-gray-500 mt-0.5">projeção de receita</p>
                        </div>
                        <div>
                          <p className={clsx('text-sm font-semibold', prevFin.projecaoMargem >= 0 ? 'text-accent-green' : 'text-red-400')}>{fmt(prevFin.projecaoMargem)}</p>
                          <p className="text-xs text-gray-500 mt-0.5">projeção margem</p>
                        </div>
                        <div>
                          <p className="text-sm font-semibold text-brand-400">{fmt(prevFin.mediaDiaria)}<span className="text-xs font-normal text-gray-500">/dia</span></p>
                          <p className="text-xs text-gray-500 mt-0.5">média últimos {prevFin.n}d</p>
                        </div>
                      </div>
                      <div className="flex justify-between text-xs text-gray-500 mb-1.5">
                        <span>Estimado realizado: {fmt(prevFin.realizadoEstimado)}</span>
                        <span>{Math.round(prevFin.percentual * 100)}% do mês ({prevFin.diaAtual}/{prevFin.daysInMonth})</span>
                      </div>
                      <div className="h-2 rounded-full bg-surface-600 overflow-hidden">
                        <div
                          className="h-full rounded-full bg-gradient-to-r from-brand-600 to-brand-400 transition-all duration-500"
                          style={{ width: `${prevFin.percentual * 100}%` }}
                        />
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        )}

        {/* ── Rankings & Alertas ── */}
        {(dp.panels.patrimonio || dp.panels.clientes || dp.panels.lgpd) && (
          <p className="text-[10px] font-semibold text-gray-600 uppercase tracking-widest -mb-3">Rankings & alertas</p>
        )}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-5">

          {dp.panels.patrimonio && allProducts.length > 0 && (
            <div className="card">
              <div className="flex items-center justify-between">
                <button onClick={togglePanelPatrimonio} className="flex items-center gap-2 flex-1 text-left">
                  <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
                    <Package className="w-4 h-4 text-emerald-400" /> Patrimônio
                  </h3>
                  {panelPatrimonio ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
                </button>
                <a href="/admin/estoque" className="text-xs text-brand-400 hover:text-brand-300 transition-colors ml-2">→</a>
              </div>
              {panelPatrimonio && (
                <div className="mt-3 space-y-1.5">
                  {[
                    { label: 'Custo total',  value: fmt(patrimonioCusto),  color: 'text-white' },
                    { label: 'Valor venda',  value: fmt(patrimonioVenda),  color: 'text-accent-gold' },
                    { label: 'Margem',       value: fmt(lucroEstoque),     color: lucroEstoque >= 0 ? 'text-emerald-400' : 'text-red-400' },
                    { label: 'Total peças',  value: totalPecas.toLocaleString('pt-BR'), color: 'text-brand-400' },
                  ].map(r => (
                    <div key={r.label} className="flex justify-between items-center text-xs py-1 border-b border-surface-600 last:border-0">
                      <span className="text-gray-500">{r.label}</span>
                      <span className={clsx('font-mono font-bold', r.color)}>{r.value}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {dp.panels.clientes && (
            <div className="card">
              <div className="flex items-center justify-between">
                <button onClick={togglePanelClientes} className="flex items-center gap-2 flex-1 text-left">
                  <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
                    <Trophy className="w-4 h-4 text-accent-gold" /> Top Clientes
                  </h3>
                  {panelClientes ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
                </button>
                <a href="/admin/usuarios" className="text-xs text-brand-400 hover:text-brand-300 transition-colors ml-3">Ver todos →</a>
              </div>
              {panelClientes && (ranking.length === 0 ? (
                <p className="text-xs text-gray-500 py-4 text-center">Nenhuma compra registrada ainda</p>
              ) : (
                <div className="space-y-2 mt-3">
                  {ranking.map((c, i) => {
                    const medalColor = i === 0 ? 'text-yellow-400' : i === 1 ? 'text-gray-300' : i === 2 ? 'text-amber-600' : 'text-gray-400'
                    const MedalIcon  = i === 0 ? Star : i <= 2 ? Medal : Trophy
                    return (
                      <div key={c.userId} className="flex items-center gap-3 py-2 px-3 rounded-lg bg-surface-800 hover:bg-surface-700 transition-colors">
                        <MedalIcon className={clsx('w-4 h-4 shrink-0', medalColor)} />
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium text-white truncate">{c.nome}</p>
                          <div className="flex flex-wrap items-center gap-1.5 mt-0.5">
                            <span className="text-xs text-gray-500">{c.numVisitas} visita{c.numVisitas !== 1 ? 's' : ''} · {fmt(c.ticketMedio)}/visita</span>
                            {c.inativo30 && (
                              <span className="text-[10px] font-medium px-1.5 py-0.5 rounded-full bg-amber-500/15 text-amber-400 border border-amber-500/20">inativo</span>
                            )}
                            {c.pontosVencemEm !== null && c.pontos > 0 && c.pontosVencemEm <= 14 && (
                              <span className={clsx('text-[10px] font-medium px-1.5 py-0.5 rounded-full border',
                                c.pontosVencemEm < 0 ? 'bg-red-500/15 text-red-400 border-red-500/20' : 'bg-orange-500/15 text-orange-400 border-orange-500/20'
                              )}>
                                {c.pontosVencemEm < 0 ? `${c.pontos}pts vencidos` : `${c.pontos}pts vencem em ${c.pontosVencemEm}d`}
                              </span>
                            )}
                          </div>
                        </div>
                        <div className="flex items-center gap-2 shrink-0">
                          <p className="text-sm font-bold text-accent-gold font-mono">{fmt(c.gastoTotal)}</p>
                          {c.whatsApp && (
                            <a href={`https://wa.me/${c.whatsApp.replace(/\D/g, '')}`} target="_blank" rel="noopener noreferrer"
                              onClick={e => e.stopPropagation()}
                              className="w-7 h-7 flex items-center justify-center rounded-lg bg-emerald-600/20 hover:bg-emerald-600/40 text-emerald-400 transition-colors"
                              title={`WhatsApp: ${c.whatsApp}`}>
                              <svg viewBox="0 0 24 24" className="w-3.5 h-3.5 fill-current">
                                <path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413z"/>
                              </svg>
                            </a>
                          )}
                        </div>
                      </div>
                    )
                  })}
                </div>
              ))}
            </div>
          )}

          {dp.panels.lgpd && (
            <div className="card">
              <button onClick={togglePanelLgpd} className="w-full flex items-center justify-between">
                <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
                  <Shield className="w-4 h-4 text-brand-400" /> LGPD
                </h3>
                {panelLgpd ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
              </button>
              {panelLgpd && (
                <a href="/admin/lgpd" className="mt-3 flex items-center gap-3 p-2.5 rounded-xl bg-surface-800 hover:bg-surface-700 transition-colors">
                  <div className={clsx('w-8 h-8 rounded-lg flex items-center justify-center shrink-0',
                    pendingLgpd.some(r => r.isOverdue) ? 'bg-red-500/15' : pendingLgpd.length > 0 ? 'bg-brand-500/15' : 'bg-surface-600')}>
                    <Shield className={clsx('w-4 h-4',
                      pendingLgpd.some(r => r.isOverdue) ? 'text-red-400' : pendingLgpd.length > 0 ? 'text-brand-400' : 'text-gray-500')} />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-white">Solicitações LGPD</p>
                    <p className="text-xs text-gray-500">
                      {pendingLgpd.some(r => r.isOverdue) ? 'Solicitação vencida!' : 'Pendentes de resposta'}
                    </p>
                  </div>
                  <span className={clsx('text-sm font-bold tabular-nums',
                    pendingLgpd.some(r => r.isOverdue) ? 'text-red-400' : pendingLgpd.length > 0 ? 'text-brand-400' : 'text-gray-600')}>
                    {pendingLgpd.length}
                  </span>
                </a>
              )}
            </div>
          )}
        </div>

        {/* ── Produtos & Eventos ── */}
        {(dp.panels.produtos || dp.panels.preVenda) && (
          <p className="text-[10px] font-semibold text-gray-600 uppercase tracking-widest -mb-3">Produtos & eventos</p>
        )}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-5">

          {dp.panels.produtos && (
            <div className="card">
              <button onClick={togglePanelProdutos} className="w-full flex items-center justify-between">
                <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
                  <Star className="w-4 h-4 text-accent-gold" />
                  Top produtos
                  <span className="text-xs font-normal text-gray-500">
                    {prodDe || prodAte ? `${prodDe || '…'} → ${prodAte || '…'}` : '(todos os tempos)'}
                  </span>
                </h3>
                {panelProdutos ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
              </button>

              {panelProdutos && (
                <div className="mt-3 space-y-3">
                  {/* Seletor de período — calendário De/Até */}
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="flex items-center gap-1.5">
                      <span className="text-xs text-gray-500 shrink-0">De</span>
                      <input
                        type="date"
                        value={prodDe}
                        max={prodAte || brToday()}
                        onChange={e => { setProdDe(e.target.value); fetchProdutos(e.target.value, prodAte) }}
                        className="input text-xs py-1 w-32"
                      />
                    </div>
                    <div className="flex items-center gap-1.5">
                      <span className="text-xs text-gray-500 shrink-0">Até</span>
                      <input
                        type="date"
                        value={prodAte}
                        max={brToday()}
                        min={prodDe || undefined}
                        onChange={e => { setProdAte(e.target.value); fetchProdutos(prodDe, e.target.value) }}
                        className="input text-xs py-1 w-32"
                      />
                    </div>
                    <button
                      onClick={() => { setProdDe(''); setProdAte(''); fetchProdutos('', '') }}
                      className="px-2.5 py-0.5 rounded-lg text-xs font-semibold bg-surface-700 text-gray-500 border border-surface-600 hover:text-gray-300 transition-colors"
                    >
                      Geral
                    </button>
                  </div>

                  {/* Lista */}
                  {finProdutos && finProdutos.topProdutos.length > 0 ? (
                    <div className="space-y-1">
                      {finProdutos.topProdutos.slice(0, 5).map((p, i) => (
                        <div key={p.nome} className="flex items-center gap-2 py-1.5 border-b border-surface-600 last:border-0">
                          <span className="text-xs text-gray-600 w-3.5 shrink-0">{i + 1}</span>
                          <span className="text-sm text-gray-300 flex-1 truncate">{p.nome}</span>
                          <span className="text-xs text-gray-500 shrink-0">{p.qtd}un</span>
                          <span className="text-sm font-bold text-accent-gold shrink-0">{fmt(p.receita)}</span>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="text-xs text-gray-600 text-center py-3">Nenhum produto no período</p>
                  )}
                </div>
              )}
            </div>
          )}

          {dp.panels.preVenda && (
            <div className="card">
              <button onClick={togglePanelPreVenda} className="w-full flex items-center justify-between">
                <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
                  <Package className="w-4 h-4 text-amber-400" /> Pré-venda
                </h3>
                {panelPreVenda ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
              </button>
              {panelPreVenda && (
                <a href="/admin/reservas" className="mt-3 flex items-center gap-3 p-2.5 rounded-xl bg-surface-800 hover:bg-surface-700 transition-colors">
                  <div className={clsx('w-8 h-8 rounded-lg flex items-center justify-center shrink-0',
                    pendingPreVenda > 0 ? 'bg-amber-500/15' : 'bg-surface-600')}>
                    <Package className={clsx('w-4 h-4', pendingPreVenda > 0 ? 'text-amber-400' : 'text-gray-500')} />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-white">Lista de espera</p>
                    <p className="text-xs text-gray-500">Aguardando notificação</p>
                  </div>
                  <span className={clsx('text-sm font-bold tabular-nums', pendingPreVenda > 0 ? 'text-amber-400' : 'text-gray-600')}>
                    {pendingPreVenda}
                  </span>
                </a>
              )}
            </div>
          )}
        </div>

      </div>
    </div>
  )
}
