'use client'

import { useEffect, useState, useCallback } from 'react'
import { api } from '@/lib/api'
import toast, { Toaster } from 'react-hot-toast'
import clsx from 'clsx'
import {
  Clock, CheckCircle, XCircle, Package, User as UserIcon,
  ShoppingBag, LayoutList, RefreshCw, Loader2, ChevronLeft, ChevronRight,
  AlertTriangle, TimerIcon, Plus,
} from 'lucide-react'

const PAYMENT_METHODS = ['Dinheiro', 'Pix', 'Débito', 'Crédito', 'Crediario']

type Reservation = {
  id: string
  userId: string
  userName?: string
  productId: string
  productName?: string
  productImageUrl?: string
  variantId?: string
  variantLabel?: string
  quantity: number
  status: string
  notes?: string
  reservedAt: string
  expiresAt: string
  fulfilledAt?: string
  cancelledAt?: string
  isExpired: boolean
}

type OpenComanda = {
  id: string
  mesaNumero?: number
  userName?: string
  totalInReais?: number
}

const statusCls: Record<string, string> = {
  active:    'bg-blue-500/15 text-blue-400 border-blue-500/30',
  fulfilled: 'bg-green-500/15 text-green-400 border-green-500/30',
  cancelled: 'bg-red-500/15 text-red-400 border-red-500/30',
  expired:   'bg-gray-500/15 text-gray-400 border-gray-500/30',
}

const statusLabel: Record<string, string> = {
  active:    'Aguardando',
  fulfilled: 'Homologada',
  cancelled: 'Cancelada',
  expired:   'Expirada',
}

function fmtDate(d: string) {
  return new Date(d).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
}

function timeUntil(d: string) {
  const diff = new Date(d).getTime() - Date.now()
  if (diff <= 0) return 'expirada'
  const h = Math.floor(diff / 3_600_000)
  const m = Math.floor((diff % 3_600_000) / 60_000)
  return h > 0 ? `${h}h ${m}m` : `${m}min`
}

