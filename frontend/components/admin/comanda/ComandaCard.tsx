'use client'
import { useEffect, useState } from 'react'
import { comandaApi, ComandaDto, getErrorMessage } from '@/lib/api'
import toast from 'react-hot-toast'
import { CobrancaPixModal } from '@/components/admin/CobrancaPixModal'
import {
  TableProperties, Clock, ChevronDown, ChevronUp, Plus, CheckCircle, XCircle,
  Trash2, Star, Loader2,
} from 'lucide-react'
import clsx from 'clsx'
import { fmt, elapsedLabel, elapsedColor } from './shared'
import { AddItemModal } from './AddItemModal'
import { CloseComandaModal } from './CloseComandaModal'
import { ConfirmModal } from './ConfirmModal'

// ── Card de Comanda ───────────────────────────────────────────────────────────

export function ComandaCard({
  comanda, onClose, onCancel, onUpdate, onClosedExternally, isNew, recentChange, autoEmitMethods, fiscalEnabled,
}: {
  comanda: ComandaDto
  onClose:  (id: string, paymentMethod: string, secondMethod?: string, secondAmountInCents?: number, discountInCents?: number, emitirNotaFiscal?: boolean) => void
  onCancel: (id: string) => void
  onUpdate: (updated: ComandaDto, changeType?: 'add' | 'remove') => void
  onClosedExternally: () => void
  isNew:    boolean
  recentChange: 'add' | 'remove' | null
  autoEmitMethods: string[]
  fiscalEnabled: boolean
}) {
  const [expanded, setExpanded]   = useState(false)
  const [loading, setLoading]     = useState(false)
  const [addOpen, setAddOpen]     = useState(false)
  const [closeOpen, setCloseOpen] = useState(false)
  const [pixOpen, setPixOpen]     = useState(false)
  const [confirm, setConfirm]     = useState<'cancel' | null>(null)
  const [removingItem, setRemovingItem]   = useState<string | null>(null)
  const [updatingItem, setUpdatingItem]   = useState<string | null>(null)
  const [removingPts,  setRemovingPts]    = useState(false)
  const [, forceRender]           = useState(0)

  // Atualiza o tempo exibido a cada minuto
  useEffect(() => {
    const id = setInterval(() => forceRender(n => n + 1), 60000)
    return () => clearInterval(id)
  }, [])

  // Pontos só abatem "de verdade" no fechamento — enquanto aberta, mostra o total já líquido
  // pra não parecer que "usar pontos" não fez nada.
  const netTotal = Math.max(0, comanda.totalInReais - comanda.pointsApplied / 100)

  const statusMap: Record<string, string> = {
    Aberta: 'badge-aberta', EmAndamento: 'badge-andamento',
  }
  const statusLabel: Record<string, string> = {
    Aberta: '● Aberta', EmAndamento: '● Em Andamento',
  }

  async function handleClose(paymentMethod: string, secondMethod?: string, secondAmountInCents?: number, discountInCents?: number, emitirNotaFiscal?: boolean) {
    setCloseOpen(false)
    setLoading(true)
    try { await onClose(comanda.id, paymentMethod, secondMethod, secondAmountInCents, discountInCents, emitirNotaFiscal) } finally { setLoading(false) }
  }
  async function handleCancel() {
    setConfirm(null)
    setLoading(true)
    try { await onCancel(comanda.id) } finally { setLoading(false) }
  }
  async function handleRemoveItem(itemId: string, itemName: string) {
    if (!window.confirm(`Remover "${itemName}" da comanda?`)) return
    setRemovingItem(itemId)
    try {
      const { data } = await comandaApi.removeItem(comanda.id, itemId)
      onUpdate(data, 'remove')
      toast.success('Item removido.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao remover item.'))
    } finally {
      setRemovingItem(null)
    }
  }
  async function handleRemovePoints() {
    if (!window.confirm('Remover os pontos aplicados e devolver ao saldo do cliente?')) return
    setRemovingPts(true)
    try {
      const { data } = await comandaApi.removePoints(comanda.id)
      onUpdate(data, 'remove')
      toast.success('Pontos removidos e devolvidos ao cliente!')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao remover pontos.'))
    } finally {
      setRemovingPts(false)
    }
  }

  async function handleUpdateQty(itemId: string, newQty: number) {
    if (newQty < 0) return
    setUpdatingItem(itemId)
    try {
      const { data } = await comandaApi.updateItem(comanda.id, itemId, newQty)
      onUpdate(data)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao atualizar quantidade.'))
    } finally {
      setUpdatingItem(null)
    }
  }

  return (
    <>
      {addOpen && (
        <AddItemModal
          comandaId={comanda.id}
          onClose={() => setAddOpen(false)}
          onAdded={updated => { onUpdate(updated, 'add'); setAddOpen(false) }}
        />
      )}
      {closeOpen && (
        <CloseComandaModal
          comanda={comanda}
          onConfirm={handleClose}
          onCancel={() => setCloseOpen(false)}
          onGerarPix={() => { setCloseOpen(false); setPixOpen(true) }}
          autoEmitMethods={autoEmitMethods}
          fiscalEnabled={fiscalEnabled}
        />
      )}
      {pixOpen && (
        <CobrancaPixModal
          clienteNome={comanda.userName}
          gerar={() => comandaApi.gerarPix(comanda.id)}
          verificar={txid => comandaApi.statusPix(comanda.id, txid)}
          onClose={() => setPixOpen(false)}
          onSuccess={onClosedExternally}
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
        isNew && 'flash-new border-brand-500/50',
        recentChange === 'add'    && 'ring-2 ring-green-500/60 shadow-[0_0_12px_rgba(34,197,94,0.25)]',
        recentChange === 'remove' && 'ring-2 ring-amber-400/60 shadow-[0_0_12px_rgba(251,191,36,0.25)]',
      )}>
        {/* Header */}
        <div className="flex items-start justify-between">
          <div className="flex items-start gap-2.5 flex-1 min-w-0">
            {/* Avatar */}
            {comanda.profileImageUrl ? (
              <img
                src={comanda.profileImageUrl}
                alt=""
                className="w-9 h-9 rounded-full object-cover shrink-0 mt-0.5 ring-2 ring-surface-600"
              />
            ) : (
              <div className="w-9 h-9 rounded-full bg-brand-600/25 flex items-center justify-center shrink-0 mt-0.5 ring-2 ring-surface-600">
                <span className="text-sm font-bold text-brand-300 leading-none">
                  {comanda.userName[0]?.toUpperCase() ?? '?'}
                </span>
              </div>
            )}
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1 flex-wrap">
                <span className={statusMap[comanda.status] ?? 'badge'}>{statusLabel[comanda.status]}</span>
                {recentChange === 'add' && (
                  <span className="text-[10px] font-bold px-1.5 py-0.5 rounded-full bg-green-500/20 text-green-400 border border-green-500/30">+ adicionado</span>
                )}
                {recentChange === 'remove' && (
                  <span className="text-[10px] font-bold px-1.5 py-0.5 rounded-full bg-amber-400/20 text-amber-300 border border-amber-400/30">− removido</span>
                )}
              </div>
              <p className="font-semibold text-white truncate">{comanda.userName}</p>
              {comanda.tableIdentifier && (
                <div className="flex items-center gap-1 text-xs text-gray-400 mt-0.5">
                  <TableProperties className="w-3 h-3" /> {comanda.tableIdentifier}
                </div>
              )}
            </div>
          </div>
          <div className="text-right ml-3">
            <p className="text-xl font-bold text-accent-gold">{fmt(netTotal)}</p>
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
            {comanda.items.map(item => {
              const busy = updatingItem === item.id || removingItem === item.id
              return (
                <div key={item.id} className="flex items-center gap-2 text-sm">
                  {/* Controles de quantidade */}
                  <div className="flex items-center gap-1 shrink-0">
                    <button
                      onClick={() => handleUpdateQty(item.id, item.quantity - 1)}
                      disabled={busy}
                      className="w-5 h-5 rounded flex items-center justify-center bg-surface-600 hover:bg-red-600/30 text-gray-400 hover:text-red-400 transition-colors disabled:opacity-40 text-base leading-none"
                    >−</button>
                    <span className="w-5 text-center text-xs font-mono text-white">
                      {busy && updatingItem === item.id
                        ? <Loader2 className="w-3 h-3 animate-spin mx-auto" />
                        : item.quantity}
                    </span>
                    <button
                      onClick={() => handleUpdateQty(item.id, item.quantity + 1)}
                      disabled={busy}
                      className="w-5 h-5 rounded flex items-center justify-center bg-surface-600 hover:bg-emerald-600/30 text-gray-400 hover:text-emerald-400 transition-colors disabled:opacity-40 text-base leading-none"
                    >+</button>
                  </div>
                  <span className="text-gray-300 flex-1 truncate">{item.itemNameSnapshot}</span>
                  <div className="flex items-center gap-1.5 shrink-0">
                    <span className="text-gray-400 text-xs">{fmt(item.subtotalInReais)}</span>
                    <button
                      onClick={() => handleRemoveItem(item.id, item.itemNameSnapshot)}
                      disabled={busy}
                      className="p-0.5 text-gray-400 hover:text-red-400 transition-colors disabled:opacity-40"
                      title="Remover item"
                    >
                      {removingItem === item.id
                        ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                        : <Trash2 className="w-3.5 h-3.5" />}
                    </button>
                  </div>
                </div>
              )
            })}
            {comanda.pointsApplied > 0 && (
              <div className="flex items-center justify-between text-sm border-t border-surface-500 pt-1.5 gap-2">
                <span className="text-brand-300 flex items-center gap-1">
                  <Star className="w-3 h-3" />
                  Pontos aplicados
                </span>
                <div className="flex items-center gap-1.5">
                  <span className="text-brand-300">−{fmt(comanda.pointsApplied / 100)}</span>
                  <button
                    onClick={handleRemovePoints}
                    disabled={removingPts}
                    title="Remover pontos (devolver ao cliente)"
                    className="text-gray-400 hover:text-red-400 transition-colors disabled:opacity-40"
                  >
                    {removingPts
                      ? <Loader2 className="w-3 h-3 animate-spin" />
                      : <Trash2 className="w-3 h-3" />}
                  </button>
                </div>
              </div>
            )}
            <div className="border-t border-surface-500 pt-1.5 flex justify-between text-sm font-semibold">
              <span className="text-gray-300">Total</span>
              <span className="text-accent-gold">{fmt(netTotal)}</span>
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
            onClick={() => setCloseOpen(true)} disabled={loading}
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
