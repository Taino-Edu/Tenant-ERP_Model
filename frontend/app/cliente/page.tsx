'use client'
import { useEffect, useState, useCallback } from 'react'
import { comandaApi, userApi, productApi, categoryApi, ComandaDto, Product, ProductCategory, UserProfile } from '@/lib/api'
import { getUserName } from '@/lib/auth'
import { startHub, stopHub, ComandaOpenedEvent } from '@/lib/signalr'
import toast, { Toaster } from 'react-hot-toast'
import { 
  ShoppingCart, Plus, Trash2, Loader2, Clock, 
  TableProperties, Receipt, PackageOpen, Star,
  Layout, BookOpen, Settings2, User as UserIcon
} from 'lucide-react'
import ThemeToggle from '@/components/ThemeToggle'
import Link from 'next/link'
import clsx from 'clsx'


export default function ClientePage() {
  const [comanda, setComanda]         = useState<ComandaDto | null>(null)
  const [products, setProducts]       = useState<Product[]>([])
  const [categories, setCategories]   = useState<ProductCategory[]>([])
  const [profile, setProfile]         = useState<UserProfile | null>(null)
  const [loading, setLoading]         = useState(true)
  const [adding, setAdding]           = useState<string | null>(null)
  const [applyingPts, setApplyingPts] = useState(false)
  const [removingPts, setRemovingPts] = useState(false)

  // Opção de estilo visual (opcional para o cliente)
  const [immersiveMode, setImmersiveMode] = useState(false)

  const fetchComanda = useCallback(async () => {
    try { 
      const { data } = await comandaApi.myComanda()
      setComanda(data) 
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status
      if (status === 404) setComanda(null)
    } finally { 
      setLoading(false) 
    }
  }, [])

  useEffect(() => {
    // Recupera preferência de tema do localstorage
    const savedMode = localStorage.getItem('immersive-mode')
    if (savedMode === 'true') setImmersiveMode(true)

    fetchComanda()
    productApi.list().then(r => setProducts(r.data)).catch(() => {})
    categoryApi.list().then(r => setCategories(r.data)).catch(() => {})
    userApi.me().then(r => setProfile(r.data)).catch(() => {})

    startHub().then(hub => {
      hub.on('ComandaOpened', async (data: ComandaOpenedEvent) => {
        await hub.invoke('JoinComandaGroup', data.comandaId).catch(() => {})
        await fetchComanda()
        toast.success('Sua comanda foi aberta! 🎉', { duration: 5000 })
      })
      hub.on('ComandaClosed', () => {
        toast.success('Sua comanda foi fechada! Obrigado pela visita 🎉', { duration: 6000 })
        fetchComanda()
      })
      hub.on('ComandaCancelled', () => {
        toast.error('Sua comanda foi cancelada.', { duration: 6000 })
        fetchComanda()
      })
      hub.on('ItemAddedByAdmin', (data: { itemName: string; newTotalInReais: number }) => {
        toast(`+${data.itemName} adicionado pelo atendente`, { icon: '🛒' })
        fetchComanda()
      })
      hub.on('ComandaUpdated', () => fetchComanda())
      hub.onreconnected(() => fetchComanda())
    }).catch(() => {})

    return () => { stopHub() }
  }, [fetchComanda])

  const toggleImmersive = () => {
    const newVal = !immersiveMode
    setImmersiveMode(newVal)
    localStorage.setItem('immersive-mode', String(newVal))
  }

  const [confirmItem, setConfirmItem] = useState<Product | null>(null)

  async function addProduct(product: Product) {
    if (!comanda) return
    setConfirmItem(null)
    setAdding(product.id)
    try {
      const { data } = await comandaApi.addItem(comanda.id, {
        productId: product.id,
        itemName: product.name,
        unitPriceInCents: product.priceInCents,
        quantity: 1,
      })
      setComanda(data)
      toast.success(`${product.name} adicionado!`, { icon: '✅' })
    } catch { 
      toast.error('Não foi possível adicionar o item.') 
    } finally { 
      setAdding(null) 
    }
  }

  async function applyPoints() {
    if (!comanda || !profile || profile.pointsBalance <= 0) return
    setApplyingPts(true)
    try {
      const { data } = await comandaApi.applyPoints(comanda.id, profile.pointsBalance)
      setComanda(data)
      setProfile(prev => prev ? { ...prev, pointsBalance: 0 } : prev)
      toast.success(`${profile.pointsBalance} pontos aplicados! 🎉`)
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao aplicar pontos.')
    } finally { setApplyingPts(false) }
  }

  async function removePoints() {
    if (!comanda) return
    setRemovingPts(true)
    try {
      const { data } = await comandaApi.removePoints(comanda.id)
      const pontosDevolvidos = comanda.pointsApplied
      setComanda(data)
      setProfile(prev => prev ? { ...prev, pointsBalance: (prev.pointsBalance ?? 0) + pontosDevolvidos } : prev)
      toast.success('Pontos removidos e devolvidos ao saldo! ✅')
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao remover pontos.')
    } finally { setRemovingPts(false) }
  }

  const statusColors: Record<string, string> = {
    Aberta: 'text-emerald-400', 
    EmAndamento: 'text-amber-400',
    Fechada: 'text-blue-400', 
    Cancelada: 'text-red-400',
  }

  return (
    <div className="min-h-screen bg-surface-900 pb-32">
      <Toaster position="top-center" toastOptions={{ style: { background: 'var(--bg-card)', color: 'var(--text-primary)', border: '1px solid var(--border-color)' }}} />

      {/* Modal de confirmação de item */}
      {confirmItem && (
        <div className="fixed inset-0 z-50 flex items-end justify-center p-4 bg-black/60 backdrop-blur-sm">
          <div className="bg-surface-800 border border-surface-600 rounded-2xl w-full max-w-sm p-6 space-y-4 shadow-2xl">
            <div>
              <p className="text-sm text-gray-400 font-medium mb-1">Deseja adicionar?</p>
              <p className="font-bold text-white text-xl">{confirmItem.name}</p>
              <p className="text-accent-green font-bold text-lg mt-0.5">
                R$ {confirmItem.priceInReais.toFixed(2).replace('.', ',')}
              </p>
            </div>
            <div className="flex gap-3">
              <button onClick={() => setConfirmItem(null)} className="flex-1 py-3 px-4 rounded-xl border border-surface-500 text-gray-400 font-bold hover:bg-white/5 transition-colors">
                Cancelar
              </button>
              <button
                onClick={() => addProduct(confirmItem)}
                disabled={adding === confirmItem.id}
                className="flex-1 py-3 px-4 rounded-xl bg-brand-500 text-white font-bold hover:bg-brand-600 transition-colors flex items-center justify-center gap-2"
              >
                {adding === confirmItem.id ? <Loader2 className="w-5 h-5 animate-spin" /> : <Plus className="w-5 h-5" />}
                Confirmar
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── TOP HEADER (ESTILO SANTUÁRIO) ────────────────────────── */}
      <header className="bg-surface-800 border-b border-surface-700 px-6 pt-8 pb-6 text-center">
        <img src="/logo-santuario.png" alt="Logo" className="w-16 h-16 mx-auto mb-4 drop-shadow-[0_0_15px_rgba(66,182,238,0.3)]" />
        <h1 className="text-2xl tracking-[0.2em] text-white uppercase" style={{ fontFamily: 'var(--font-cinzel)' }}>
          O Santuário Nerd
        </h1>
        <div className="flex items-center justify-center gap-3 mt-4">
          <ThemeToggle compact />
          <Link href="/cliente/perfil" className="flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-surface-900 border border-surface-600 text-xs text-gray-400 hover:text-white transition-colors">
            <UserIcon className="w-3.5 h-3.5" /> Conta
          </Link>
          <button onClick={toggleImmersive} className={clsx(
            "flex items-center gap-1.5 px-3 py-1.5 rounded-full border transition-all text-xs font-medium",
            immersiveMode ? "bg-brand-500/10 border-brand-500 text-brand-500" : "bg-surface-900 border-surface-600 text-gray-400"
          )}>
            <BookOpen className="w-3.5 h-3.5" /> {immersiveMode ? 'Modo RPG On' : 'Modo Clássico'}
          </button>
        </div>
      </header>

      {/* ── CONTENT AREA ─────────────────────────────────────────── */}
      <main className="max-w-lg mx-auto px-4 py-8 space-y-8">

        {/* Status do Jogador / Pontos */}
        {profile && (
          <section className="bg-gradient-to-br from-surface-800 to-surface-900 rounded-2xl p-5 border border-surface-600 relative overflow-hidden group">
            <div className="absolute top-0 right-0 p-4 opacity-5 group-hover:opacity-10 transition-opacity">
              <Star className="w-20 h-20 text-white" />
            </div>
            <div className="flex items-center gap-4">
              <div className="w-12 h-12 rounded-full bg-brand-500/20 flex items-center justify-center border border-brand-500/30">
                <Star className="w-6 h-6 text-brand-500" />
              </div>
              <div>
                <p className="text-xs text-gray-400 font-bold uppercase tracking-wider">Seus Pontos</p>
                <p className="text-2xl font-black text-white">{profile.pointsBalance} <span className="text-sm font-normal text-gray-500">pts</span></p>
              </div>
            </div>
          </section>
        )}

        {loading ? (
          <div className="flex flex-col items-center justify-center py-20 gap-4">
            <Loader2 className="w-8 h-8 animate-spin text-brand-500" />
            <p className="text-sm text-gray-500 font-medium">Lendo os pergaminhos...</p>
          </div>
        ) : !comanda ? (
          <section className="text-center py-16 space-y-4">
            <div className="w-20 h-20 bg-surface-800 rounded-full flex items-center justify-center mx-auto border border-surface-600">
              <PackageOpen className="w-10 h-10 text-gray-500" />
            </div>
            <div className="space-y-1">
              <h2 className="text-white font-bold text-lg">Nenhuma comanda aberta</h2>
              <p className="text-gray-400 text-sm">Escaneie o QR Code da sua mesa para começar.</p>
            </div>
          </section>
        ) : (
          <>
            {/* COMANDA ATIVA */}
            <section className={clsx(
              "relative transition-all duration-500",
              immersiveMode ? "bg-[#f4e4bc] text-[#5d4037] p-8 rounded-[2px] shadow-[0_10px_30px_rgba(0,0,0,0.5)] border-x-8 border-[#d7c49e]" : "bg-surface-800 border border-surface-600 rounded-2xl p-6"
            )}>
              {/* Detalhes Visuais do Modo Imersivo */}
              {immersiveMode && (
                <>
                  <div className="absolute -top-3 left-0 right-0 flex justify-between px-8">
                    <div className="w-8 h-6 bg-[#d7c49e] rounded-t-full" />
                    <div className="w-8 h-6 bg-[#d7c49e] rounded-t-full" />
                  </div>
                  <div className="border-b border-[#5d4037]/20 pb-4 mb-6">
                    <h2 className="font-bold uppercase tracking-[0.1em] text-center" style={{ fontFamily: 'var(--font-cinzel)' }}>
                      Relatório de Consumo
                    </h2>
                  </div>
                </>
              )}

              <div className="flex items-center justify-between mb-6">
                <div className="flex items-center gap-2">
                  <Receipt className={clsx("w-5 h-5", immersiveMode ? "text-[#5d4037]" : "text-brand-500")} />
                  <span className={clsx("font-bold text-sm uppercase tracking-wider", immersiveMode ? "text-[#5d4037]" : "text-white")}>
                    Mesa {comanda.tableIdentifier || 'N/A'}
                  </span>
                </div>
                <span className={clsx("text-[10px] font-black uppercase px-2 py-0.5 rounded border", 
                  immersiveMode ? "border-[#5d4037] text-[#5d4037]" : "border-emerald-500/30 bg-emerald-500/10 text-emerald-400"
                )}>
                  {comanda.status}
                </span>
              </div>

              {comanda.items.length === 0 ? (
                <div className={clsx("text-center py-8 border-2 border-dashed rounded-xl", 
                  immersiveMode ? "border-[#5d4037]/20 text-[#5d4037]/60" : "border-surface-500 text-gray-500"
                )}>
                  <p className="text-sm italic">Seu pergaminho está em branco...</p>
                </div>
              ) : (
                <div className="space-y-4 mb-8">
                  {comanda.items.map(item => (
                    <div key={item.id} className="flex justify-between items-start gap-4">
                      <div className="flex-1">
                        <p className={clsx("text-sm font-bold leading-tight", immersiveMode ? "text-[#5d4037]" : "text-white")}>
                          {item.itemNameSnapshot}
                        </p>
                        <p className={clsx("text-[10px] mt-0.5 font-medium", immersiveMode ? "text-[#5d4037]/60" : "text-gray-400")}>
                          {item.quantity}× R$ {item.unitPriceInReais.toFixed(2).replace('.', ',')}
                        </p>
                      </div>
                      <span className={clsx("font-bold text-sm", immersiveMode ? "text-[#5d4037]" : "text-accent-green")}>
                        R$ {item.subtotalInReais.toFixed(2).replace('.', ',')}
                      </span>
                    </div>
                  ))}

                  <div className={clsx("pt-4 border-t-2 border-dashed flex justify-between items-center", 
                    immersiveMode ? "border-[#5d4037]/20" : "border-surface-600"
                  )}>
                    <span className={clsx("font-black uppercase text-sm", immersiveMode ? "text-[#5d4037]" : "text-white")}>Total</span>
                    <span className={clsx("text-2xl font-black", immersiveMode ? "text-[#5d4037]" : "text-accent-green")}>
                      R$ {comanda.totalInReais.toFixed(2).replace('.', ',')}
                    </span>
                  </div>
                </div>
              )}

              {/* Botão de Pontos (Estilo diferenciado) */}
              {comanda.status !== 'Fechada' && comanda.status !== 'Cancelada' && (
                <div className="mt-6">
                  {comanda.pointsApplied > 0 ? (
                    <button onClick={removePoints} disabled={removingPts} className={clsx(
                      "w-full p-3 rounded-xl flex items-center justify-between transition-opacity",
                      immersiveMode ? "bg-black/5 text-[#5d4037]" : "bg-emerald-500/10 text-emerald-400"
                    )}>
                      <div className="flex items-center gap-2 font-bold text-xs uppercase">
                        <Star className="w-4 h-4" /> Descontado
                      </div>
                      <div className="flex items-center gap-2">
                        <span className="font-black">-{ (comanda.pointsApplied / 100).toFixed(2).replace('.', ',') } pts</span>
                        <Trash2 className="w-3.5 h-3.5 opacity-50" />
                      </div>
                    </button>
                  ) : profile && profile.pointsBalance > 0 && !profile.pointsExpired && (
                    <button onClick={applyPoints} disabled={applyingPts} className={clsx(
                      "w-full p-3 rounded-xl flex items-center justify-between border-2 border-dashed transition-all active:scale-95",
                      immersiveMode ? "border-[#5d4037]/30 text-[#5d4037] hover:bg-black/5" : "border-brand-500/30 text-brand-400 hover:bg-brand-500/5"
                    )}>
                      <span className="font-black text-xs uppercase tracking-widest">Usar Pontos Acumulados</span>
                      <Plus className="w-4 h-4" />
                    </button>
                  )}
                </div>
              )}
            </section>

            {/* CARDÁPIO / MENU DE PRODUTOS */}
            {comanda.status !== 'Fechada' && comanda.status !== 'Cancelada' && (
              <section className="space-y-4">
                <div className="flex items-center justify-between">
                  <h2 className="font-bold text-white uppercase tracking-widest text-xs flex items-center gap-2">
                    <ShoppingCart className="w-4 h-4 text-brand-500" /> Cardápio Disponível
                  </h2>
                  <span className="text-[10px] text-gray-500 font-bold uppercase">{products.length} Itens</span>
                </div>

                <div className="grid grid-cols-2 gap-3">
                  {products.map(p => (
                    <button
                      key={p.id}
                      onClick={() => p.stockQuantity > 0 && setConfirmItem(p)}
                      disabled={adding === p.id || p.stockQuantity === 0}
                      className={clsx(
                        'bg-surface-800 border border-surface-600 rounded-2xl p-4 text-left transition-all duration-200 active:scale-95 disabled:opacity-40 relative group',
                        adding === p.id && 'border-brand-500'
                      )}
                    >
                      <div className="flex justify-between items-start mb-3">
                        <span className="text-3xl filter drop-shadow-sm">{getCategoryEmoji(p.category, categories)}</span>
                        <div className="w-8 h-8 rounded-full bg-surface-900 flex items-center justify-center border border-surface-600 group-hover:border-brand-500/50 transition-colors">
                          <Plus className="w-4 h-4 text-gray-400 group-hover:text-brand-500" />
                        </div>
                      </div>
                      <p className="text-xs font-bold text-white line-clamp-2 min-h-[2rem]">{p.name}</p>
                      <p className="text-accent-green font-black text-sm mt-2">
                        R$ {p.priceInReais.toFixed(2).replace('.', ',')}
                      </p>
                    </button>
                  ))}
                </div>
              </section>
            )}
          </>
        )}
      </main>
    </div>
  )
}

function getCategoryEmoji(cat: string, categories: ProductCategory[]): string {
  const found = categories.find(c => c.name === cat)
  return found?.emoji ?? '📦'
}
