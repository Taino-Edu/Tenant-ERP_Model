'use client'
import { useEffect, useState, useCallback, useRef } from 'react'
import { comandaApi, ComandaDto } from '@/lib/api'
import { startHub, stopHub, ComandaUpdatedEvent } from '@/lib/signalr'
import toast from 'react-hot-toast'
import {
  Wifi, WifiOff, RefreshCw, Users, TrendingUp,
  Clock, CheckCircle, XCircle, Plus, Trash2,
  ChevronDown, ChevronUp, Banknote, TableProperties
} from 'lucide-react'
import clsx from 'clsx'

// ── Componente Card de Comanda ────────────────────────────────────────────────
function ComandaCard({
  comanda, onClose, onCancel, isNew
}: {
  comanda: ComandaDto
  onClose:  (id: string) => void
  onCancel: (id: string) => void
  isNew:    boolean
}) {
  const [expanded, setExpanded] = useState(false)
  const [loading, setLoading]   = useState(false)
  const statusMap: Record<string, string> = {
    Aberta: 'badge-aberta', EmAndamento: 'badge-andamento',
    Fechada: 'badge-fechada', Cancelada: 'badge-cancelada',
  }
  const statusLabel: Record<string, string> = {
    Aberta: '● Aberta', EmAndamento: '● Em Andamento',
    Fechada: '✓ Fechada', Cancelada: '✗ Cancelada',
  }

  const elapsed = () => {
    const mins = Math.floor((Date.now() - new Date(comanda.openedAt).getTime()) / 60000)
    return mins < 60 ? `${mins}min` : `${Math.floor(mins/60)}h${mins%60}min`
  }

  async function handleClose() {
    setLoading(true)
    try { await onClose(comanda.id) } finally { setLoading(false) }
  }
  async function handleCancel() {
    if (!confirm(`Cancelar a comanda de ${comanda.userName}?`)) return
    setLoading(true)
    try { await onCancel(comanda.id) } finally { setLoading(false) }
  }

  return (
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
              <TableProperties className="w-3 h-3" />
              {comanda.tableIdentifier}
            </div>
          )}
        </div>
        <div className="text-right ml-3">
          <p className="text-xl font-bold text-accent-gold">
            R$ {comanda.totalInReais.toFixed(2).replace('.', ',')}
          </p>
          <div className="flex items-center gap-1 text-xs text-gray-500 justify-end mt-0.5">
            <Clock className="w-3 h-3" />{elapsed()}
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

      {/* Lista de itens expandida */}
      {expanded && comanda.items.length > 0 && (
        <div className="bg-surface-800 rounded-lg p-3 space-y-1.5 animate-fade-in">
          {comanda.items.map(item => (
            <div key={item.id} className="flex items-center justify-between text-sm">
              <span className="text-gray-300 flex-1 truncate">
                {item.quantity}× {item.itemNameSnapshot}
              </span>
              <span className="text-gray-400 ml-2 shrink-0">
                R$ {item.subtotalInReais.toFixed(2).replace('.', ',')}
              </span>
            </div>
          ))}
          <div className="border-t border-surface-500 pt-1.5 flex justify-between text-sm font-semibold">
            <span className="text-gray-300">Total</span>
            <span className="text-accent-gold">R$ {comanda.totalInReais.toFixed(2).replace('.', ',')}</span>
          </div>
        </div>
      )}

      {/* Ações */}
      <div className="flex gap-2 pt-1">
        <button
          onClick={handleClose} disabled={loading}
          className="btn-success flex-1 justify-center text-sm py-1.5"
        >
          <CheckCircle className="w-4 h-4" /> Fechar
        </button>
        <button
          onClick={handleCancel} disabled={loading}
          className="btn-danger py-1.5 px-3"
          title="Cancelar comanda"
        >
          <XCircle className="w-4 h-4" />
        </button>
      </div>
    </div>
  )
}