export default function ReservasPage() {
  const [items,       setItems]       = useState<Reservation[]>([])
  const [loading,     setLoading]     = useState(true)
  const [statusFilter,setStatusFilter]= useState('active')
  const [page,        setPage]        = useState(1)
  const [totalPages,  setTotalPages]  = useState(1)
  const [totalCount,  setTotalCount]  = useState(0)

  // Modal de homologação
  const [homModal,    setHomModal]    = useState<Reservation | null>(null)
  const [homMode,     setHomMode]     = useState<'pdv' | 'comanda'>('pdv')
  const [homPayment,  setHomPayment]  = useState('Dinheiro')
  const [comandas,    setComandas]    = useState<OpenComanda[]>([])
  const [homComanda,  setHomComanda]  = useState<string>('')
  const [submitting,  setSubmitting]  = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const { data } = await api.get('/api/reservations', {
        params: { status: statusFilter || undefined, page, pageSize: 20 },
      })
      setItems(data.items)
      setTotalPages(data.totalPages)
      setTotalCount(data.total)
    } catch { toast.error('Erro ao carregar reservas') }
    finally  { setLoading(false) }
  }, [statusFilter, page])

  useEffect(() => { load() }, [load])

  async function loadComandas() {
    try {
      const { data } = await api.get('/api/comanda/dashboard')
      setComandas(data.map((c: any) => ({
        id: c.id,
        mesaNumero: c.mesaNumero,
        userName: c.userName,
        totalInReais: c.totalInReais,
      })))
    } catch { /* silencioso */ }
  }

  async function openHomModal(r: Reservation) {
    setHomModal(r)
    setHomMode('pdv')
    setHomPayment('Dinheiro')
    setHomComanda('')
    await loadComandas()
  }

  async function handleHomologar() {
    if (!homModal) return
    if (homMode === 'comanda' && !homComanda) {
      toast.error('Selecione uma comanda'); return
    }
    setSubmitting(true)
    try {
      await api.post(`/api/reservations/${homModal.id}/homologar`, {
        mode:          homMode,
        paymentMethod: homMode === 'pdv' ? homPayment : undefined,
        comandaId:     homMode === 'comanda' ? homComanda : undefined,
      })
      toast.success('Reserva homologada!')
      setHomModal(null)
      load()
    } catch (e: any) {
      toast.error(e?.response?.data?.message ?? 'Erro ao homologar')
    } finally { setSubmitting(false) }
  }

  async function handleCancel(r: Reservation) {
    if (!confirm(`Cancelar reserva de "${r.productName}" para ${r.userName}?`)) return
    try {
      await api.delete(`/api/reservations/${r.id}`)
      toast.success('Reserva cancelada')
      load()
    } catch { toast.error('Erro ao cancelar') }
  }

  async function handleExtend(r: Reservation) {
    try {
      await api.put(`/api/reservations/${r.id}/extend`)
      toast.success('+48h adicionadas')
      load()
    } catch { toast.error('Erro ao estender') }
  }

  const badges = [
    { value: 'active',    label: 'Aguardando' },
    { value: 'fulfilled', label: 'Homologadas' },
    { value: 'cancelled', label: 'Canceladas' },
    { value: '',          label: 'Todas' },
  ]

  return (
    <div className="p-4 md:p-6 max-w-5xl mx-auto">
      <Toaster />

      {/* Header */}
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 rounded-xl bg-brand-500/10">
          <LayoutList className="w-5 h-5 text-brand-400" />
        </div>
        <div>
          <h1 className="text-xl font-black text-white">Pré-vendas / Reservas</h1>
          <p className="text-sm text-gray-400">{totalCount} reserva{totalCount !== 1 ? 's' : ''} encontrada{totalCount !== 1 ? 's' : ''}</p>
        </div>
        <button onClick={load} className="ml-auto p-2 rounded-xl bg-surface-700 hover:bg-surface-600 transition-colors text-gray-400">
          <RefreshCw className="w-4 h-4" />
        </button>
      </div>

      {/* Filtros */}
      <div className="flex gap-2 flex-wrap mb-5">
        {badges.map(b => (
          <button
            key={b.value}
            onClick={() => { setStatusFilter(b.value); setPage(1) }}
            className={clsx(
              'px-3 py-1.5 rounded-full text-xs font-semibold border transition-colors',
              statusFilter === b.value
                ? 'bg-brand-500/20 text-brand-300 border-brand-500/40'
                : 'bg-surface-700 text-gray-400 border-surface-600 hover:border-surface-500'
            )}
          >
            {b.label}
          </button>
        ))}
      </div>

      {/* Lista */}
      {loading ? (
        <div className="flex justify-center py-16"><Loader2 className="w-8 h-8 animate-spin text-brand-400" /></div>
      ) : items.length === 0 ? (
        <div className="text-center py-16 text-gray-500">
          <LayoutList className="w-10 h-10 mx-auto mb-3 opacity-30" />
          <p>Nenhuma reserva encontrada</p>
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          {items.map(r => {
            const expired = r.isExpired || new Date(r.expiresAt) < new Date()
            const displayStatus = r.status === 'active' && expired ? 'expired' : r.status
            return (
              <div key={r.id} className="card flex gap-4 items-start">
                {/* Imagem */}
                <div className="w-14 h-14 rounded-lg bg-surface-700 flex-shrink-0 overflow-hidden flex items-center justify-center">
                  {r.productImageUrl
                    ? <img src={r.productImageUrl} alt={r.productName} className="w-full h-full object-cover" />
                    : <Package className="w-6 h-6 text-surface-500" />}
                </div>

                {/* Info */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <p className="font-bold text-white text-sm truncate">{r.productName}</p>
                    {r.variantLabel && <span className="text-xs text-gray-400">· {r.variantLabel}</span>}
                    <span className={clsx('text-[10px] font-bold px-2 py-0.5 rounded-full border uppercase tracking-wider ml-auto',
                      statusCls[displayStatus] ?? statusCls['expired'])}>
                      {statusLabel[displayStatus] ?? displayStatus}
                    </span>
                  </div>

                  <div className="flex items-center gap-3 mt-1 text-xs text-gray-400 flex-wrap">
                    <span className="flex items-center gap-1"><UserIcon className="w-3 h-3" />{r.userName ?? 'Cliente'}</span>
                    <span className="flex items-center gap-1"><ShoppingBag className="w-3 h-3" />Qtd: <strong className="text-white">{r.quantity}</strong></span>
                    <span className="flex items-center gap-1"><Clock className="w-3 h-3" />Reservado: {fmtDate(r.reservedAt)}</span>
                    {r.status === 'active' && !expired && (
                      <span className="flex items-center gap-1 text-amber-400">
                        <TimerIcon className="w-3 h-3" />Expira em: {timeUntil(r.expiresAt)}
                      </span>
                    )}
                    {r.status === 'fulfilled' && r.fulfilledAt && (
                      <span className="flex items-center gap-1 text-green-400">
                        <CheckCircle className="w-3 h-3" />Homologada: {fmtDate(r.fulfilledAt)}
                      </span>
                    )}
                  </div>

                  {r.notes && <p className="text-xs text-gray-500 mt-1 italic">"{r.notes}"</p>}
                </div>

                {/* Ações */}
                {r.status === 'active' && !expired && (
                  <div className="flex flex-col gap-2 flex-shrink-0">
                    <button onClick={() => openHomModal(r)}
                      className="px-3 py-1.5 rounded-lg bg-green-500/20 text-green-400 border border-green-500/30
                                 hover:bg-green-500/30 text-xs font-semibold transition-colors flex items-center gap-1">
                      <CheckCircle className="w-3 h-3" /> Homologar
                    </button>
                    <button onClick={() => handleExtend(r)}
                      className="px-3 py-1.5 rounded-lg bg-amber-500/10 text-amber-400 border border-amber-500/20
                                 hover:bg-amber-500/20 text-xs font-semibold transition-colors flex items-center gap-1">
                      <Plus className="w-3 h-3" /> +48h
                    </button>
                    <button onClick={() => handleCancel(r)}
                      className="px-3 py-1.5 rounded-lg bg-red-500/10 text-red-400 border border-red-500/20
                                 hover:bg-red-500/20 text-xs font-semibold transition-colors flex items-center gap-1">
                      <XCircle className="w-3 h-3" /> Cancelar
                    </button>
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}

      {/* Paginação */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-3 mt-6">
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
            className="p-2 rounded-lg bg-surface-700 disabled:opacity-40">
            <ChevronLeft className="w-4 h-4" />
          </button>
          <span className="text-sm text-gray-400">{page} / {totalPages}</span>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}
            className="p-2 rounded-lg bg-surface-700 disabled:opacity-40">
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Modal de Homologação */}
      {homModal && (
        <div className="fixed inset-0 bg-black/70 flex items-center justify-center z-50 p-4">
          <div className="bg-surface-800 rounded-2xl w-full max-w-md p-6 flex flex-col gap-5">
            <div>
              <h2 className="text-lg font-black text-white">Homologar reserva</h2>
              <p className="text-sm text-gray-400 mt-0.5">
                {homModal.productName} · {homModal.quantity}x · {homModal.userName}
              </p>
            </div>

            {/* Modo */}
            <div className="flex gap-2">
              {(['pdv', 'comanda'] as const).map(m => (
                <button
                  key={m}
                  onClick={() => setHomMode(m)}
                  className={clsx(
                    'flex-1 py-2.5 rounded-xl border text-sm font-semibold transition-colors',
                    homMode === m
                      ? 'bg-brand-500/20 text-brand-300 border-brand-500/40'
                      : 'bg-surface-700 text-gray-400 border-surface-600'
                  )}
                >
                  {m === 'pdv' ? '🧾 Frente de Caixa (PDV)' : '🪑 Adicionar a uma Comanda'}
                </button>
              ))}
            </div>

            {/* PDV — forma de pagamento */}
            {homMode === 'pdv' && (
              <div>
                <label className="text-xs text-gray-400 mb-2 block font-semibold">Forma de pagamento</label>
                <div className="grid grid-cols-3 gap-2">
                  {PAYMENT_METHODS.map(m => (
                    <button key={m} onClick={() => setHomPayment(m)}
                      className={clsx(
                        'py-2 rounded-lg text-xs font-semibold border transition-colors',
                        homPayment === m
                          ? 'bg-brand-500/20 text-brand-300 border-brand-500/40'
                          : 'bg-surface-700 text-gray-400 border-surface-600'
                      )}>
                      {m}
                    </button>
                  ))}
                </div>
              </div>
            )}

            {/* Comanda — seleção */}
            {homMode === 'comanda' && (
              <div>
                <label className="text-xs text-gray-400 mb-2 block font-semibold">Selecionar comanda aberta</label>
                {comandas.length === 0 ? (
                  <div className="flex items-center gap-2 text-amber-400 text-sm bg-amber-500/10 rounded-xl p-3">
                    <AlertTriangle className="w-4 h-4 flex-shrink-0" />
                    Nenhuma comanda aberta no momento
                  </div>
                ) : (
                  <div className="flex flex-col gap-2 max-h-48 overflow-y-auto">
                    {comandas.map(c => (
                      <button key={c.id} onClick={() => setHomComanda(c.id)}
                        className={clsx(
                          'flex items-center gap-3 p-3 rounded-xl border text-left transition-colors',
                          homComanda === c.id
                            ? 'bg-brand-500/20 border-brand-500/40'
                            : 'bg-surface-700 border-surface-600 hover:border-surface-500'
                        )}>
                        <div className="w-8 h-8 rounded-lg bg-surface-600 flex items-center justify-center text-xs font-bold text-white">
                          {c.mesaNumero ?? '?'}
                        </div>
                        <div>
                          <p className="text-sm font-semibold text-white">Mesa {c.mesaNumero ?? '—'}</p>
                          {c.userName && <p className="text-xs text-gray-400">{c.userName}</p>}
                        </div>
                        {c.totalInReais != null && (
                          <span className="ml-auto text-sm font-bold text-brand-300">
                            R$ {c.totalInReais.toFixed(2).replace('.', ',')}
                          </span>
                        )}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}

            <div className="flex gap-3 pt-1">
              <button onClick={() => setHomModal(null)} disabled={submitting}
                className="flex-1 py-3 rounded-xl bg-surface-700 text-gray-300 text-sm font-semibold">
                Cancelar
              </button>
              <button onClick={handleHomologar} disabled={submitting || (homMode === 'comanda' && !homComanda)}
                className="flex-1 py-3 rounded-xl bg-brand-500 hover:bg-brand-400 disabled:opacity-40
                           text-white text-sm font-bold transition-colors flex items-center justify-center gap-2">
                {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle className="w-4 h-4" />}
                Confirmar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
