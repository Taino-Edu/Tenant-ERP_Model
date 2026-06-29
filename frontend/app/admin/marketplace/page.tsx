'use client'

import { useEffect, useState, useCallback } from 'react'
import { marketplaceApi, CardListingDto } from '@/lib/api'
import Link from 'next/link'
import toast from 'react-hot-toast'
import clsx from 'clsx'
import {
  Search, Package, Loader2, Trash2, Eye, ChevronLeft, ChevronRight,
  User as UserIcon, ShoppingBag, CheckCircle, XCircle, Clock,
} from 'lucide-react'

const STATUS_OPTS = [
  { value: '', label: 'Todos' },
  { value: 'Available', label: 'Disponível' },
  { value: 'Reserved',  label: 'Reservado' },
  { value: 'Sold',      label: 'Vendido' },
]

const statusCls: Record<string, string> = {
  Available: 'bg-green-500/15 text-green-400 border-green-500/30',
  Reserved:  'bg-amber-500/15 text-amber-400 border-amber-400/30',
  Sold:      'bg-red-500/15 text-red-400 border-red-500/30',
}
const statusLabel: Record<string, string> = {
  Available: 'Disponível',
  Reserved:  'Reservado',
  Sold:      'Vendido',
}

function fmtPrice(cents: number) {
  return `R$ ${(cents / 100).toFixed(2).replace('.', ',')}`
}

