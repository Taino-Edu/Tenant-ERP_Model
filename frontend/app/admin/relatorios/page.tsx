'use client'
import { useEffect, useState, useCallback } from 'react'
import { relatorioApi, RelatorioVendasDto, RelatorioCategoria } from '@/lib/api'
import toast from 'react-hot-toast'
import { BarChart2, ChevronDown, ChevronUp, Loader2, Package, TrendingUp, ShoppingCart } from 'lucide-react'
import clsx from 'clsx'

const fmt = (n: number) => `R$ ${n.toFixed(2).replace('.', ',')}`
const MESES = ['Janeiro','Fevereiro','Março','Abril','Maio','Junho','Julho','Agosto','Setembro','Outubro','Novembro','Dezembro']

// ── Card de categoria ─────────────────────────────────────────────────────────
function CategoriaCard({ cat, totalGeral }: { cat: RelatorioCategoria; totalGeral: number }) {
  const [open, setOpen] = useState(false)
  const pct = totalGeral > 0 ? (cat.quantidadeVendida / totalGeral) * 100 : 0

  return (
    <div className="card">
      {/* Header da categoria */}
      <button
        onClick={() => setOpen(v => !v)}
        className="w-full flex items-center gap-3 text-left"
      >
        <span className="text-2xl w-8 text-center shrink-0">{cat.emoji}</span>

        <div className="flex-1 min-w-0">
          <div className="flex items-center justify-between gap-2">
            <p className="font-semibold text-white truncate">{cat.categoria}</p>
            <span className="text-accent-gold font-bold text-sm shrink-0">{fmt(cat.totalEmReais)}</span>
          </div>

          {/* Barra de progresso */}
          <div className="mt-1.5 h-1.5 bg-surface-600 rounded-full overflow-hidden">
            <div
              className="h-full bg-brand-500 rounded-full transition-all duration-500"
              style={{ width: `${pct}%` }}
            />
          </div>

          <div className="flex items-center justify-between mt-1 text-xs text-gray-500">
            <span>{cat.quantidadeVendida} un. vendidas</span>
            <span>{pct.toFixed(1)}% do total</span>
          </div>
        </div>

        {open
          ? <ChevronUp className="w-4 h-4 text-gray-500 shrink-0" />
          : <ChevronDown className="w-4 h-4 text-gray-500 shrink-0" />
        }
      </button>

      {/* Tabela de produtos */}
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

// ── Página principal ──────────────────────────────────────────────────────────
export default function RelatoriosPage() {
  const hoje = new Date()
  const [mes, setMes]       = useState(hoje.getMonth() + 1)
  const [ano, setAno]       = useState(hoje.getFullYear())
  const [data, setData]     = useState<RelatorioVendasDto | null>(null)
  const [loading, setLoading] = useState(false)

  const fetchRelatorio = useCallback(async () => {
    setLoading(true)
    try {
      const { data: d } = await relatorioApi.vendas(mes, ano)
      setData(d)
    } catch {
      toast.error('Erro ao carregar relatório')
    } finally {
      setLoading(false)
    }
  }, [mes, ano])

  useEffect(() => { fetchRelatorio() }, [fetchRelatorio])

  const anos = Array.from({ length: 3 }, (_, i) => hoje.getFullYear() - i)

  return (
    <div className="p-4 sm:p-6 space-y-5">
      {/* Header */}
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <BarChart2 className="w-6 h-6 text-brand-400" /> Relatórios de Vendas
          </h1>
          <p className="text-gray-400 text-sm mt-0.5">
            Produtos vendidos por categoria — comandas + frente de caixa
          </p>
        </div>

        {/* Seletor de período */}
        <div className="flex items-center gap-2 flex-shrink-0">
          <select
            value={mes}
            onChange={e => setMes(Number(e.target.value))}
            className="input py-1.5 text-sm max-w-[130px]"
          >
            {MESES.map((m, i) => (
              <option key={i + 1} value={i + 1}>{m}</option>
            ))}
          </select>
          <select
            value={ano}
            onChange={e => setAno(Number(e.target.value))}
            className="input py-1.5 text-sm w-[80px]"
          >
            {anos.map(a => <option key={a} value={a}>{a}</option>)}
          </select>
        </div>
      </div>

      {/* KPIs */}
      {data && !loading && (
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
          {[
            { label: 'Total vendido',      value: fmt(data.totalGeralEmReais),  icon: TrendingUp,  color: 'text-accent-gold',  bg: 'bg-amber-500/10' },
            { label: 'Itens vendidos',     value: `${data.totalItensVendidos} un.`, icon: ShoppingCart, color: 'text-brand-400', bg: 'bg-brand-500/10' },
            { label: 'Categorias ativas',  value: `${data.porCategoria.length}`, icon: Package,     color: 'text-accent-green', bg: 'bg-accent-green/10' },
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
      )}

      {/* Lista de categorias */}
      {loading ? (
        <div className="flex justify-center py-24">
          <Loader2 className="w-8 h-8 animate-spin text-brand-400" />
        </div>
      ) : !data || data.porCategoria.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <div className="w-16 h-16 bg-surface-700 rounded-2xl flex items-center justify-center mb-4">
            <BarChart2 className="w-8 h-8 text-gray-600" />
          </div>
          <p className="text-gray-400 font-medium">Nenhuma venda em {MESES[mes - 1]} {ano}</p>
          <p className="text-gray-600 text-sm mt-1">Selecione outro período ou realize vendas no mês</p>
        </div>
      ) : (
        <div className="space-y-3">
          <p className="text-xs text-gray-500 uppercase tracking-wider font-semibold">
            {MESES[mes - 1]} {ano} — clique para ver os produtos
          </p>
          {data.porCategoria.map((cat, i) => (
            <CategoriaCard
              key={i}
              cat={cat}
              totalGeral={data.totalItensVendidos}
            />
          ))}
        </div>
      )}
    </div>
  )
}
