'use client'
import { useEffect, useState, useCallback, useRef } from 'react'
import { comandaApi, userApi, productApi, analyticsApi, ComandaDto, UserSummary, Product, COMANDA_PAYMENT_METHODS, FinanceiroDto, ClienteInsightDto } from '@/lib/api'
import { startHub, stopHub, ComandaUpdatedEvent } from '@/lib/signalr'
import { playGoalSound } from '@/lib/sounds'
import { tocarSom, notificarBrowser, pedirPermissaoNotificacao, incrementBadge, clearBadge } from '@/lib/notificacoes'
import CameraScanner from '@/components/CameraScanner'
import toast from 'react-hot-toast'
import {
  Wifi, WifiOff, RefreshCw, Users, TrendingUp, Banknote,
  Clock, CheckCircle, XCircle, Plus, ChevronDown, ChevronUp,
  History, Search, Loader2, TableProperties, Trash2, CreditCard, ScanBarcode, Camera,
  AlertTriangle, DollarSign, BarChart2, Trophy, Medal, Star, FolderOpen, Package,
} from 'lucide-react'
import clsx from 'clsx'

const fmt = (n: number) => `R$ ${n.toFixed(2).replace('.', ',')}`

/** Data de hoje no fuso de Brasília como YYYY-MM-DD (nunca usa UTC). */
const brToday = () => new Intl.DateTimeFormat('fr-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date())

// ── Mini gráfico de barras (últimos 7 dias) ───────────────────────────────────
function MiniBarChart({ dias }: { dias: FinanceiroDto['diaDia'] }) {
  const [hovered, setHovered] = useState<number | null>(null)
  if (!dias || dias.length === 0) return null
  const maxVal = Math.max(...dias.map(d => d.receita), 1)
  const BAR_H = 72

  return (
    <div className="card self-start">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
          <BarChart2 className="w-4 h-4 text-brand-400" /> Receita — últimos 7 dias
        </h3>
        <a href="/admin/financeiro" className="text-xs text-brand-400 hover:text-brand-300 transition-colors">
          Ver relatório completo →
        </a>
      </div>

      {/* Barras ancoradas no bottom */}
      <div className="flex items-end gap-1.5" style={{ height: `${BAR_H}px` }}>
        {dias.map((d, i) => {
          const barH = Math.max(4, Math.round((d.receita / maxVal) * BAR_H))
          const isHovered = hovered === i
          return (
            <div
              key={d.dia}
              className="flex-1 relative group cursor-pointer"
              style={{ height: `${barH}px` }}
              onMouseEnter={() => setHovered(i)}
              onMouseLeave={() => setHovered(null)}
            >
              {/* Tooltip */}
              {isHovered && (
                <div className="absolute bottom-full mb-1.5 left-1/2 -translate-x-1/2 bg-surface-700 border border-surface-500 rounded px-2 py-1 text-xs text-white whitespace-nowrap z-10 pointer-events-none">
                  {d.dia.slice(5).replace('-', '/')}: {fmt(d.receita)}
                </div>
              )}
              <div
                className={`w-full h-full rounded-t transition-colors ${isHovered ? 'bg-brand-400' : 'bg-brand-600'}`}
              />
            </div>
          )
        })}
      </div>

      {/* Labels de data abaixo das barras */}
      <div className="flex gap-1.5 mt-1.5">
        {dias.map(d => (
          <span key={d.dia} className="flex-1 text-center text-[9px] text-gray-500 leading-none">
            {d.dia.slice(8)}
          </span>
        ))}
      </div>
    </div>
  )
}

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
  const [products, setProducts]         = useState<Product[]>([])
  const [loading, setLoading]           = useState(true)
  const [adding, setAdding]             = useState<string | null>(null)
  const [search, setSearch]             = useState('')
  const [barcodeInput, setBarcodeInput] = useState('')
  const [barcodeLoading, setBarcodeLoading] = useState(false)
  const [mode, setMode]                 = useState<'search' | 'barcode'>('search')
  const [cameraOpen, setCameraOpen]     = useState(false)
  const barcodeRef                      = useRef<HTMLInputElement>(null)

  useEffect(() => {
    productApi.list()
      .then(r => setProducts(r.data.filter(p => p.isActive && p.stockQuantity > 0)))
      .catch(() => toast.error('Erro ao carregar produtos'))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    if (mode === 'barcode') setTimeout(() => barcodeRef.current?.focus(), 100)
  }, [mode])

  async function handleAdd(product: Product) {
    setAdding(product.id)
    try {
      const { data } = await comandaApi.addItem(comandaId, {
        productId:        product.id,
        itemName:         product.name,
        unitPriceInCents: product.priceInCents,
        quantity:         1,
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

  async function handleBarcodeSubmit(e: React.FormEvent) {
    e.preventDefault()
    const code = barcodeInput.trim()
    if (!code) return
    setBarcodeLoading(true)
    try {
      const { data } = await productApi.getByBarcode(code)
      await handleAdd(data)
      setBarcodeInput('')
    } catch {
      toast.error('Produto não encontrado para este código de barras.')
    } finally {
      setBarcodeLoading(false)
      barcodeRef.current?.focus()
    }
  }

  async function handleCameraDetect(code: string) {
    setCameraOpen(false)
    setBarcodeLoading(true)
    try {
      const { data } = await productApi.getByBarcode(code)
      await handleAdd(data)
      setBarcodeInput('')
    } catch {
      toast.error('Produto não encontrado para este código de barras.')
    } finally {
      setBarcodeLoading(false)
    }
  }

  const filtered = products.filter(p =>
    p.name.toLowerCase().includes(search.toLowerCase()) ||
    p.category.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <>
    {cameraOpen && (
      <CameraScanner
        onDetected={handleCameraDetect}
        onClose={() => setCameraOpen(false)}
      />
    )}
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm" onClick={onClose}>
      <div className="bg-surface-700 border border-surface-500 rounded-2xl w-full max-w-md max-h-[75vh] flex flex-col" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between p-4 border-b border-surface-500">
          <h3 className="font-semibold text-white">Adicionar produto à comanda</h3>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-300 transition-colors">
            <XCircle className="w-5 h-5" />
          </button>
        </div>

        {/* Tabs busca / barcode */}
        <div className="flex border-b border-surface-500">
          <button
            onClick={() => setMode('search')}
            className={clsx('flex-1 flex items-center justify-center gap-1.5 py-2.5 text-sm font-medium transition-colors',
              mode === 'search' ? 'text-brand-400 border-b-2 border-brand-400 -mb-px' : 'text-gray-500 hover:text-gray-300')}
          >
            <Search className="w-3.5 h-3.5" /> Buscar
          </button>
          <button
            onClick={() => setMode('barcode')}
            className={clsx('flex-1 flex items-center justify-center gap-1.5 py-2.5 text-sm font-medium transition-colors',
              mode === 'barcode' ? 'text-brand-400 border-b-2 border-brand-400 -mb-px' : 'text-gray-500 hover:text-gray-300')}
          >
            <ScanBarcode className="w-3.5 h-3.5" /> Código de Barras
          </button>
        </div>

        {mode === 'barcode' ? (
          <form onSubmit={handleBarcodeSubmit} className="p-4 space-y-3">
            <p className="text-xs text-gray-500">Clique no campo e escaneie com o leitor USB ou use a câmera.</p>
            <div className="flex gap-2">
              <div className="relative flex-1">
                <ScanBarcode className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                <input
                  ref={barcodeRef}
                  className="input pl-9 font-mono"
                  placeholder="Aguardando leitura..."
                  value={barcodeInput}
                  onChange={e => setBarcodeInput(e.target.value)}
                />
              </div>
              <button
                type="button"
                onClick={() => setCameraOpen(true)}
                className="shrink-0 px-3 rounded-lg bg-surface-600 hover:bg-brand-600/20 border border-surface-500 hover:border-brand-500/40 text-gray-400 hover:text-brand-400 transition-colors"
                title="Usar câmera"
              >
                <Camera className="w-4 h-4" />
              </button>
            </div>
            <button
              type="submit"
              disabled={!barcodeInput.trim() || barcodeLoading}
              className="btn-primary w-full justify-center"
            >
              {barcodeLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />}
              {barcodeLoading ? 'Buscando...' : 'Adicionar'}
            </button>
          </form>
        ) : (
          <>
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
          </>
        )}
      </div>
    </div>
    </>
  )
}

// ── Modal: Admin abre comanda por um cliente ──────────────────────────────────

function AdminOpenModal({
  onConfirm, onCancel,
}: {
  onConfirm: (userId: string, tableIdentifier: string) => Promise<void>
  onCancel:  () => void
}) {
  const [search,   setSearch]   = useState('')
  const [users,    setUsers]    = useState<UserSummary[]>([])
  const [selected, setSelected] = useState<UserSummary | null>(null)
  const [table,    setTable]    = useState('')
  const [loading,  setLoading]  = useState(false)
  const [saving,   setSaving]   = useState(false)

  useEffect(() => {
    setLoading(true)
    userApi.list(search || undefined)
      .then(r => setUsers(r.data))
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [search])

  async function handleConfirm() {
    if (!selected) return
    setSaving(true)
    try {
      await onConfirm(selected.id, table.trim())
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm" onClick={onCancel}>
      <div className="bg-surface-700 border border-surface-500 rounded-2xl w-full max-w-sm flex flex-col max-h-[80vh]" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between p-4 border-b border-surface-500">
          <h3 className="font-semibold text-white flex items-center gap-2">
            <FolderOpen className="w-4 h-4 text-brand-400" /> Abrir Comanda
          </h3>
          <button onClick={onCancel} className="text-gray-500 hover:text-gray-300"><XCircle className="w-5 h-5" /></button>
        </div>

        <div className="p-3 border-b border-surface-500">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input
              autoFocus
              className="input pl-9 text-sm"
              placeholder="Buscar cliente..."
              value={search}
              onChange={e => { setSearch(e.target.value); setSelected(null) }}
            />
          </div>
        </div>

        <div className="flex-1 overflow-y-auto">
          {loading ? (
            <div className="flex justify-center py-8"><Loader2 className="w-5 h-5 animate-spin text-brand-400" /></div>
          ) : users.length === 0 ? (
            <p className="text-center text-gray-500 text-sm py-8">Nenhum cliente encontrado</p>
          ) : (
            users.map(u => (
              <button
                key={u.id}
                onClick={() => setSelected(u)}
                className={clsx(
                  'w-full flex items-center gap-3 px-4 py-3 text-left transition-colors',
                  selected?.id === u.id ? 'bg-brand-600/20 border-l-2 border-brand-500' : 'hover:bg-surface-600'
                )}
              >
                <div className="w-8 h-8 rounded-full bg-brand-600/20 flex items-center justify-center text-brand-400 font-bold text-sm shrink-0">
                  {u.name.charAt(0).toUpperCase()}
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-medium text-white truncate">{u.name}</p>
                  {u.email && <p className="text-xs text-gray-500 truncate">{u.email}</p>}
                </div>
                {selected?.id === u.id && <CheckCircle className="w-4 h-4 text-brand-400 ml-auto shrink-0" />}
              </button>
            ))
          )}
        </div>

        {selected && (
          <div className="p-3 border-t border-surface-500 space-y-3">
            <div>
              <label className="label text-xs">Mesa / Identificador (opcional)</label>
              <input
                className="input text-sm"
                placeholder="Ex: Mesa 3, Balcão..."
                value={table}
                onChange={e => setTable(e.target.value)}
              />
            </div>
            <button
              onClick={handleConfirm}
              disabled={saving}
              className="btn-primary w-full justify-center"
            >
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <FolderOpen className="w-4 h-4" />}
              Abrir para {selected.name.split(' ')[0]}
            </button>
          </div>
        )}
      </div>
    </div>
  )
}

// ── Card de Comanda ───────────────────────────────────────────────────────────

// ── Modal: selecionar pagamento ao fechar comanda ────────────────────────────

function CloseComandaModal({
  comanda, onConfirm, onCancel,
}: {
  comanda:   ComandaDto
  onConfirm: (paymentMethod: string) => void
  onCancel:  () => void
}) {
  const [method, setMethod] = useState('Dinheiro')

  const totalRestante = comanda.totalInReais - comanda.pointsApplied / 100
  const saldoCashback = comanda.userBalanceInCents / 100
  const saldoPontos   = comanda.userPointsBalance

  const semSaldoCashback = method === 'Cashback' && saldoCashback < totalRestante
  const semSaldoPontos   = method === 'Pontos'   && saldoPontos   < comanda.totalInReais * 100 - comanda.pointsApplied
  const bloqueado = semSaldoCashback || semSaldoPontos

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm" onClick={onCancel}>
      <div className="bg-surface-700 border border-surface-500 rounded-2xl w-full max-w-sm p-6 space-y-4" onClick={e => e.stopPropagation()}>
        <div>
          <h3 className="font-semibold text-white text-lg">Fechar comanda</h3>
          <p className="text-gray-400 text-sm mt-1">
            {comanda.userName} · <span className="text-accent-gold font-bold">{`R$ ${comanda.totalInReais.toFixed(2).replace('.', ',')}`}</span>
          </p>
          {/* Saldos do cliente */}
          {(saldoPontos > 0 || saldoCashback > 0) && (
            <div className="flex gap-3 mt-2">
              {saldoPontos > 0 && (
                <span className="text-xs text-amber-400 bg-amber-500/10 border border-amber-500/20 rounded-lg px-2 py-1">
                  {saldoPontos} pts
                </span>
              )}
              {saldoCashback > 0 && (
                <span className="text-xs text-emerald-400 bg-emerald-500/10 border border-emerald-500/20 rounded-lg px-2 py-1">
                  R$ {saldoCashback.toFixed(2).replace('.', ',')} cashback
                </span>
              )}
            </div>
          )}
        </div>

        <div>
          <p className="text-xs text-gray-500 mb-2 font-medium uppercase tracking-wider">Forma de pagamento</p>
          <div className="grid grid-cols-1 gap-2">
            {COMANDA_PAYMENT_METHODS.map(pm => (
              <button
                key={pm.value}
                onClick={() => setMethod(pm.value)}
                className={clsx(
                  'flex items-center gap-3 px-4 py-3 rounded-xl border text-sm font-medium transition-all text-left',
                  method === pm.value
                    ? pm.value === 'Crediario'
                      ? 'bg-amber-500/10 border-amber-500/50 text-amber-300'
                      : 'bg-brand-600/20 border-brand-500/50 text-brand-300'
                    : 'border-surface-500 text-gray-400 hover:border-surface-400 hover:text-gray-200'
                )}
              >
                <CreditCard className="w-4 h-4 shrink-0" />
                {pm.label}
                {pm.value === 'Crediario' && (
                  <span className="ml-auto text-xs text-amber-400/70 font-normal">acumula no saldo</span>
                )}
                {pm.value === 'Cashback' && saldoCashback > 0 && (
                  <span className="ml-auto text-xs text-emerald-400/70 font-normal">
                    R$ {saldoCashback.toFixed(2).replace('.', ',')} disponível
                  </span>
                )}
                {pm.value === 'Pontos' && saldoPontos > 0 && (
                  <span className="ml-auto text-xs text-amber-400/70 font-normal">
                    {saldoPontos} pts disponíveis
                  </span>
                )}
              </button>
            ))}
          </div>
        </div>

        {method === 'Crediario' && (
          <div className="bg-amber-500/5 border border-amber-500/20 rounded-xl p-3 text-xs text-amber-300">
            O valor será acumulado no saldo devedor do cliente. Novas comandas podem ser abertas normalmente.
          </div>
        )}
        {semSaldoCashback && (
          <div className="bg-red-500/10 border border-red-500/20 rounded-xl p-3 text-xs text-red-400">
            Saldo insuficiente. Cliente tem R$ {saldoCashback.toFixed(2).replace('.', ',')} mas a comanda custa R$ {totalRestante.toFixed(2).replace('.', ',')}.
          </div>
        )}
        {semSaldoPontos && (
          <div className="bg-red-500/10 border border-red-500/20 rounded-xl p-3 text-xs text-red-400">
            Pontos insuficientes. Cliente tem {saldoPontos} pts mas a comanda requer {Math.ceil(totalRestante * 100 - comanda.pointsApplied)} pts.
          </div>
        )}

        <div className="flex gap-3 pt-1">
          <button onClick={onCancel} className="btn-secondary flex-1 justify-center">Voltar</button>
          <button
            onClick={() => onConfirm(method)}
            disabled={bloqueado}
            className="btn-success flex-1 justify-center disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <CheckCircle className="w-4 h-4" /> Confirmar
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Modal de confirmação genérico (cancelar) ──────────────────────────────────

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
  onClose:  (id: string, paymentMethod: string) => void
  onCancel: (id: string) => void
  onUpdate: (updated: ComandaDto) => void
  isNew:    boolean
}) {
  const [expanded, setExpanded]   = useState(false)
  const [loading, setLoading]     = useState(false)
  const [addOpen, setAddOpen]     = useState(false)
  const [closeOpen, setCloseOpen] = useState(false)
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

  const statusMap: Record<string, string> = {
    Aberta: 'badge-aberta', EmAndamento: 'badge-andamento',
  }
  const statusLabel: Record<string, string> = {
    Aberta: '● Aberta', EmAndamento: '● Em Andamento',
  }

  async function handleClose(paymentMethod: string) {
    setCloseOpen(false)
    setLoading(true)
    try { await onClose(comanda.id, paymentMethod) } finally { setLoading(false) }
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
      onUpdate(data)
      toast.success('Item removido.')
    } catch {
      toast.error('Erro ao remover item.')
    } finally {
      setRemovingItem(null)
    }
  }
  async function handleRemovePoints() {
    if (!window.confirm('Remover os pontos aplicados e devolver ao saldo do cliente?')) return
    setRemovingPts(true)
    try {
      const { data } = await comandaApi.removePoints(comanda.id)
      onUpdate(data)
      toast.success('Pontos removidos e devolvidos ao cliente!')
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg ?? 'Erro ao remover pontos.')
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
    } catch {
      toast.error('Erro ao atualizar quantidade.')
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
          onAdded={updated => { onUpdate(updated); setAddOpen(false) }}
        />
      )}
      {closeOpen && (
        <CloseComandaModal
          comanda={comanda}
          onConfirm={handleClose}
          onCancel={() => setCloseOpen(false)}
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

// ── Página principal ──────────────────────────────────────────────────────────

export default function DashboardPage() {
  const [tab, setTab]             = useState<'ativas' | 'historico'>('ativas')
  const [comandas, setComandas]   = useState<ComandaDto[]>([])
  const [history, setHistory]     = useState<ComandaDto[]>([])
  const [histData, setHistData]   = useState(() => brToday())
  const [loading, setLoading]     = useState(true)
  const [histLoading, setHistLoad]= useState(false)
  const [connected, setConnected] = useState(false)
  const [newIds, setNewIds]       = useState<Set<string>>(new Set())
  const [search, setSearch]       = useState('')
  const [fin7d, setFin7d]         = useState<FinanceiroDto | null>(null)
  const [finHoje, setFinHoje]     = useState<FinanceiroDto | null>(null)
  const [lowStock, setLowStock]   = useState(0)
  const [allProducts, setAllProducts] = useState<Product[]>([])
  const [ranking, setRanking]     = useState<ClienteInsightDto[]>([])
  const [openModal, setOpenModal] = useState(false)
  const [expandedHist, setExpandedHist] = useState<string | null>(null)
  const prevCountRef              = useRef(0)
  const knownIdsRef               = useRef<Set<string>>(new Set())

  const fetchComandas = useCallback(async () => {
    try {
      const { data } = await comandaApi.dashboard()
      // Detecta novas comandas e toca o som de gol
      data.forEach(c => {
        if (!knownIdsRef.current.has(c.id) && knownIdsRef.current.size > 0) {
          playGoalSound()
        }
        knownIdsRef.current.add(c.id)
      })
      setComandas(data)
    } catch {
      toast.error('Erro ao carregar comandas')
    } finally {
      setLoading(false)
    }
  }, [])

  const fetchHistory = useCallback(async (data?: string) => {
    setHistLoad(true)
    try {
      const { data: res } = await comandaApi.history(data)
      setHistory(res)
    } catch {
      toast.error('Erro ao carregar histórico')
    } finally {
      setHistLoad(false)
    }
  }, [])

  // Carrega dados financeiros e estoque baixo
  useEffect(() => {
    const hoje  = brToday()
    const ini7d = new Date(); ini7d.setDate(ini7d.getDate() - 6)
    const ini7s = new Intl.DateTimeFormat('fr-CA', { timeZone: 'America/Sao_Paulo' }).format(ini7d)

    analyticsApi.financeiro(hoje, hoje).then(r => setFinHoje(r.data)).catch(() => {})
    analyticsApi.financeiro(ini7s, hoje).then(r => setFin7d(r.data)).catch(() => {})
    productApi.list().then(r => {
      const prods = r.data.filter(p => p.isActive)
      setLowStock(prods.filter(p => p.isLowStock).length)
      setAllProducts(prods)
    }).catch(() => {})
    analyticsApi.clientes().then(r => setRanking(r.data.filter(c => c.gastoTotal > 0).slice(0, 5))).catch(() => {})
  }, [])

  useEffect(() => {
    fetchComandas()
    let hub: Awaited<ReturnType<typeof startHub>>

    pedirPermissaoNotificacao()

    startHub().then(h => {
      hub = h
      setConnected(true)

      hub.on('ComandaUpdated', (event: ComandaUpdatedEvent) => {
        setNewIds(s => new Set(s).add(event.comandaId))
        setTimeout(() => setNewIds(s => { const n = new Set(s); n.delete(event.comandaId); return n }), 3000)
        fetchComandas()
        tocarSom('nova')
        incrementBadge()
        notificarBrowser('Nova atividade — Santuário Nerd', `${event.userName}: +${event.lastItemAdded ?? 'item'}`)
        toast(`📋 ${event.userName}: +${event.lastItemAdded ?? 'item'}`, {
          icon: '🃏',
          style: { background: '#1A1A1F', color: '#fff', border: '1px solid #42B6EE', borderRadius: '12px' }
        })
      })

      hub.on('ComandaOpened', () => {
        fetchComandas()
      })

      hub.on('ComandaClosed', () => {
        fetchComandas()
        fetchHistory(histData)
        tocarSom('fechada')
      })
      hub.onclose(() => setConnected(false))
      hub.onreconnecting(() => setConnected(false))
      hub.onreconnected(() => { setConnected(true); fetchComandas() })
    }).catch(() => setConnected(false))

    // ── Polling de segurança — garante dados frescos mesmo se evento falhar ──
    // Roda a cada 30s; se hub caiu, tenta reconectar também
    const poll = setInterval(async () => {
      const { HubConnectionState } = await import('@microsoft/signalr')
      const hub = (await import('@/lib/signalr')).getComandaHub()
      if (hub.state === HubConnectionState.Disconnected) {
        try { await hub.start(); setConnected(true) } catch { /* ignora */ }
      }
      fetchComandas()
    }, 30_000)

    // Limpa badge quando admin foca na aba
    const onFocus = () => clearBadge()
    window.addEventListener('focus', onFocus)

    return () => {
      clearInterval(poll)
      stopHub()
      window.removeEventListener('focus', onFocus)
    }
  }, [fetchComandas, fetchHistory])

  useEffect(() => {
    if (tab === 'historico') fetchHistory(histData)
  }, [tab, histData, fetchHistory])

  useEffect(() => {
    if (comandas.length > prevCountRef.current && prevCountRef.current > 0)
      toast('🎉 Nova comanda aberta!', { duration: 3000 })
    prevCountRef.current = comandas.length
  }, [comandas.length])

  function handleUpdate(updated: ComandaDto) {
    setComandas(prev => prev.map(c => c.id === updated.id ? updated : c))
  }

  async function handleClose(id: string, paymentMethod: string) {
    try {
      await comandaApi.close(id, paymentMethod)
      const label = paymentMethod === 'Crediario' ? 'Comanda fechada no crediário!' : 'Comanda fechada!'
      toast.success(label)
      fetchComandas()
      fetchHistory(histData)
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg ?? 'Erro ao fechar comanda.')
    }
  }
  async function handleCancel(id: string) {
    try {
      await comandaApi.cancel(id)
      toast.success('Comanda cancelada.')
      fetchComandas()
      fetchHistory(histData)
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg ?? 'Erro ao cancelar comanda.')
    }
  }

  async function handleAdminOpen(userId: string, tableIdentifier: string) {
    try {
      await comandaApi.adminOpen(userId, tableIdentifier || undefined)
      toast.success('Comanda aberta!')
      setOpenModal(false)
      fetchComandas()
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg ?? 'Erro ao abrir comanda.')
    }
  }

  const patrimonioCusto  = allProducts.reduce((s, p) => s + p.costPriceInCents  * p.stockQuantity, 0) / 100
  const patrimonioVenda  = allProducts.reduce((s, p) => s + p.priceInCents      * p.stockQuantity, 0) / 100
  const lucroEstoque     = patrimonioVenda - patrimonioCusto
  const totalPecas       = allProducts.reduce((s, p) => s + p.stockQuantity, 0)

  const totalAberto  = comandas.reduce((s, c) => s + c.totalInReais, 0)
  const emAndamento  = comandas.filter(c => c.status === 'EmAndamento').length
  const fechadas     = history.filter(c => c.status === 'Fechada')
  const totalFechado = fechadas.reduce((s, c) => s + c.totalInReais, 0)

  const paymentBreakdown = [
    { key: 'Dinheiro',      label: 'Dinheiro',  color: 'text-accent-green' },
    { key: 'Pix',           label: 'Pix',        color: 'text-brand-400' },
    { key: 'CartaoCredito', label: 'Crédito',    color: 'text-amber-400' },
    { key: 'CartaoDebito',  label: 'Débito',     color: 'text-blue-400' },
    { key: 'Crediario',     label: 'Crediário',  color: 'text-red-400'    },
    { key: 'Pontos',        label: 'Pontos',     color: 'text-amber-400'  },
    { key: 'Cashback',      label: 'Cashback',   color: 'text-purple-400' },
  ].map(pm => ({
    ...pm,
    total: fechadas
      .filter(c => c.paymentMethod === pm.key)
      .reduce((s, c) => s + c.totalInReais, 0),
  })).filter(pm => pm.total > 0)

  const filtered = comandas.filter(c =>
    !search || c.userName.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <div className="p-4 sm:p-6 space-y-4 sm:space-y-5">
      {openModal && (
        <AdminOpenModal
          onConfirm={handleAdminOpen}
          onCancel={() => setOpenModal(false)}
        />
      )}

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
          <button onClick={() => setOpenModal(true)} className="btn-primary text-sm py-1.5">
            <FolderOpen className="w-4 h-4" /> <span className="hidden sm:inline">Abrir Comanda</span>
          </button>
          <button onClick={fetchComandas} className="btn-secondary text-sm py-1.5">
            <RefreshCw className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Métricas ao vivo */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {[
          { label: 'Comandas Ativas',  value: String(comandas.length),  icon: Users,          color: 'text-brand-400',   bg: 'bg-brand-600/10'   },
          { label: 'Em Aberto',        value: fmt(totalAberto),          icon: Clock,          color: 'text-red-400',     bg: 'bg-red-500/10'     },
          { label: 'Receita Hoje',     value: finHoje ? fmt(finHoje.receita) : '—', icon: DollarSign, color: 'text-emerald-400', bg: 'bg-emerald-500/10' },
          { label: 'Estoque Baixo',    value: String(lowStock),          icon: AlertTriangle,  color: lowStock > 0 ? 'text-red-400' : 'text-gray-400', bg: lowStock > 0 ? 'bg-red-500/10' : 'bg-surface-600' },
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

      {/* KPIs financeiros hoje — sempre visíveis quando finHoje carregou */}
      {finHoje && (
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          {(() => {
            const totalTx = (finHoje.pagamentosPorForma ?? []).reduce((s, f) => s + f.quantidade, 0)
            const ticketMedio = totalTx > 0 ? finHoje.receita / totalTx : 0
            return [
              { label: 'Receita Total',   value: fmt(finHoje.receita), icon: Banknote,   color: 'text-accent-gold' },
              { label: 'Custo Hoje',     value: fmt(finHoje.custo),  icon: TrendingUp, color: 'text-red-400'     },
              { label: 'Margem Hoje',    value: fmt(finHoje.margem), icon: TrendingUp, color: finHoje.margem >= 0 ? 'text-emerald-400' : 'text-red-400' },
              { label: 'Ticket Médio',   value: fmt(ticketMedio),    icon: CreditCard, color: 'text-brand-400'   },
            ]
          })().map(m => (
            <div key={m.label} className="card flex items-center gap-3 py-2.5">
              <m.icon className={clsx('w-4 h-4 shrink-0', m.color)} />
              <div className="min-w-0">
                <p className={clsx('text-base font-bold font-mono truncate', m.color)}>{m.value}</p>
                <p className="text-xs text-gray-500">{m.label}</p>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Gráfico 7 dias + Ranking lado a lado em telas grandes */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">

        {/* Coluna esquerda: gráfico + patrimônio */}
        <div className="flex flex-col gap-5">
          {fin7d && fin7d.diaDia.length > 1 && <MiniBarChart dias={fin7d.diaDia} />}

          {allProducts.length > 0 && (
            <div className="card">
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
                  <Package className="w-4 h-4 text-emerald-400" /> Patrimônio em Estoque
                </h3>
                <a href="/admin/estoque" className="text-xs text-brand-400 hover:text-brand-300 transition-colors">
                  Ver estoque →
                </a>
              </div>
              <div className="grid grid-cols-2 gap-2.5">
                <div className="bg-surface-800 rounded-xl p-3">
                  <p className="text-xs text-gray-500 mb-1">Custo total</p>
                  <p className="text-base font-bold text-white font-mono">{fmt(patrimonioCusto)}</p>
                </div>
                <div className="bg-surface-800 rounded-xl p-3">
                  <p className="text-xs text-gray-500 mb-1">Valor de venda</p>
                  <p className="text-base font-bold text-accent-gold font-mono">{fmt(patrimonioVenda)}</p>
                </div>
                <div className="bg-surface-800 rounded-xl p-3">
                  <p className="text-xs text-gray-500 mb-1">Lucro potencial</p>
                  <p className={`text-base font-bold font-mono ${lucroEstoque >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                    {fmt(lucroEstoque)}
                  </p>
                </div>
                <div className="bg-surface-800 rounded-xl p-3">
                  <p className="text-xs text-gray-500 mb-1">Total de peças</p>
                  <p className="text-base font-bold text-brand-400 font-mono">{totalPecas.toLocaleString('pt-BR')}</p>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Ranking de clientes — sempre visível */}
        <div className="card">
          <div className="flex items-center justify-between mb-3">
            <h3 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
              <Trophy className="w-4 h-4 text-accent-gold" /> Top Clientes
            </h3>
            <a href="/admin/usuarios" className="text-xs text-brand-400 hover:text-brand-300 transition-colors">
              Ver todos →
            </a>
          </div>
          {ranking.length === 0 ? (
            <p className="text-xs text-gray-500 py-4 text-center">Nenhuma compra registrada ainda</p>
          ) : (
            <div className="space-y-2">
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
                          <span className={clsx(
                            'text-[10px] font-medium px-1.5 py-0.5 rounded-full border',
                            c.pontosVencemEm < 0
                              ? 'bg-red-500/15 text-red-400 border-red-500/20'
                              : 'bg-orange-500/15 text-orange-400 border-orange-500/20'
                          )}>
                            {c.pontosVencemEm < 0
                              ? `${c.pontos}pts vencidos`
                              : `${c.pontos}pts vencem em ${c.pontosVencemEm}d`}
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="flex items-center gap-2 shrink-0">
                      <div className="text-right">
                        <p className="text-sm font-bold text-accent-gold font-mono">{fmt(c.gastoTotal)}</p>
                      </div>
                      {c.whatsApp && (
                        <a
                          href={`https://wa.me/${c.whatsApp.replace(/\D/g, '')}`}
                          target="_blank"
                          rel="noopener noreferrer"
                          onClick={e => e.stopPropagation()}
                          className="w-7 h-7 flex items-center justify-center rounded-lg bg-emerald-600/20 hover:bg-emerald-600/40 text-emerald-400 transition-colors"
                          title={`WhatsApp: ${c.whatsApp}`}
                        >
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
          )}
        </div>
      </div>

      {/* Breakdown por pagamento — só aparece quando há histórico */}
      {tab === 'historico' && paymentBreakdown.length > 0 && (
        <div className="card">
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Fechamento por forma de pagamento</p>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-5 gap-3">
            {paymentBreakdown.map(pm => (
              <div key={pm.key} className="bg-surface-800 rounded-xl p-3 text-center">
                <p className={`text-lg font-bold ${pm.color}`}>{fmt(pm.total)}</p>
                <p className="text-xs text-gray-500 mt-0.5">{pm.label}</p>
              </div>
            ))}
            <div className="bg-surface-800 rounded-xl p-3 text-center border border-surface-500">
              <p className="text-lg font-bold text-accent-gold">{fmt(totalFechado)}</p>
              <p className="text-xs text-gray-500 mt-0.5">Total</p>
            </div>
          </div>
        </div>
      )}


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
            <History className="w-4 h-4 inline mr-1.5" />Histórico
          </button>
        </div>

        {tab === 'historico' && (
          <input
            type="date"
            value={histData}
            max={brToday()}
            onChange={e => setHistData(e.target.value)}
            className="input text-sm w-40 py-1.5"
          />
        )}

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
              <Users className="w-8 h-8 text-gray-400" />
            </div>
            <p className="text-gray-400 font-medium">
              {search ? `Nenhuma comanda para "${search}"` : 'Nenhuma comanda aberta no momento'}
            </p>
            {!search && <p className="text-gray-400 text-sm mt-1">Clientes acessam via QR Code nas mesas</p>}
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
              <History className="w-8 h-8 text-gray-400" />
            </div>
            <p className="text-gray-400 font-medium">Nenhuma comanda encerrada neste dia</p>
          </div>
        ) : (
          <div className="space-y-2">
            {history.map(c => {
              const isExpanded = expandedHist === c.id
              return (
                <div key={c.id} className="card">
                  <button
                    onClick={() => setExpandedHist(isExpanded ? null : c.id)}
                    className="w-full flex items-center justify-between gap-4 text-left"
                  >
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
                          {c.paymentMethod && (
                            <>
                              <span>·</span>
                              <span className="text-gray-400 font-medium">
                                {COMANDA_PAYMENT_METHODS.find(m => m.value === c.paymentMethod)?.label ?? c.paymentMethod}
                              </span>
                            </>
                          )}
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2 shrink-0">
                      <div className="text-right">
                        <p className={clsx('font-bold', c.status === 'Fechada' ? 'text-accent-gold' : 'text-gray-500')}>
                          {fmt(c.totalInReais)}
                        </p>
                        <p className={clsx('text-xs', c.status === 'Fechada' ? 'text-accent-green' : 'text-red-400')}>
                          {c.status === 'Fechada' ? 'Fechada' : 'Cancelada'}
                        </p>
                      </div>
                      {c.items.length > 0 && (
                        isExpanded
                          ? <ChevronUp className="w-4 h-4 text-gray-500" />
                          : <ChevronDown className="w-4 h-4 text-gray-500" />
                      )}
                    </div>
                  </button>

                  {isExpanded && c.items.length > 0 && (
                    <div className="mt-3 pt-3 border-t border-surface-600 space-y-1">
                      {c.items.map(item => (
                        <div key={item.id} className="flex items-center justify-between text-xs text-gray-300 py-0.5">
                          <span className="flex-1 truncate">{item.quantity}× {item.itemNameSnapshot}</span>
                          <span className="text-gray-500 ml-2 shrink-0">{fmt(item.subtotalInReais)}</span>
                        </div>
                      ))}
                      <div className="flex justify-between text-xs font-semibold text-gray-200 pt-1 border-t border-surface-600">
                        <span>Total</span>
                        <span className="text-accent-gold">{fmt(c.totalInReais)}</span>
                      </div>
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        )
      )}
    </div>
  )
}
