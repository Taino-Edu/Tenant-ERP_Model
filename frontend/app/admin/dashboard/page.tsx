'use client'
import { useEffect, useState, useCallback, useRef } from 'react'
import { comandaApi, productApi, ComandaDto, Product } from '@/lib/api'
import { startHub, stopHub, ComandaUpdatedEvent } from '@/lib/signalr'
import toast from 'react-hot-toast'
import {
  Wifi, WifiOff, RefreshCw, Users, TrendingUp, Banknote,
  Clock, CheckCircle, XCircle, Plus, ChevronDown, ChevronUp,
  History, Search, Loader2, TableProperties, Trash2,
} from 'lucide-react'
import clsx from 'clsx'

const fmt = (n: number) => `R$ ${n.toFixed(2).replace('.', ',')}`

function elapsedLabel(openedAt: string) {
  const mins = Math.floor((Date.now() - new Date(openedAt).getTime()) / 60000)
  return mins < 60 ? `${mins}min` : `${Math.floor(mins / 60)}h${mins % 60}min`
}

function elapsedColor(openedAt: string) {
  const mins = Math.floor((Date.now() - new Date(openedAt).getTime()) / 60000)
  if (mins < 20) return 'text-accent-green'
  if (mins < 45) return 'text-amber-400'
  return 'text-red-400'
}

// ── Modal: adicionar item a uma comanda ───────────────────────────────────────

function AddItemModal({
  comandaId, onClose, onAdded,
}: {
  comandaId: string
  onClose: () => void
  onAdded: (updated: ComandaDto) => void
}) {
  const [products, setProducts]   = useState<Product[]>([])
  const [loading, setLoading]     = useState(true)
  const [adding, setAdding]       = useState<string | null>(null)
  const [search, setSearch]       = useState('')

  useEffect(() => {
    productApi.list()
      .then(r => setProducts(r.data.filter(p => p.isActive && p.stockQuantity > 0)))
      .catch(() => toast.error('Erro ao carregar produtos'))
      .finally(() => setLoading(false))
  }, [])

  async function handleAdd(product: Product) {
    setAdding(product.id)
    try {
      const { data } = await comandaApi.addItem(comandaId, {
        productId:       product.id,
        itemName:        product.name,
        unitPriceInCents: product.priceInCents,
        quantity:        1,
      })
      onAdded(data)
      toast.success(`${product.name} adicionado!`)
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao adicionar item.')
    } finally {
      setAdding(null)
    }
  }

  const filtered = products.filter(p =>
    p.name.toLowerCase().includes(search.toLowerCase()) ||
    p.category.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm" onClick={onClose}>
      <div className="bg-surface-700 border border-surface-500 rounded-2xl w-full max-w-md max-h-[70vh] flex flex-col" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between p-4 border-b border-surface-500">
          <h3 className="font-semibold text-white">Adicionar produto à comanda</h3>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-300 transition-colors">
            <XCircle className="w-5 h-5" />
          </button>
        </div>
        <div className="p-3 border-b border-surface-500">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input
              autoFocus
              className="input pl-9 text-sm"
              placeholder="Buscar produto..."
              value={search}
              onChange={e => setSearch(e.target.value)}
            />
          </div>
        </div>
        <div className="flex-1 overflow-y-auto p-3 space-y-1.5">
          {loading ? (
            <div className="flex justify-center py-8">
              <div className="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <p className="text-center text-gray-500 text-sm py-8">Nenhum produto encontrado</p>
          ) : (
            filtered.map(p => (
              <button
                key={p.id}
                onClick={() => handleAdd(p)}
                disabled={!!adding}
                className="w-full flex items-center justify-between px-3 py-2.5 rounded-lg hover:bg-surface-600 transition-colors text-left disabled:opacity-50"
              >
                <div>
                  <p className="text-sm text-white font-medium">{p.name}</p>
                  <p className="text-xs text-gray-500">{p.category} · {p.stockQuantity} un.</p>
                </div>
                <div className="flex items-center gap-2 shrink-0 ml-3">
                  <span className="text-accent-gold text-sm font-bold">{fmt(p.priceInReais)}</span>
                  {adding === p.id
                    ? <Loader2 className="w-4 h-4 animate-spin text-brand-400" />
                    : <Plus className="w-4 h-4 text-brand-400" />
                  }
                </div>
              </button>
            ))
          )}
        </div>
      </div>
    </div>
  )
}

