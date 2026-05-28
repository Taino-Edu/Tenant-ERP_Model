'use client'
import { useEffect, useState, useCallback } from 'react'
import {
  relatorioApi, RelatorioVendasDto, RelatorioCategoria,
  RelatorioCrediarioDto, DevedorDto, PagamentoMesDto,
} from '@/lib/api'
import toast from 'react-hot-toast'
import {
  BarChart2, ChevronDown, ChevronUp, Loader2, Package,
  TrendingUp, ShoppingCart, CreditCard, AlertTriangle,
  CheckCircle, DollarSign, Phone,
} from 'lucide-react'
import clsx from 'clsx'

const fmt     = (n: number) => `R$ ${n.toFixed(2).replace('.', ',')}`
const fmtDate = (d: string) => new Date(d).toLocaleDateString('pt-BR')
const fmtHour = (d: string) => new Date(d).toLocaleString('pt-BR', {
  day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
})
const MESES = ['Janeiro','Fevereiro','Março','Abril','Maio','Junho',
               'Julho','Agosto','Setembro','Outubro','Novembro','Dezembro']

// ── Aba Vendas ────────────────────────────────────────────────────────────────

function CategoriaCard({ cat, totalGeral }: { cat: RelatorioCategoria; totalGeral: number }) {
  const [open, setOpen] = useState(false)
  const pct = totalGeral > 0 ? (cat.quantidadeVendida / totalGeral) * 100 : 0

  return (
    <div className="card">
      <button onClick={() => setOpen(v => !v)} className="w-full flex items-center gap-3 text-left">
        <span className="text-2xl w-8 text-center shrink-0">{cat.emoji}</span>
        <div className="flex-1 min-w-0">
          <div className="flex items-center justify-between gap-2">
            <p className="font-semibold text-white truncate">{cat.categoria}</p>
            <span className="text-accent-gold font-bold text-sm shrink-0">{fmt(cat.totalEmReais)}</span>
          </div>
          <div className="mt-1.5 h-1.5 bg-surface-600 rounded-full overflow-hidden">
            <div className="h-full bg-brand-500 rounded-full transition-all duration-500" style={{ width: `${pct}%` }} />
          </div>
          <div className="flex items-center justify-between mt-1 text-xs text-gray-500">
            <span>{cat.quantidadeVendida} un. vendidas</span>
            <span>{pct.toFixed(1)}% do total</span>
          </div>
        </div>
        {open ? <ChevronUp className="w-4 h-4 text-gray-500 shrink-0" /> : <ChevronDown className="w-4 h-4 text-gray-500 shrink-0" />}
      </button>

      {open && (
        <div className="mt-3 border-t border-surface-600 pt-3 space-y-1">
          {cat.produtos.map((p, i) => (
            <div key={i} className="flex items-center justify-between bg-surface-700 rounded-lg px-3 py-2 text-sm">
              <div className="flex items-center gap-2 min-w-0">
                <span className="text-gray-500 text-xs w-4 shrink-0">{i + 1}.</span>
                <span className="text-white truncate">{p.nome}</span>
              </div>
              <div className="flex items-center gap-4 shrink-0 ml-2">
                <span className="text-gray-400 text-xs">{p.quantidadeVendida} un.</span>
                <span className="text-accent-gold font-mono font-semibold">{fmt(p.totalEmReais)}</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function AbaVendas({ mes, ano }: { mes: number; ano: number }) {
  const [data, setData]       = useState<RelatorioVendasDto | null>(null)
  const [loading, setLoading] = useState(false)

  const fetch = useCallback(async () => {
    setLoading(true)
    try { const { data: d } = await relatorioApi.vendas(mes, ano); setData(d) }
    catch { toast.error('Erro ao carregar relatório de vendas') }
    finally { setLoading(false) }
  }, [mes, ano])

  useEffect(() => { fetch() }, [fetch])

  if (loading) return <div className="flex justify-center py-24"><Loader2 className="w-8 h-8 animate-spin text-brand-400" /></div>
  if (!data || data.porCategoria.length === 0)
    return (
      <div className="flex flex-col items-center justify-center py-24 text-center">
        <BarChart2 className="w-12 h-12 text-gray-600 mb-3" />
        <p className="text-gray-400 font-medium">Nenhuma venda em {MESES[mes - 1]} {ano}</p>
      </div>
    )

  return (
    <div className="space-y-4">
      {/* KPIs */}
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {[
          { label: 'Total vendido',     value: fmt(data.totalGeralEmReais),      icon: TrendingUp,  color: 'text-accent-gold',  bg: 'bg-amber-500/10' },
          { label: 'Itens vendidos',    value: `${data.totalItensVendidos} un.`, icon: ShoppingCart, color: 'text-brand-400',   bg: 'bg-brand-500/10' },
          { label: 'Categorias',        value: `${data.porCategoria.length}`,    icon: Package,      color: 'text-accent-green', bg: 'bg-accent-green/10' },
        ].map(m => (
          <div key={m.label} className="card flex items-center gap-3 py-3">
            <div className={clsx('w-10 h-10 rounded-xl flex items-center justify-center shrink-0', m.bg)}>
              <m.icon className={clsx('w-5 h-5', m.color)} />
            </div>
            <div className="min-w-0">
              <p className="text-lg font-bold text-white truncate">{m.value}</p>
              <p className="text-xs text-gray-400">{m.label}</p>
            </div>
          </div>
        ))}
      </div>

      <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">
        {MESES[mes - 1]} {ano} — clique para ver os produtos
      </p>
      {data.porCategoria.map((cat, i) => (
        <CategoriaCard key={i} cat={cat} totalGeral={data.totalItensVendidos} />
      ))}
    </div>
  )
}

// ── Aba Crediário ─────────────────────────────────────────────────────────────

function DevedorRow({ d }: { d: DevedorDto }) {
  return (
    <div className={clsx('card py-3', d.vencido && 'border-red-500/30')}>
      <div className="flex items-center gap-3">
        <div className={clsx('w-8 h-8 rounded-lg flex items-center justify-center shrink-0',
          d.vencido ? 'bg-red-500/10' : 'bg-amber-500/10')}>
          {d.vencido
            ? <AlertTriangle className="w-4 h-4 text-red-400" />
            : <CreditCard className="w-4 h-4 text-amber-400" />
          }
        </div>
        <div className="flex-1 min-w-0">
          <p className="font-semibold text-white text-sm truncate">{d.nome}</p>
          <div className="flex flex-wrap items-center gap-x-3 gap-y-0.5 mt-0.5">
            {d.vencido
              ? <span className="text-xs text-red-400">{d.diasAtraso} dias em atraso</span>
              : <span className="text-xs text-gray-500">Vence {fmtDate(d.dataVencimento)}</span>
            }
            {d.whatsApp && (
              <a href={`https://wa.me/55${d.whatsApp.replace(/\D/g,'')}`}
                 target="_blank" rel="noopener noreferrer"
                 className="flex items-center gap-1 text-xs text-emerald-400 hover:text-emerald-300 transition-colors">
                <Phone className="w-3 h-3" /> WhatsApp
              </a>
            )}
          </div>
        </div>
        <span className={clsx('font-bold text-lg shrink-0', d.vencido ? 'text-red-400' : 'text-amber-400')}>
          {fmt(d.saldoEmReais)}
        </span>
      </div>
    </div>
  )
}

function AbaCrediario({ mes, ano }: { mes: number; ano: number }) {
  const [data, setData]       = useState<RelatorioCrediarioDto | null>(null)
  const [loading, setLoading] = useState(false)
  const [showPgtos, setShowPgtos] = useState(false)

  const fetch = useCallback(async () => {
    setLoading(true)
    try { const { data: d } = await relatorioApi.crediario(mes, ano); setData(d) }
    catch { toast.error('Erro ao carregar relatório de crediário') }
    finally { setLoading(false) }
  }, [mes, ano])

  useEffect(() => { fetch() }, [fetch])

  if (loading) return <div className="flex justify-center py-24"><Loader2 className="w-8 h-8 animate-spin text-brand-400" /></div>
  if (!data) return null

  return (
    <div className="space-y-4">
      {/* KPIs situação atual */}
      <div className="grid grid-cols-2 gap-3">
        {[
          { label: 'Total em aberto',  value: fmt(data.totalEmAbertoEmReais), icon: CreditCard,     color: 'text-amber-400',    bg: 'bg-amber-500/10',      sub: `${data.qtdAbertos} cliente${data.qtdAbertos !== 1 ? 's' : ''}` },
          { label: 'Vencidos',         value: fmt(data.totalVencidoEmReais),  icon: AlertTriangle,  color: 'text-red-400',      bg: 'bg-red-500/10',        sub: `${data.qtdVencidos} em atraso` },
          { label: `Recebido em ${MESES[mes-1].slice(0,3)}`, value: fmt(data.recebidoNoMesEmReais), icon: CheckCircle, color: 'text-accent-green', bg: 'bg-accent-green/10', sub: `${data.qtdPagamentosNoMes} pagamento${data.qtdPagamentosNoMes !== 1 ? 's' : ''}` },
          { label: 'Saldo líquido',    value: fmt(data.totalEmAbertoEmReais - data.totalVencidoEmReais), icon: DollarSign, color: 'text-brand-400', bg: 'bg-brand-500/10', sub: 'em dia' },
        ].map(m => (
          <div key={m.label} className="card flex items-center gap-3 py-3">
            <div className={clsx('w-10 h-10 rounded-xl flex items-center justify-center shrink-0', m.bg)}>
              <m.icon className={clsx('w-5 h-5', m.color)} />
            </div>
            <div className="min-w-0">
              <p className="text-base font-bold text-white truncate">{m.value}</p>
              <p className="text-[11px] text-gray-400 truncate">{m.label}</p>
              <p className="text-[10px] text-gray-600 truncate">{m.sub}</p>
            </div>
          </div>
        ))}
      </div>

      {/* Devedores */}
      {data.devedores.length === 0 ? (
        <div className="card text-center py-8">
          <CheckCircle className="w-10 h-10 text-accent-green mx-auto mb-2" />
          <p className="text-accent-green font-semibold">Nenhum crediário em aberto</p>
        </div>
      ) : (
        <div className="space-y-2">
          <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">
            Devedores ativos ({data.devedores.length})
          </p>
          {data.devedores.map((d, i) => <DevedorRow key={i} d={d} />)}
        </div>
      )}

      {/* Pagamentos do mês */}
      {data.pagamentosNoMes.length > 0 && (
        <div className="space-y-2">
          <button
            onClick={() => setShowPgtos(v => !v)}
            className="flex items-center gap-2 text-xs text-gray-400 hover:text-white transition-colors uppercase tracking-wider font-semibold"
          >
            {showPgtos ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
            Pagamentos recebidos em {MESES[mes - 1]} ({data.pagamentosNoMes.length})
          </button>
          {showPgtos && (
            <div className="space-y-1.5">
              {data.pagamentosNoMes.map((p, i) => (
                <div key={i} className="flex items-center justify-between bg-surface-700 rounded-xl px-4 py-2.5">
                  <div>
                    <p className="text-sm text-white font-medium">{p.clienteNome}</p>
                    <p className="text-xs text-gray-500">
                      {p.formaPagamento} · {fmtHour(p.createdAt)}
                      {p.observacao && ` · ${p.observacao}`}
                    </p>
                  </div>
                  <span className="text-accent-green font-bold font-mono ml-3 shrink-0">{fmt(p.valorEmReais)}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────

type Aba = 'vendas' | 'crediario'

export default function RelatoriosPage() {
  const hoje = new Date()
  const [mes, setMes] = useState(hoje.getMonth() + 1)
  const [ano, setAno] = useState(hoje.getFullYear())
  const [aba, setAba] = useState<Aba>('vendas')

  const anos = Array.from({ length: 3 }, (_, i) => hoje.getFullYear() - i)

  return (
    <div className="p-4 sm:p-6 space-y-5">
      {/* Header */}
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <BarChart2 className="w-6 h-6 text-brand-400" /> Relatórios
          </h1>
          <p className="text-gray-400 text-sm mt-0.5">Vendas por categoria e situação do crediário</p>
        </div>

        {/* Seletor de período */}
        <div className="flex items-center gap-2 flex-shrink-0">
          <select value={mes} onChange={e => setMes(Number(e.target.value))} className="input py-1.5 text-sm max-w-[130px]">
            {MESES.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
          </select>
          <select value={ano} onChange={e => setAno(Number(e.target.value))} className="input py-1.5 text-sm w-[80px]">
            {anos.map(a => <option key={a} value={a}>{a}</option>)}
          </select>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 bg-surface-800 border border-surface-600 p-1 rounded-xl w-fit">
        {([
          { key: 'vendas',    label: 'Vendas',    icon: ShoppingCart },
          { key: 'crediario', label: 'Crediário', icon: CreditCard },
        ] as { key: Aba; label: string; icon: React.ElementType }[]).map(({ key, label, icon: Icon }) => (
          <button
            key={key}
            onClick={() => setAba(key)}
            className={clsx(
              'flex items-center gap-2 px-4 py-1.5 rounded-lg text-sm font-medium transition-all',
              aba === key ? 'bg-brand-600 text-white' : 'text-gray-400 hover:text-white'
            )}
          >
            <Icon className="w-4 h-4" />
            {label}
          </button>
        ))}
      </div>

      {/* Conteúdo da aba */}
      {aba === 'vendas'    && <AbaVendas    mes={mes} ano={ano} />}
      {aba === 'crediario' && <AbaCrediario mes={mes} ano={ano} />}
    </div>
  )
}