// ── Página principal do Dashboard ────────────────────────────────────────────
export default function DashboardPage() {
  const [comandas, setComandas]   = useState<ComandaDto[]>([])
  const [loading, setLoading]     = useState(true)
  const [connected, setConnected] = useState(false)
  const [newIds, setNewIds]       = useState<Set<string>>(new Set())
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

  // Inicia o SignalR e escuta eventos
  useEffect(() => {
    fetchComandas()
    let hub: Awaited<ReturnType<typeof startHub>>

    startHub().then(h => {
      hub = h
      setConnected(true)

      // Nova atualização de comanda → re-busca e marca como novo
      hub.on('ComandaUpdated', (event: ComandaUpdatedEvent) => {
        setNewIds(s => new Set(s).add(event.comandaId))
        setTimeout(() => setNewIds(s => { const n = new Set(s); n.delete(event.comandaId); return n }), 3000)
        fetchComandas()
        toast(`📋 ${event.userName}: +${event.lastItemAdded ?? 'item'}`, {
          icon: '🃏',
          style: { background: '#1e1e28', color: '#fff', border: '1px solid #7c3aed' }
        })
      })

      hub.on('ComandaClosed', () => fetchComandas())
      hub.onclose(() => setConnected(false))
      hub.onreconnected(() => { setConnected(true); fetchComandas() })
    }).catch(() => setConnected(false))

    return () => { stopHub() }
  }, [fetchComandas])

  // Som/notificação quando entra nova comanda
  useEffect(() => {
    if (comandas.length > prevCountRef.current && prevCountRef.current > 0) {
      toast('🎉 Nova comanda aberta!', { duration: 3000 })
    }
    prevCountRef.current = comandas.length
  }, [comandas.length])

  async function handleClose(id: string) {
    await comandaApi.close(id)
    toast.success('Comanda fechada com sucesso!')
    fetchComandas()
  }
  async function handleCancel(id: string) {
    await comandaApi.cancel(id)
    toast.success('Comanda cancelada.')
    fetchComandas()
  }

  // Métricas do dashboard
  const totalAberto    = comandas.reduce((s, c) => s + c.totalInReais, 0)
  const emAndamento    = comandas.filter(c => c.status === 'EmAndamento').length
  const abertas        = comandas.filter(c => c.status === 'Aberta').length

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
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
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        {[
          { label: 'Mesas Ativas', value: comandas.length, icon: Users,        color: 'text-brand-400',  bg: 'bg-brand-600/10'  },
          { label: 'Em Andamento', value: emAndamento,     icon: TrendingUp,   color: 'text-amber-400',  bg: 'bg-amber-500/10'  },
          { label: 'Faturamento Aberto', value: `R$ ${totalAberto.toFixed(2).replace('.', ',')}`, icon: Banknote, color: 'text-accent-gold', bg: 'bg-amber-500/10' },
        ].map(m => (
          <div key={m.label} className="card flex items-center gap-4">
            <div className={clsx('w-11 h-11 rounded-xl flex items-center justify-center', m.bg)}>
              <m.icon className={clsx('w-5 h-5', m.color)} />
            </div>
            <div>
              <p className="text-2xl font-bold text-white">{m.value}</p>
              <p className="text-xs text-gray-400">{m.label}</p>
            </div>
          </div>
        ))}
      </div>

      {/* Grid de Comandas */}
      {loading ? (
        <div className="flex items-center justify-center py-24">
          <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>
      ) : comandas.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <div className="w-16 h-16 bg-surface-700 rounded-2xl flex items-center justify-center mb-4">
            <TableProperties className="w-8 h-8 text-gray-600" />
          </div>
          <p className="text-gray-400 font-medium">Nenhuma comanda aberta no momento</p>
          <p className="text-gray-600 text-sm mt-1">Clientes acessam via QR Code nas mesas</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4 gap-4">
          {comandas.map(c => (
            <ComandaCard
              key={c.id}
              comanda={c}
              onClose={handleClose}
              onCancel={handleCancel}
              isNew={newIds.has(c.id)}
            />
          ))}
        </div>
      )}
    </div>
  )
}
