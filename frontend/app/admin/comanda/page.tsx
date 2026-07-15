'use client'
import { useEffect, useState, useCallback, useRef, useMemo } from 'react'
import { comandaApi, crediarioApi, userApi, productApi, fiscalApi, ComandaDto, UserSummary, Product, COMANDA_PAYMENT_METHODS, EditarComandaRequest, CrediariosDto, getErrorMessage } from '@/lib/api'
import { usePreferences } from '@/hooks/usePreferences'
import { startHub, stopHub, ComandaUpdatedEvent } from '@/lib/signalr'
import { playGoalSound } from '@/lib/sounds'
import { tocarSom, notificarBrowser, pedirPermissaoNotificacao, incrementBadge, clearBadge } from '@/lib/notificacoes'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import PageHeader from '@/components/admin/PageHeader'
import StatCard from '@/components/admin/StatCard'
import toast from 'react-hot-toast'
import {
  Wifi, WifiOff, RefreshCw, Users, Clock, CheckCircle, ChevronDown, ChevronUp,
  History, Search, Loader2, Trash2, FolderOpen, Pencil, Receipt,
} from 'lucide-react'
import clsx from 'clsx'
import { fmt, brToday, handleNotaFiscalResult } from '@/components/admin/comanda/shared'
import { AdminOpenModal } from '@/components/admin/comanda/AdminOpenModal'
import { ComandaCard } from '@/components/admin/comanda/ComandaCard'
import { EditarComandaModal } from '@/components/admin/comanda/EditarComandaModal'
import { EscolherContaCrediarioModal } from '@/components/admin/comanda/EscolherContaCrediarioModal'
import { ComandaReceiptModal } from '@/components/admin/comanda/ComandaReceiptModal'

// ── Página principal ──────────────────────────────────────────────────────────