// ── Card de Comanda ───────────────────────────────────────────────────────────

// ── Modal de confirmação genérico ─────────────────────────────────────────────

function ConfirmModal({
  title, message, confirmLabel, confirmClass, onConfirm, onCancel,
}: {
  title:        string
  message:      string
  confirmLabel: string
  confirmClass: string
  onConfirm:    () => void
  onCancel:     () => void
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm" onClick={onCancel}>
      <div className="bg-surface-700 border border-surface-500 rounded-2xl w-full max-w-sm p-6" onClick={e => e.stopPropagation()}>
        <h3 className="font-semibold text-white text-lg mb-2">{title}</h3>
        <p className="text-gray-400 text-sm mb-6">{message}</p>
        <div className="flex gap-3">
          <button onClick={onCancel} className="btn-secondary flex-1 justify-center">Voltar</button>
          <button onClick={onConfirm} className={`${confirmClass} flex-1 justify-center`}>{confirmLabel}</button>
        </div>
      </div>
    </div>
  )
}

// ── Card de Comanda ───────────────────────────────────────────────────────────

function ComandaCard({
  comanda, onClose, onCancel, onUpdate, isNew,
}: {
  comanda: ComandaDto
  onClose:  (id: string) => void
  onCancel: (id: string) => void
  onUpdate: (updated: ComandaDto) => void
  isNew:    boolean
}) {
  const [expanded, setExpanded]   = useState(false)
  const [loading, setLoading]     = useState(false)
  const [addOpen, setAddOpen]     = useState(false)
  const [confirm, setConfirm]     = useState<'close' | 'cancel' | null>(null)
  const [, forceRender]           = useState(0)

  // Atualiza o tempo exibido a cada minuto
  useEffect(() => {
    const id = setInterval(() => forceRender(n => n + 1), 60000)
    return () => clearInterval(id)
  }, [])

  const statusMap: Record<string, string> = {
    Aberta: 'badge-aberta', EmAndamento: 'badge-andamento',
  }
  const statusLabel: Record<string, string> = {
    Aberta: '● Aberta', EmAndamento: '● Em Andamento',
  }

  async function handleClose() {
    setConfirm(null)
    setLoading(true)
    try { await onClose(comanda.id) } finally { setLoading(false) }
  }
  async function handleCancel() {
    setConfirm(null)
    setLoading(true)
    try { await onCancel(comanda.id) } finally { setLoading(false) }
  }

  return (
    <>
      {addOpen && (
        <AddItemModal
          comandaId={comanda.id}
          onClose={() => setAddOpen(false)}
          onAdded={updated => { onUpdate(updated); setAddOpen(false) }}
        />
      )}
      {confirm === 'close' && (
        <ConfirmModal
          title="Fechar comanda"
          message={`Confirma o fechamento da comanda de ${comanda.userName}? Total: ${fmt(comanda.totalInReais)}`}
          confirmLabel="Fechar"
          confirmClass="btn-success"
          onConfirm={handleClose}
          onCancel={() => setConfirm(null)}
        />
      )}
      {confirm === 'cancel' && (
        <ConfirmModal
          title="Cancelar comanda"
          message={`Cancelar a comanda de ${comanda.userName}? Esta ação não pode ser desfeita.`}
          confirmLabel="Cancelar comanda"
          confirmClass="btn-danger"
          onConfirm={handleCancel}
          onCancel={() => setConfirm(null)}
        />
      )}

      <div className={clsx(
        'card flex flex-col gap-3 transition-all duration-300',
        isNew && 'flash-new border-brand-500/50'
      )}>
        {/* Header */}
        <div className="flex items-start justify-between">
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 mb-1">
              <span className={statusMap[comanda.status] ?? 'badge'}>{statusLabel[comanda.status]}</span>
            </div>
            <p className="font-semibold text-white truncate">{comanda.userName}</p>
            {comanda.tableIdentifier && (
              <div className="flex items-center gap-1 text-xs text-gray-400 mt-0.5">
                <TableProperties className="w-3 h-3" /> {comanda.tableIdentifier}
              </div>
            )}
          </div>
          <div className="text-right ml-3">
            <p className="text-xl font-bold text-accent-gold">{fmt(comanda.totalInReais)}</p>
            <div className={clsx('flex items-center gap-1 text-xs justify-end mt-0.5', elapsedColor(comanda.openedAt))}>
              <Clock className="w-3 h-3" />{elapsedLabel(comanda.openedAt)}
            </div>
          </div>
        </div>

        {/* Itens resumo */}
        <div className="flex items-center justify-between text-sm">
          <span className="text-gray-400">{comanda.items.length} {comanda.items.length === 1 ? 'item' : 'itens'}</span>
          <button
            onClick={() => setExpanded(v => !v)}
            className="text-gray-500 hover:text-gray-300 flex items-center gap-1 text-xs transition-colors"
          >
            {expanded ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
            {expanded ? 'Ocultar' : 'Ver itens'}
          </button>
        </div>

        {expanded && comanda.items.length > 0 && (
          <div className="bg-surface-800 rounded-lg p-3 space-y-1.5 animate-fade-in">
            {comanda.items.map(item => (
              <div key={item.id} className="flex items-center justify-between text-sm">
                <span className="text-gray-300 flex-1 truncate">{item.quantity}× {item.itemNameSnapshot}</span>
                <span className="text-gray-400 ml-2 shrink-0">{fmt(item.subtotalInReais)}</span>
              </div>
            ))}
            {comanda.pointsApplied > 0 && (
              <div className="flex items-center justify-between text-sm border-t border-surface-500 pt-1.5">
                <span className="text-brand-300">Pontos aplicados</span>
                <span className="text-brand-300">−{fmt(comanda.pointsApplied / 100)}</span>
              </div>
            )}
            <div className="border-t border-surface-500 pt-1.5 flex justify-between text-sm font-semibold">
              <span className="text-gray-300">Total</span>
              <span className="text-accent-gold">{fmt(comanda.totalInReais)}</span>
            </div>
          </div>
        )}

        {/* Ações */}
        <div className="flex gap-2 pt-1">
          <button
            onClick={() => setAddOpen(true)}
            className="btn-secondary py-1.5 px-3"
            title="Adicionar item"
          >
            <Plus className="w-4 h-4" />
          </button>
          <button
            onClick={() => setConfirm('close')} disabled={loading}
            className="btn-success flex-1 justify-center text-sm py-1.5"
          >
            <CheckCircle className="w-4 h-4" /> Fechar
          </button>
          <button
            onClick={() => setConfirm('cancel')} disabled={loading}
            className="btn-danger py-1.5 px-3"
            title="Cancelar comanda"
          >
            <XCircle className="w-4 h-4" />
          </button>
        </div>
      </div>
    </>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────

export default function DashboardPage() {
  const [tab, setTab]             = useState<'ativas' | 'historico'>('ativas')
  const [comandas, setComandas]   = useState<ComandaDto[]>([])
  const [history, setHistory]     = useState<ComandaDto[]>([])
  const [loading, setLoading]     = useState(true)
  const [histLoading, setHistLoad]= useState(false)
  const [connected, setConnected] = useState(false)
  const [newIds, setNewIds]       = useState<Set<string>>(new Set())
  const [search, setSearch]       = useState('')
  const prevCountRef              = useRef(0)

  const fetchComandas = useCallback(async () => {
    try {
      const { data } = await comandaApi.dashboard()
      setComandas(data)
    } catch {
      toast.error('Erro ao carregar comandas')
    } finally {
      setLoading(false)
    }
  }, [])

  const fetchHistory = useCallback(async () => {
    setHistLoad(true)
    try {
      const { data } = await comandaApi.history()
      setHistory(data)
    } catch {
      toast.error('Erro ao carregar histórico')
    } finally {
      setHistLoad(false)
    }
  }, [])

  useEffect(() => {
    fetchComandas()
    let hub: Awaited<ReturnType<typeof startHub>>

    startHub().then(h => {
      hub = h
      setConnected(true)

      hub.on('ComandaUpdated', (event: ComandaUpdatedEvent) => {
        setNewIds(s => new Set(s).add(event.comandaId))
        setTimeout(() => setNewIds(s => { const n = new Set(s); n.delete(event.comandaId); return n }), 3000)
        fetchComandas()
        toast(`📋 ${event.userName}: +${event.lastItemAdded ?? 'item'}`, {
          icon: '🃏',
          style: { background: '#1e1e28', color: '#fff', border: '1px solid #7c3aed' }
        })
      })

      hub.on('ComandaClosed', () => { fetchComandas(); fetchHistory() })
      hub.onclose(() => setConnected(false))
      hub.onreconnected(() => { setConnected(true); fetchComandas() })
    }).catch(() => setConnected(false))

    return () => { stopHub() }
  }, [fetchComandas, fetchHistory])

  useEffect(() => {
    if (tab === 'historico') fetchHistory()
  }, [tab, fetchHistory])

  useEffect(() => {
    if (comandas.length > prevCountRef.current && prevCountRef.current > 0)
      toast('🎉 Nova comanda aberta!', { duration: 3000 })
    prevCountRef.current = comandas.length
  }, [comandas.length])

  function handleUpdate(updated: ComandaDto) {
    setComandas(prev => prev.map(c => c.id === updated.id ? updated : c))
  }

  async function handleClose(id: string) {
    await comandaApi.close(id)
    toast.success('Comanda fechada!')
    fetchComandas()
    fetchHistory()
  }
  async function handleCancel(id: string) {
    await comandaApi.cancel(id)
    toast.success('Comanda cancelada.')
    fetchComandas()
    fetchHistory()
  }

  const totalAberto   = comandas.reduce((s, c) => s + c.totalInReais, 0)
  const emAndamento   = comandas.filter(c => c.status === 'EmAndamento').length
  const totalFechado  = history.filter(c => c.status === 'Fechada').reduce((s, c) => s + c.totalInReais, 0)

  const filtered = comandas.filter(c =>
    !search || c.userName.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <div className="p-6 space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-bold text-white">Dashboard ao Vivo</h1>
          <p className="text-gray-400 text-sm mt-0.5">Comandas abertas em tempo real</p>
        </div>
        <div className="flex items-center gap-3">
          <div className={clsx('flex items-center gap-2 px-3 py-1.5 rounded-full text-xs font-medium border',
            connected
              ? 'bg-accent-green/10 text-accent-green border-accent-green/30'
              : 'bg-red-500/10 text-red-400 border-red-500/30'
          )}>
            {connected ? <Wifi className="w-3.5 h-3.5" /> : <WifiOff className="w-3.5 h-3.5" />}
            {connected ? 'Conectado' : 'Desconectado'}
          </div>
          <button onClick={fetchComandas} className="btn-secondary text-sm py-1.5">
            <RefreshCw className="w-4 h-4" /> Atualizar
          </button>
        </div>
      </div>

      {/* Métricas */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {[
          { label: 'Comandas Ativas',     value: comandas.length,     icon: Users,       color: 'text-brand-400',  bg: 'bg-brand-600/10'  },
          { label: 'Em Andamento',         value: emAndamento,         icon: TrendingUp,  color: 'text-amber-400',  bg: 'bg-amber-500/10'  },
          { label: 'Em Aberto',            value: fmt(totalAberto),    icon: Clock,       color: 'text-red-400',    bg: 'bg-red-500/10'    },
          { label: 'Fechado Hoje',         value: fmt(totalFechado),   icon: Banknote,    color: 'text-accent-gold',bg: 'bg-amber-500/10'  },
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

      {/* Tabs */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex gap-1 bg-surface-800 p-1 rounded-lg">
          <button
            onClick={() => setTab('ativas')}
            className={clsx('px-4 py-1.5 rounded-md text-sm font-medium transition-all',
              tab === 'ativas' ? 'bg-brand-600 text-white' : 'text-gray-400 hover:text-gray-200')}
          >
            <Users className="w-4 h-4 inline mr-1.5" />Ativas ({comandas.length})
          </button>
          <button
            onClick={() => setTab('historico')}
            className={clsx('px-4 py-1.5 rounded-md text-sm font-medium transition-all',
              tab === 'historico' ? 'bg-brand-600 text-white' : 'text-gray-400 hover:text-gray-200')}
          >
            <History className="w-4 h-4 inline mr-1.5" />Histórico de Hoje
          </button>
        </div>

        {tab === 'ativas' && (
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input
              className="input pl-9 text-sm w-56"
              placeholder="Buscar por cliente..."
              value={search}
              onChange={e => setSearch(e.target.value)}
            />
          </div>
        )}
      </div>

      {/* ── Tab: Ativas ──────────────────────────────────────────────────────── */}
      {tab === 'ativas' && (
        loading ? (
          <div className="flex items-center justify-center py-24">
            <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          </div>
        ) : filtered.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-24 text-center">
            <div className="w-16 h-16 bg-surface-700 rounded-2xl flex items-center justify-center mb-4">
              <Users className="w-8 h-8 text-gray-600" />
            </div>
            <p className="text-gray-400 font-medium">
              {search ? `Nenhuma comanda para "${search}"` : 'Nenhuma comanda aberta no momento'}
            </p>
            {!search && <p className="text-gray-600 text-sm mt-1">Clientes acessam via QR Code nas mesas</p>}
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4 gap-4">
            {filtered.map(c => (
              <ComandaCard
                key={c.id}
                comanda={c}
                onClose={handleClose}
                onCancel={handleCancel}
                onUpdate={handleUpdate}
                isNew={newIds.has(c.id)}
              />
            ))}
          </div>
        )
      )}

      {/* ── Tab: Histórico ───────────────────────────────────────────────────── */}
      {tab === 'historico' && (
        histLoading ? (
          <div className="flex items-center justify-center py-24">
            <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          </div>
        ) : history.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-24 text-center">
            <div className="w-16 h-16 bg-surface-700 rounded-2xl flex items-center justify-center mb-4">
              <History className="w-8 h-8 text-gray-600" />
            </div>
            <p className="text-gray-400 font-medium">Nenhuma comanda encerrada hoje</p>
          </div>
        ) : (
          <div className="space-y-2">
            {history.map(c => (
              <div key={c.id} className="card flex items-center justify-between gap-4">
                <div className="flex items-center gap-3 min-w-0">
                  <div className={clsx(
                    'w-8 h-8 rounded-lg flex items-center justify-center shrink-0',
                    c.status === 'Fechada' ? 'bg-accent-green/10' : 'bg-red-500/10'
                  )}>
                    {c.status === 'Fechada'
                      ? <CheckCircle className="w-4 h-4 text-accent-green" />
                      : <Trash2 className="w-4 h-4 text-red-400" />
                    }
                  </div>
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-white truncate">{c.userName}</p>
                    <p className="text-xs text-gray-500 flex items-center gap-1">
                      <Clock className="w-3 h-3" />
                      {c.closedAt
                        ? new Date(c.closedAt).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
                        : '—'
                      }
                      <span>·</span>
                      {c.items.length} {c.items.length === 1 ? 'item' : 'itens'}
                    </p>
                  </div>
                </div>
                <div className="text-right shrink-0">
                  <p className={clsx('font-bold', c.status === 'Fechada' ? 'text-accent-gold' : 'text-gray-500')}>
                    {fmt(c.totalInReais)}
                  </p>
                  <p className={clsx('text-xs', c.status === 'Fechada' ? 'text-accent-green' : 'text-red-400')}>
                    {c.status === 'Fechada' ? 'Fechada' : 'Cancelada'}
                  </p>
                </div>
              </div>
            ))}
          </div>
        )
      )}
    </div>
  )
}