export default function AdminMarketplacePage() {
  const [items,       setItems]       = useState<CardListingDto[]>([])
  const [loading,     setLoading]     = useState(true)
  const [search,      setSearch]      = useState('')
  const [statusFilter,setStatusFilter]= useState('')
  const [page,        setPage]        = useState(1)
  const [totalPages,  setTotalPages]  = useState(1)
  const [totalCount,  setTotalCount]  = useState(0)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const { data } = await marketplaceApi.list({
        page, pageSize: 30,
        search: search || undefined,
        status: statusFilter || undefined,
      })
      setItems(data.items)
      setTotalPages(data.totalPages)
      setTotalCount(data.totalCount)
    } catch { toast.error('Erro ao carregar') }
    finally  { setLoading(false) }
  }, [page, search, statusFilter])

  useEffect(() => { load() }, [load])

  async function handleDelete(l: CardListingDto) {
    if (!confirm(`Remover anúncio "${l.cardName}" de ${l.sellerName}?`)) return
    try {
      await marketplaceApi.remove(l.id)
      setItems(prev => prev.filter(i => i.id !== l.id))
      setTotalCount(c => c - 1)
      toast.success('Anúncio removido')
    } catch { toast.error('Erro ao remover') }
  }

  async function handleStatus(l: CardListingDto, status: string) {
    try {
      const { data } = await marketplaceApi.update(l.id, { status })
      setItems(prev => prev.map(i => i.id === l.id ? data : i))
      toast.success('Status atualizado')
    } catch { toast.error('Erro') }
  }

  const counts = {
    available: items.filter(i => i.status === 'Available').length,
    reserved:  items.filter(i => i.status === 'Reserved').length,
    sold:      items.filter(i => i.status === 'Sold').length,
  }

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-black text-white">Marketplace</h1>
          <p className="text-sm text-gray-400 mt-0.5">{totalCount} anúncios no total</p>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-3 gap-3 mb-6">
        <div className="card text-center">
          <div className="flex items-center justify-center gap-2 mb-1">
            <CheckCircle className="w-4 h-4 text-green-400" />
            <span className="text-xs text-gray-400">Disponíveis</span>
          </div>
          <p className="text-2xl font-black text-green-400">{counts.available}</p>
        </div>
        <div className="card text-center">
          <div className="flex items-center justify-center gap-2 mb-1">
            <Clock className="w-4 h-4 text-amber-400" />
            <span className="text-xs text-gray-400">Reservados</span>
          </div>
          <p className="text-2xl font-black text-amber-400">{counts.reserved}</p>
        </div>
        <div className="card text-center">
          <div className="flex items-center justify-center gap-2 mb-1">
            <XCircle className="w-4 h-4 text-red-400" />
            <span className="text-xs text-gray-400">Vendidos</span>
          </div>
          <p className="text-2xl font-black text-red-400">{counts.sold}</p>
        </div>
      </div>

      {/* Filtros */}
      <div className="flex flex-col sm:flex-row gap-3 mb-5">
        <div className="relative flex-1 max-w-xs">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
          <input
            className="input pl-9"
            placeholder="Buscar carta ou vendedor..."
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1) }}
          />
        </div>
        <div className="flex gap-2">
          {STATUS_OPTS.map(s => (
            <button
              key={s.value}
              onClick={() => { setStatusFilter(s.value); setPage(1) }}
              className={clsx(
                'px-3 py-2 rounded-xl text-xs font-semibold transition-colors',
                statusFilter === s.value
                  ? 'bg-brand-500 text-white'
                  : 'bg-surface-700 text-gray-400 hover:bg-surface-600',
              )}
            >
              {s.label}
            </button>
          ))}
        </div>
      </div>

      {/* Tabela */}
      {loading ? (
        <div className="flex items-center justify-center py-20">
          <Loader2 className="w-8 h-8 animate-spin text-brand-400" />
        </div>
      ) : items.length === 0 ? (
        <div className="text-center py-20 text-gray-500">
          <Package className="w-12 h-12 mx-auto mb-3 opacity-40" />
          <p>Nenhum anúncio encontrado</p>
        </div>
      ) : (
        <div className="card overflow-hidden p-0">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-surface-700">
                  <th className="text-left p-4 text-gray-400 font-semibold">Carta</th>
                  <th className="text-left p-4 text-gray-400 font-semibold">Vendedor</th>
                  <th className="text-left p-4 text-gray-400 font-semibold">Preço</th>
                  <th className="text-left p-4 text-gray-400 font-semibold">Status</th>
                  <th className="text-left p-4 text-gray-400 font-semibold">Interesses</th>
                  <th className="text-left p-4 text-gray-400 font-semibold">Data</th>
                  <th className="text-right p-4 text-gray-400 font-semibold">Ações</th>
                </tr>
              </thead>
              <tbody>
                {items.map(l => (
                  <tr key={l.id} className="border-b border-surface-700/50 hover:bg-surface-700/30 transition-colors">
                    {/* Carta */}
                    <td className="p-4">
                      <div className="flex items-center gap-3">
                        {l.cardImageUrl ? (
                          <img src={l.cardImageUrl} alt={l.cardName} className="w-10 h-10 rounded-lg object-contain bg-surface-700" />
                        ) : (
                          <div className="w-10 h-10 rounded-lg bg-surface-700 flex items-center justify-center">
                            <Package className="w-4 h-4 text-gray-500" />
                          </div>
                        )}
                        <div className="min-w-0">
                          <p className="font-semibold text-white truncate max-w-[160px]">{l.cardName}</p>
                          {l.cardGame && <p className="text-xs text-brand-400">{l.cardGame}</p>}
                          <p className="text-xs text-gray-500">{l.condition}</p>
                        </div>
                      </div>
                    </td>

                    {/* Vendedor */}
                    <td className="p-4">
                      <Link href={`/perfil/${l.sellerId}`} className="flex items-center gap-2 hover:text-brand-300 transition-colors">
                        {l.sellerImageUrl ? (
                          <img src={l.sellerImageUrl} alt={l.sellerName ?? ''} className="w-7 h-7 rounded-full object-cover" />
                        ) : (
                          <div className="w-7 h-7 rounded-full bg-surface-600 flex items-center justify-center">
                            <UserIcon className="w-3.5 h-3.5 text-gray-400" />
                          </div>
                        )}
                        <span className="text-sm text-white truncate max-w-[120px]">{l.sellerName}</span>
                      </Link>
                    </td>

                    {/* Preço */}
                    <td className="p-4 font-bold text-brand-300">{fmtPrice(l.priceInCents)}</td>

                    {/* Status */}
                    <td className="p-4">
                      <select
                        value={l.status}
                        onChange={e => handleStatus(l, e.target.value)}
                        className={clsx(
                          'text-xs font-semibold px-2 py-1 rounded-lg border bg-transparent cursor-pointer focus:outline-none',
                          statusCls[l.status] ?? 'border-surface-600 text-gray-400',
                        )}
                      >
                        <option value="Available">Disponível</option>
                        <option value="Reserved">Reservado</option>
                        <option value="Sold">Vendido</option>
                      </select>
                    </td>

                    {/* Interesses */}
                    <td className="p-4">
                      <span className={clsx(
                        'text-sm font-bold',
                        l.interestCount > 0 ? 'text-brand-300' : 'text-gray-600',
                      )}>
                        {l.interestCount}
                      </span>
                    </td>

                    {/* Data */}
                    <td className="p-4 text-xs text-gray-400">
                      {new Date(l.createdAt).toLocaleDateString('pt-BR')}
                    </td>

                    {/* Ações */}
                    <td className="p-4">
                      <div className="flex items-center justify-end gap-2">
                        <Link
                          href={`/cliente/mercado`}
                          className="p-1.5 rounded-lg bg-surface-700 hover:bg-brand-500/20 text-gray-400 hover:text-brand-400 transition-colors"
                          title="Ver no mercado"
                        >
                          <Eye className="w-4 h-4" />
                        </Link>
                        <button
                          onClick={() => handleDelete(l)}
                          className="p-1.5 rounded-lg bg-surface-700 hover:bg-red-500/20 text-gray-400 hover:text-red-400 transition-colors"
                          title="Remover anúncio"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Paginação */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-4 mt-6">
          <button disabled={page <= 1} onClick={() => setPage(p => p - 1)} className="p-2 rounded-xl bg-surface-800 disabled:opacity-40 hover:bg-surface-700 transition-colors">
            <ChevronLeft className="w-5 h-5" />
          </button>
          <span className="text-sm text-gray-400">{page} / {totalPages}</span>
          <button disabled={page >= totalPages} onClick={() => setPage(p => p + 1)} className="p-2 rounded-xl bg-surface-800 disabled:opacity-40 hover:bg-surface-700 transition-colors">
            <ChevronRight className="w-5 h-5" />
          </button>
        </div>
      )}
    </div>
  )
}