export default function ComandaPage() {
  const { prefs } = usePreferences()
  const dp = prefs.dashboard
  const { site } = useSiteConfig()
  const siteNameRef = useRef(site.siteName)
  useEffect(() => { siteNameRef.current = site.siteName }, [site.siteName])
  const [subTab, setSubTab]       = useState<'ativas' | 'historico'>('ativas')
  const [comandas, setComandas]   = useState<ComandaDto[]>([])
  const [history, setHistory]     = useState<ComandaDto[]>([])
  const [histData, setHistData]   = useState(() => brToday())
  const [loading, setLoading]     = useState(true)
  const [histLoading, setHistLoad]= useState(false)
  const [connected, setConnected] = useState(false)
  const [newIds, setNewIds]       = useState<Set<string>>(new Set())
  const [recentChanges, setRecentChanges] = useState<Map<string, { type: 'add' | 'remove'; at: number }>>(new Map())
  const [search, setSearch]       = useState('')
  const [allUsers, setAllUsers]   = useState<UserSummary[]>([])
  const [allProducts, setAllProducts] = useState<Product[]>([])
  const [openModal, setOpenModal] = useState(false)
  const [expandedHist, setExpandedHist] = useState<string | null>(null)
  const [editComanda, setEditComanda]   = useState<ComandaDto | null>(null)
  const [closedReceipt, setClosedReceipt] = useState<ComandaDto | null>(null)
  // Crediário — escolha de conta ao fechar comanda
  const [pendingClose, setPendingClose] = useState<{
    id: string; pm: string; pm2?: string; amt2?: number; discount?: number; emitirNota?: boolean
    userId: string; userName: string; valorPrincipal: number
  } | null>(null)
  const [autoEmitMethods, setAutoEmitMethods] = useState<string[]>([])
  const [emitindoNotaId, setEmitindoNotaId]   = useState<string | null>(null)
  const [contasAbertas, setContasAbertas] = useState<CrediariosDto[]>([])
  const [histSearch,   setHistSearch]   = useState('')
  const [histHoraDe,   setHistHoraDe]   = useState('')
  const [histHoraAte,  setHistHoraAte]  = useState('')
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
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao carregar comandas'))
    } finally {
      setLoading(false)
    }
  }, [])

  const fetchHistory = useCallback(async (data?: string) => {
    setHistLoad(true)
    try {
      const { data: res } = await comandaApi.history(data)
      setHistory(res)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao carregar histórico'))
    } finally {
      setHistLoad(false)
    }
  }, [])

  // Config de emissão automática de nota fiscal (usada pelo fechamento de comanda)
  useEffect(() => {
    fiscalApi.getConfig().then(r => setAutoEmitMethods(r.data.formasPagamentoAutoEmissao ?? [])).catch(() => {})
    productApi.listAdmin().then(r => setAllProducts(r.data.filter(p => p.isActive))).catch(() => {})
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
        incrementBadge(siteNameRef.current)
        notificarBrowser(`Nova atividade — ${siteNameRef.current}`, `${event.userName}: +${event.lastItemAdded ?? 'item'}`)
        toast(`📋 ${event.userName}: +${event.lastItemAdded ?? 'item'}`, {
          icon: '🔔',
          style: { background: '#1A1A1F', color: '#fff', border: '1px solid rgb(var(--brand-500))', borderRadius: '12px' }
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

    // Limpa badge quando admin foca na aba
    const onFocus = () => clearBadge(siteNameRef.current)
    window.addEventListener('focus', onFocus)

    return () => {
      stopHub()
      window.removeEventListener('focus', onFocus)
    }
  }, [fetchComandas, fetchHistory])

  // Polling separado — reage em tempo real quando o usuário muda o intervalo nas configurações
  useEffect(() => {
    if (dp.refreshInterval === 0) return
    const intervalMs = dp.refreshInterval * 1000
    const poll = setInterval(async () => {
      const { HubConnectionState } = await import('@microsoft/signalr')
      const hub = (await import('@/lib/signalr')).getComandaHub()
      if (hub.state === HubConnectionState.Disconnected) {
        try { await hub.start(); setConnected(true) } catch { /* ignora */ }
      }
      fetchComandas()
    }, intervalMs)
    return () => clearInterval(poll)
  }, [dp.refreshInterval, fetchComandas])

  useEffect(() => {
    if (subTab === 'historico') fetchHistory(histData)
  }, [subTab, histData, fetchHistory])

  useEffect(() => {
    if (comandas.length > prevCountRef.current && prevCountRef.current > 0)
      toast('🎉 Nova comanda aberta!', { duration: 3000 })
    prevCountRef.current = comandas.length
  }, [comandas.length])

  function markChange(id: string, type: 'add' | 'remove') {
    const now = Date.now()
    setRecentChanges(prev => new Map(prev).set(id, { type, at: now }))
    setTimeout(() => setRecentChanges(prev => {
      const next = new Map(prev)
      if (next.get(id)?.at === now) next.delete(id)
      return next
    }), 5 * 60 * 1000) // 5 minutos
  }

  function handleUpdate(updated: ComandaDto, changeType?: 'add' | 'remove') {
    setComandas(prev => prev.map(c => c.id === updated.id ? updated : c))
    if (changeType) markChange(updated.id, changeType)
  }

  async function handleClose(id: string, paymentMethod: string, secondMethod?: string, secondAmountInCents?: number, discountInCents?: number, emitirNotaFiscal?: boolean) {
    if (paymentMethod === 'Crediario') {
      // Descobre o cliente da comanda
      const comanda = comandas.find(c => c.id === id)
      if (comanda?.userId) {
        try {
          const { data } = await crediarioApi.byUser(comanda.userId)
          const abertas = data.filter(c => c.status === 'Aberto')
          if (abertas.length > 0) {
            // Calcula valor principal (total - desconto - segundo pagamento)
            const totalCents = comanda.items.reduce((s, i) => s + i.unitPriceInCents * i.quantity, 0)
            const valorPrincipal = totalCents - (discountInCents ?? 0) - (secondAmountInCents ?? 0)
            setPendingClose({ id, pm: paymentMethod, pm2: secondMethod, amt2: secondAmountInCents, discount: discountInCents, emitirNota: emitirNotaFiscal, userId: comanda.userId, userName: comanda.userName, valorPrincipal })
            setContasAbertas(abertas)
            return
          }
        } catch { /* se falhar na busca, fecha normalmente */ }
      }
    }
    await executarClose(id, paymentMethod, secondMethod, secondAmountInCents, undefined, discountInCents, emitirNotaFiscal)
  }

  async function executarClose(id: string, paymentMethod: string, secondMethod?: string, secondAmountInCents?: number, crediarioExistenteId?: string, discountInCents?: number, emitirNotaFiscal?: boolean) {
    try {
      const { data } = await comandaApi.close(id, paymentMethod, undefined, secondMethod, secondAmountInCents, crediarioExistenteId, discountInCents, emitirNotaFiscal)
      const label = paymentMethod === 'Crediario' ? 'Comanda fechada no crediário!' : 'Comanda fechada!'
      toast.success(label)
      handleNotaFiscalResult(data.notaFiscalId, data.notaFiscalStatus, data.notaFiscalMotivoRejeicao)
      // Comprovante não-fiscal sempre disponível — nem toda loja tem módulo
      // Fiscal, e nem todo fechamento pede NFC-e; sem isso, fechar sem nota
      // não deixava nenhum papel/PDF pra provar a venda.
      setClosedReceipt(data)
      fetchComandas()
      fetchHistory(histData)
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg ?? 'Erro ao fechar comanda.')
    }
  }
  async function openEditModal(c: ComandaDto) {
    setEditComanda(c)
    if (allUsers.length === 0) {
      try { const r = await userApi.list(); setAllUsers(r.data) } catch {}
    }
  }

  async function handleEditar(req: EditarComandaRequest) {
    if (!editComanda) return
    try {
      await comandaApi.editar(editComanda.id, req)
      toast.success('Comanda atualizada!')
      setEditComanda(null)
      fetchHistory(histData)
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg ?? 'Erro ao editar comanda.')
      throw err
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

  async function emitirNotaComanda(id: string) {
    setEmitindoNotaId(id)
    try {
      const { data } = await fiscalApi.emitirNotaComanda(id)
      if (data.status === 'Autorizada') {
        toast.success('Nota fiscal autorizada!')
        window.open(`/admin/fiscal/cupom/${data.id}`, '_blank')
      } else {
        toast.error(`Nota registrada, aguardando: ${data.status}${data.motivoRejeicao ? ' — ' + data.motivoRejeicao : ''}`)
      }
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg ?? 'Erro ao emitir nota fiscal.')
    } finally {
      setEmitindoNotaId(null)
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

  const { totalAberto } = useMemo(() => ({
    totalAberto: comandas.reduce((s, c) => s + c.totalInReais, 0),
  }), [comandas])

  const filteredHistory = history.filter(c => {
    if (histSearch && !c.userName.toLowerCase().includes(histSearch.toLowerCase())) return false
    if ((histHoraDe || histHoraAte) && c.closedAt) {
      const t = new Date(c.closedAt).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'America/Sao_Paulo' })
      if (histHoraDe && t < histHoraDe) return false
      if (histHoraAte && t > histHoraAte) return false
    }
    return true
  })

  const fechadas     = filteredHistory.filter(c => c.status === 'Fechada')
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
    // Para split payment, calcula o valor real de cada método:
    // primaryAmt = total (já líquido de pontos/desconto no fechamento) - valor do segundo método
    // secondAmt  = secondPaymentAmountInCents / 100
    total: fechadas.reduce((sum, c) => {
      const net        = c.totalInReais // totalInReais já sai líquido de pontos/desconto do CloseComandaAsync
      const hasSecond  = !!c.secondPaymentMethod && c.secondPaymentAmountInCents > 0
      const secondAmt  = c.secondPaymentAmountInCents / 100
      const primaryAmt = hasSecond ? net - secondAmt : net
      let contrib = 0
      if (c.paymentMethod       === pm.key) contrib += primaryAmt
      if (c.secondPaymentMethod === pm.key) contrib += secondAmt
      return sum + contrib
    }, 0),
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

      <PageHeader
        icon={Users}
        title="Comanda"
        description="Comandas abertas em tempo real"
        actions={
          <>
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
          </>
        }
      />

      {/* KPIs */}
      <div className="grid grid-cols-2 gap-3 sm:gap-4">
        <StatCard icon={Users} label="Ativas" value={comandas.length} tone="brand" />
        <StatCard icon={Clock} label="Em aberto" value={fmt(totalAberto)} tone="warning" />
      </div>

      {/* Sub-abas + controles */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
        <div className="flex gap-1 bg-surface-800 p-1 rounded-lg w-full sm:w-auto">
          <button
            onClick={() => setSubTab('ativas')}
            className={clsx('flex-1 sm:flex-none flex items-center justify-center gap-1.5 px-3 sm:px-4 py-1.5 rounded-md text-xs sm:text-sm font-medium transition-all',
              subTab === 'ativas' ? 'bg-brand-600 text-white' : 'text-gray-400 hover:text-gray-200')}
          >
            <Users className="w-3.5 h-3.5 sm:w-4 sm:h-4" /><span>Ativas ({comandas.length})</span>
          </button>
          <button
            onClick={() => setSubTab('historico')}
            className={clsx('flex-1 sm:flex-none flex items-center justify-center gap-1.5 px-3 sm:px-4 py-1.5 rounded-md text-xs sm:text-sm font-medium transition-all',
              subTab === 'historico' ? 'bg-brand-600 text-white' : 'text-gray-400 hover:text-gray-200')}
          >
            <History className="w-3.5 h-3.5 sm:w-4 sm:h-4" /><span>Histórico</span>
          </button>
        </div>

        {subTab === 'historico' && (
          <input
            type="date"
            value={histData}
            max={brToday()}
            onChange={e => setHistData(e.target.value)}
            className="input text-sm w-full sm:w-40 py-1.5"
          />
        )}

        {subTab === 'ativas' && (
          <div className="relative w-full sm:w-auto">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input
              className="input pl-9 text-sm w-full sm:w-56"
              placeholder="Buscar por cliente..."
              value={search}
              onChange={e => setSearch(e.target.value)}
            />
          </div>
        )}
      </div>

      {/* ── Sub-aba: Ativas ──────────────────────────────────────────────────── */}
      {subTab === 'ativas' && (
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
                onClosedExternally={() => { fetchComandas(); fetchHistory(histData) }}
                isNew={newIds.has(c.id)}
                recentChange={recentChanges.get(c.id)?.type ?? null}
                autoEmitMethods={autoEmitMethods}
                fiscalEnabled={site.enabledModules.includes('fiscal')}
              />
            ))}
          </div>
        )
      )}

      {/* ── Sub-aba: Histórico ───────────────────────────────────────────────── */}
      {subTab === 'historico' && (
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
          <div className="space-y-4">
            {/* Filtros do histórico */}
            <div className="card py-2.5 px-3 flex flex-col sm:flex-row sm:flex-wrap sm:items-center gap-2">
              <div className="relative flex-1 min-w-0 sm:min-w-36">
                <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-gray-500" />
                <input
                  className="input pl-8 text-sm py-1.5 w-full"
                  placeholder="Filtrar por nome..."
                  value={histSearch}
                  onChange={e => setHistSearch(e.target.value)}
                />
              </div>
              <div className="flex items-center gap-1.5">
                <Clock className="w-3.5 h-3.5 text-gray-500 shrink-0" />
                <input
                  type="time"
                  value={histHoraDe}
                  onChange={e => setHistHoraDe(e.target.value)}
                  className="input text-sm py-1.5 flex-1 sm:w-28 sm:flex-none"
                  title="Horário de"
                />
                <span className="text-xs text-gray-500">até</span>
                <input
                  type="time"
                  value={histHoraAte}
                  onChange={e => setHistHoraAte(e.target.value)}
                  className="input text-sm py-1.5 flex-1 sm:w-28 sm:flex-none"
                  title="Horário até"
                />
              </div>
              {(histSearch || histHoraDe || histHoraAte) && (
                <button
                  onClick={() => { setHistSearch(''); setHistHoraDe(''); setHistHoraAte('') }}
                  className="text-xs text-gray-500 hover:text-gray-300 transition-colors px-2.5 py-1.5 rounded-lg border border-surface-500 hover:border-surface-400 w-full sm:w-auto text-center"
                >
                  Limpar filtros
                </button>
              )}
            </div>

            {/* Breakdown por pagamento */}
            {paymentBreakdown.length > 0 && (
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

            {/* Lista de comandas */}
            <div className="space-y-2">
              {filteredHistory.length === 0 && (
                <div className="flex flex-col items-center justify-center py-12 text-center">
                  <Search className="w-8 h-8 text-gray-600 mb-3" />
                  <p className="text-gray-500 text-sm">Nenhuma comanda encontrada com esses filtros</p>
                  <button onClick={() => { setHistSearch(''); setHistHoraDe(''); setHistHoraAte('') }} className="mt-2 text-xs text-brand-400 hover:text-brand-300 transition-colors">Limpar filtros</button>
                </div>
              )}
              {filteredHistory.map(c => {
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
                                  {c.secondPaymentMethod && ` + ${COMANDA_PAYMENT_METHODS.find(m => m.value === c.secondPaymentMethod)?.label ?? c.secondPaymentMethod}`}
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
                        {c.status === 'Fechada' && (
                          <button
                            onClick={e => { e.stopPropagation(); openEditModal(c) }}
                            className="mt-2 w-full flex items-center justify-center gap-1.5 text-xs font-semibold text-brand-400 hover:text-brand-300 hover:bg-brand-600/10 border border-brand-600/30 hover:border-brand-500/50 rounded-xl py-1.5 transition-colors">
                            <Pencil className="w-3.5 h-3.5" /> Editar comanda
                          </button>
                        )}
                        {c.status === 'Fechada' && (
                          <button
                            disabled={emitindoNotaId === c.id}
                            onClick={e => { e.stopPropagation(); emitirNotaComanda(c.id) }}
                            className="mt-1.5 w-full flex items-center justify-center gap-1.5 text-xs font-semibold text-amber-400 hover:text-amber-300 hover:bg-amber-600/10 border border-amber-600/30 hover:border-amber-500/50 rounded-xl py-1.5 transition-colors disabled:opacity-50">
                            {emitindoNotaId === c.id
                              ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                              : <Receipt className="w-3.5 h-3.5" />}
                            Emitir nota fiscal
                          </button>
                        )}
                        {c.status === 'Fechada' && c.paymentMethod && (() => {
                          const net        = c.totalInReais // já líquido de pontos/desconto (CloseComandaAsync)
                          const hasSecond  = !!c.secondPaymentMethod && c.secondPaymentAmountInCents > 0
                          const secondAmt  = c.secondPaymentAmountInCents / 100
                          const primaryAmt = hasSecond ? net - secondAmt : net
                          const pmLabel    = (key: string) => COMANDA_PAYMENT_METHODS.find(m => m.value === key)?.label ?? key
                          return (
                            <div className="space-y-0.5 pt-1">
                              {c.pointsApplied > 0 && (
                                <div className="flex justify-between text-xs text-gray-500">
                                  <span>Pontos aplicados</span>
                                  <span className="text-amber-400">− {fmt(c.pointsApplied / 100)}</span>
                                </div>
                              )}
                              {c.discountInCents > 0 && (
                                <div className="flex justify-between text-xs text-gray-500">
                                  <span>Desconto</span>
                                  <span className="text-accent-green">− {fmt(c.discountInCents / 100)}</span>
                                </div>
                              )}
                              <div className="flex justify-between text-xs text-gray-500">
                                <span>{pmLabel(c.paymentMethod)}</span>
                                <span>{fmt(primaryAmt)}</span>
                              </div>
                              {hasSecond && (
                                <div className="flex justify-between text-xs text-gray-500">
                                  <span>{pmLabel(c.secondPaymentMethod!)}</span>
                                  <span>{fmt(secondAmt)}</span>
                                </div>
                              )}
                            </div>
                          )
                        })()}
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          </div>
        )
      )}

      {/* Modal editar comanda fechada */}
      {editComanda && (
        <EditarComandaModal
          comanda={editComanda}
          clientes={allUsers}
          produtos={allProducts}
          onSave={handleEditar}
          onClose={() => setEditComanda(null)}
        />
      )}

      {pendingClose && (
        <EscolherContaCrediarioModal
          userName={pendingClose.userName}
          contasAbertas={contasAbertas}
          valorNovo={pendingClose.valorPrincipal}
          onEscolher={async (credId) => {
            setPendingClose(null)
            await executarClose(pendingClose.id, pendingClose.pm, pendingClose.pm2, pendingClose.amt2, credId, pendingClose.discount, pendingClose.emitirNota)
          }}
          onNova={async () => {
            setPendingClose(null)
            await executarClose(pendingClose.id, pendingClose.pm, pendingClose.pm2, pendingClose.amt2, undefined, pendingClose.discount, pendingClose.emitirNota)
          }}
          onCancel={() => setPendingClose(null)}
        />
      )}

      {closedReceipt && (
        <ComandaReceiptModal
          comanda={closedReceipt}
          siteName={site.siteName}
          onClose={() => setClosedReceipt(null)}
        />
      )}
    </div>
  )
}
