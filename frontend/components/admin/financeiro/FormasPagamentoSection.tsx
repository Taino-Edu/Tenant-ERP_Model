'use client'
// Seção de recebimentos por forma de pagamento. Extraído de financeiro/page.tsx.
import { useState } from 'react'
import { ChevronDown, ChevronUp, Filter, Receipt, Search, ShoppingCart, Store, X } from 'lucide-react'
import { FormaPagamentoTotalDto } from '@/lib/api'
import { fmt, FORMA_ICONS, FORMA_LABELS } from './financeiro-shared'

export function FormasPagamentoSection({ formas }: { formas: FormaPagamentoTotalDto[] }) {
  const [expanded,    setExpanded]    = useState<string | null>(null)
  const [filterForma, setFilterForma] = useState<string>('Todas')
  const [filterMin,   setFilterMin]   = useState('')
  const [filterMax,   setFilterMax]   = useState('')
  const [searchCliente, setSearch]    = useState('')
  const [showFilters, setShowFilters] = useState(false)

  const minVal = parseFloat(filterMin.replace(',', '.')) || 0
  const maxVal = parseFloat(filterMax.replace(',', '.')) || Infinity

  // Filtra formas de pagamento
  const formasFiltradas = formas.filter(f => {
    if (filterForma !== 'Todas' && f.forma !== filterForma) return false
    if (f.total < minVal) return false
    if (f.total > maxVal) return false
    return true
  })

  // Dentro de cada forma, filtra transações
  function filtrarTransacoes(transacoes: FormaPagamentoTotalDto['transacoes']) {
    return transacoes.filter(t => {
      const cliente = (t.cliente ?? '').toLowerCase()
      if (searchCliente && !cliente.includes(searchCliente.toLowerCase())) return false
      if (t.valor < minVal) return false
      if (t.valor > maxVal) return false
      return true
    })
  }

  const hasFilters = filterForma !== 'Todas' || filterMin || filterMax || searchCliente

  return (
    <div className="card p-0 overflow-hidden">
      {/* Header + toggle filtros */}
      <div className="px-5 py-4 border-b border-surface-500">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Receipt className="w-4 h-4 text-brand-400" />
            <h3 className="text-sm font-semibold text-gray-300">Recebimentos por Forma de Pagamento</h3>
          </div>
          <button
            onClick={() => setShowFilters(v => !v)}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border transition-all ${
              showFilters || hasFilters
                ? 'bg-brand-600/20 border-brand-500/50 text-brand-300'
                : 'bg-surface-700 border-surface-600 text-gray-400 hover:text-gray-200'
            }`}
          >
            <Filter className="w-3.5 h-3.5" />
            Filtrar
            {hasFilters && <span className="w-1.5 h-1.5 rounded-full bg-brand-400" />}
          </button>
        </div>

        {/* Painel de filtros — aparece abaixo do header */}
        {showFilters && (
          <div className="mt-4 space-y-3">
            {/* Chips de método */}
            <div className="flex flex-wrap gap-1.5">
              {['Todas', ...formas.map(f => f.forma)].map(forma => (
                <button
                  key={forma}
                  onClick={() => setFilterForma(forma)}
                  className={`px-2.5 py-1 rounded-lg text-xs font-medium border transition-all ${
                    filterForma === forma
                      ? 'bg-brand-600/30 border-brand-500 text-brand-200'
                      : 'bg-surface-700 border-surface-600 text-gray-400 hover:border-surface-500'
                  }`}
                >
                  {forma === 'Todas' ? 'Todas' : (FORMA_LABELS[forma] ?? forma)}
                </button>
              ))}
            </div>

            {/* Faixa de valor + busca */}
            <div className="flex flex-wrap gap-2 items-center">
              <div className="relative">
                <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-gray-500" />
                <input
                  className="input pl-8 py-1.5 text-xs w-full sm:w-40"
                  placeholder="Buscar cliente..."
                  value={searchCliente}
                  onChange={e => setSearch(e.target.value)}
                />
              </div>
              <input
                className="input py-1.5 text-xs w-28"
                placeholder="Valor mín. R$"
                value={filterMin}
                onChange={e => setFilterMin(e.target.value)}
                type="number" min="0" step="0.01"
              />
              <span className="text-gray-500 text-xs">até</span>
              <input
                className="input py-1.5 text-xs w-28"
                placeholder="Valor máx. R$"
                value={filterMax}
                onChange={e => setFilterMax(e.target.value)}
                type="number" min="0" step="0.01"
              />
              {hasFilters && (
                <button
                  onClick={() => { setFilterForma('Todas'); setFilterMin(''); setFilterMax(''); setSearch('') }}
                  className="text-xs text-gray-500 hover:text-gray-300 transition-colors flex items-center gap-1"
                >
                  <X className="w-3.5 h-3.5" /> Limpar
                </button>
              )}
            </div>
          </div>
        )}
      </div>

      {/* Lista */}
      <div className="divide-y divide-surface-600">
        {formasFiltradas.length === 0 ? (
          <p className="text-center text-gray-500 text-sm py-8">Nenhuma forma de pagamento no filtro.</p>
        ) : (
          formasFiltradas.map(f => {
            const isCard = f.forma === 'CartaoCredito' || f.forma === 'CartaoDebito'
            const isOpen = expanded === f.forma
            const txsFiltradas = filtrarTransacoes(f.transacoes)

            return (
              <div key={f.forma}>
                <button
                  onClick={() => setExpanded(isOpen ? null : f.forma)}
                  className={`w-full flex items-center justify-between px-5 py-3 hover:bg-surface-700 transition-colors text-left ${isCard ? 'bg-purple-500/5' : ''}`}
                >
                  <div className="flex items-center gap-3">
                    {FORMA_ICONS[f.forma] ?? <Receipt className="w-4 h-4 text-gray-500" />}
                    <div>
                      <p className="text-sm font-medium text-white">
                        {FORMA_LABELS[f.forma] ?? f.forma}
                        {isCard && <span className="ml-2 text-[10px] bg-purple-500/20 text-purple-300 px-1.5 py-0.5 rounded font-semibold">NF</span>}
                      </p>
                      <p className="text-xs text-gray-500">{f.quantidade} transação{f.quantidade !== 1 ? 'ões' : ''}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <p className={`text-base font-bold font-mono ${isCard ? 'text-purple-300' : 'text-white'}`}>
                      {fmt(f.total)}
                    </p>
                    {isOpen ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
                  </div>
                </button>

                {isOpen && (
                  <div className="bg-surface-800 border-t border-surface-600 divide-y divide-surface-700">
                    {txsFiltradas.length === 0 ? (
                      <p className="text-center text-gray-500 text-xs py-4">Nenhuma transação no filtro.</p>
                    ) : (
                      txsFiltradas.map((t, i) => (
                        <div key={i} className="flex items-center justify-between px-6 py-2.5">
                          <div className="flex items-center gap-2">
                            {t.origem === 'Comanda'
                              ? <ShoppingCart className="w-3.5 h-3.5 text-brand-400 shrink-0" />
                              : <Store        className="w-3.5 h-3.5 text-amber-400 shrink-0" />
                            }
                            <div>
                              <p className="text-xs text-white font-medium">
                                {t.cliente ?? (t.origem === 'Comanda' ? 'Comanda' : 'Balcão')}
                              </p>
                              <p className="text-[10px] text-gray-500">
                                {t.origem === 'Comanda' ? 'Mesa' : 'Balcão'} ·{' '}
                                {new Date(t.data).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })}
                                {t.nota && <span className="ml-1 text-amber-500">{t.nota}</span>}
                              </p>
                            </div>
                          </div>
                          <p className={`text-sm font-bold font-mono ${t.valor < minVal || t.valor > maxVal ? 'text-gray-400' : 'text-white'}`}>
                            {fmt(t.valor)}
                          </p>
                        </div>
                      ))
                    )}
                  </div>
                )}
              </div>
            )
          })
        )}
      </div>
    </div>
  )
}

// ── Gráfico SVG de barras (principal) ─────────────────────────────────────────
// ── Modal de detalhe do dia ───────────────────────────────────────────────────
