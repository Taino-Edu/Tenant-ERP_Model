'use client'
import { useEffect, useState, useCallback } from 'react'
import { comandaApi, userApi, productApi, categoryApi, ComandaDto, Product, ProductCategory, UserProfile } from '@/lib/api'
import { getUserName } from '@/lib/auth'
import { startHub, stopHub, ComandaOpenedEvent } from '@/lib/signalr'
import toast, { Toaster } from 'react-hot-toast'
import {
  ShoppingCart, Plus, Trash2, Loader2, Search,
  Receipt, PackageOpen, Star, BookOpen, User as UserIcon, Package
} from 'lucide-react'
import ThemeToggle from '@/components/ThemeToggle'
import Link from 'next/link'
import clsx from 'clsx'


function ProductCard({ p, adding, onAdd }: {
  p: Product
  adding: string | null
  onAdd: () => void
}) {
  const isAdding = adding === p.id
  return (
    <button
      onClick={onAdd}
      disabled={isAdding}
      className={clsx(
        'bg-surface-800 border rounded-2xl text-left transition-all duration-150 active:scale-95 overflow-hidden flex flex-col disabled:opacity-50',
        isAdding ? 'border-brand-500' : 'border-surface-600'
      )}
    >
      <div className="w-full h-36 bg-surface-700 flex items-center justify-center overflow-hidden p-1">
        {p.imageUrl
          ? <img src={p.imageUrl} alt={p.name} className="w-full h-full object-contain" />
          : <Package className="w-12 h-12 text-gray-500 opacity-40" />
        }
      </div>
      <div className="p-3 flex flex-col gap-2">
        <p className="text-xs font-semibold text-gray-100 leading-snug min-h-[2.5rem] line-clamp-2">{p.name}</p>
        <div className="flex items-center justify-between gap-1">
          <span className="text-accent-green font-black text-sm">
            R$ {p.priceInReais.toFixed(2).replace('.', ',')}
          </span>
          <div className={clsx(
            'w-8 h-8 rounded-full flex items-center justify-center shrink-0 transition-colors',
            isAdding ? 'bg-brand-500/20' : 'bg-brand-500'
          )}>
            {isAdding
              ? <Loader2 className="w-4 h-4 animate-spin text-brand-500" />
              : <Plus className="w-4 h-4 text-white" />}
          </div>
        </div>
      </div>
    </button>
  )
}

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
  const [searchQuery, setSearchQuery]     = useState('')

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

  const filteredProducts = products.filter(p =>
    p.name.toLowerCase().includes(searchQuery.toLowerCase())
  )

  const groupedProducts = categories
    .filter(cat => cat.isActive && filteredProducts.some(p => p.category === cat.name))
    .sort((a, b) => a.displayOrder - b.displayOrder)
    .map(cat => ({ category: cat, items: filteredProducts.filter(p => p.category === cat.name) }))

  const uncategorized = filteredProducts.filter(p =>
    !categories.some(cat => cat.name === p.category)
  )

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
          <div className="bg-surface-800 border border-surface-600 rounded-2xl w-full max-w-sm shadow-2xl overflow-hidden">
            {confirmItem.imageUrl && (
              <div className="w-full h-48 bg-surface-700 flex items-center justify-center p-3">
                <img src={confirmItem.imageUrl} alt={confirmItem.name} className="h-full w-full object-contain" />
              </div>
            )}
            <div className="p-5 space-y-4">
              <div>
                <p className="text-xs text-gray-500 font-medium uppercase tracking-wider mb-1">Adicionar à comanda</p>
                <p className="font-bold text-white text-lg leading-snug">{confirmItem.name}</p>
                <p className="text-accent-green font-black text-xl mt-1">
                  R$ {confirmItem.priceInReais.toFixed(2).replace('.', ',')}
                </p>
              </div>
              <div className="flex gap-3">
                <button onClick={() => setConfirmItem(null)} className="flex-1 py-3 px-4 rounded-xl border border-surface-500 text-gray-400 font-semibold hover:bg-white/5 transition-colors">
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
        </div>
      )}

      {/* ── TOP HEADER ───────────────────────────────────────────── */}
      <header className="bg-surface-800 border-b border-surface-700 px-5 pt-10 pb-5">
        <div className="flex items-center justify-between max-w-lg mx-auto">
          <div className="flex items-center gap-3">
            <img src="/maikon-avatar.png" alt="Mascote" className="w-10 h-10 object-contain" />
            <div>
              <h1 className="text-base font-bold text-white leading-tight">Santuário Nerd</h1>
              {profile && <p className="text-xs text-gray-400">Olá, {profile.name.split(' ')[0]} 👋</p>}
            </div>
          </div>
          <div className="flex items-center gap-2">
            <ThemeToggle compact />
            <button onClick={toggleImmersive} className={clsx(
              "p-2 rounded-full border transition-all",
              immersiveMode ? "bg-brand-500/10 border-brand-500 text-brand-500" : "border-surface-600 text-gray-500 hover:text-gray-300"
            )} title={immersiveMode ? 'Modo RPG' : 'Modo Clássico'}>
              <BookOpen className="w-4 h-4" />
            </button>
            <Link href="/cliente/perfil">
              {profile?.profileImageUrl
                ? <img src={profile.profileImageUrl} alt={profile.name} className="w-9 h-9 rounded-full object-cover ring-2 ring-brand-500/40" />
                : <div className="w-9 h-9 rounded-full bg-brand-500/20 border border-brand-500/30 flex items-center justify-center">
                    <span className="text-sm font-bold text-brand-400">{profile?.name?.charAt(0).toUpperCase() ?? <UserIcon className="w-4 h-4" />}</span>
                  </div>
              }
            </Link>
          </div>
        </div>
      </header>

      {/* ── CONTENT AREA ─────────────────────────────────────────── */}
      <main className="max-w-lg mx-auto px-4 py-8 space-y-8">

        {/* Pontos */}
        {profile && (
          <section className="bg-surface-800 rounded-2xl p-4 border border-surface-600 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-full bg-brand-500/15 flex items-center justify-center border border-brand-500/25">
                <Star className="w-5 h-5 text-brand-400" />
              </div>
              <div>
                <p className="text-[11px] text-gray-500 font-medium uppercase tracking-wider">Seus Pontos</p>
                <p className="text-xl font-black text-gray-100 leading-tight">{profile.pointsBalance} <span className="text-xs font-normal text-gray-500">pts</span></p>
              </div>
            </div>
            {profile.balanceInCents > 0 && (
              <div className="text-right">
                <p className="text-[11px] text-gray-500 font-medium uppercase tracking-wider">Cashback</p>
                <p className="text-base font-black text-accent-green leading-tight">R$ {(profile.balanceInCents / 100).toFixed(2).replace('.', ',')}</p>
              </div>
            )}
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
              <h2 className="text-gray-100 font-bold text-lg">Nenhuma comanda aberta</h2>
              <p className="text-gray-400 text-sm">Escaneie o QR Code da sua mesa para começar.</p>
            </div>
          </section>
        ) : (
          <>
            {/* COMANDA ATIVA */}
            <section className={clsx(
              "rounded-2xl overflow-hidden border transition-all duration-500",
              immersiveMode
                ? "bg-[#f4e4bc] border-[#d7c49e] shadow-[0_10px_30px_rgba(0,0,0,0.4)]"
                : "bg-surface-800 border-surface-600"
            )}>
              {/* Header da comanda */}
              <div className={clsx(
                "flex items-center justify-between px-5 py-4 border-b",
                immersiveMode ? "border-[#d7c49e]" : "border-surface-600"
              )}>
                <div className="flex items-center gap-2">
                  <Receipt className={clsx("w-4 h-4", immersiveMode ? "text-[#5d4037]" : "text-brand-500")} />
                  <span className={clsx("font-bold text-sm", immersiveMode ? "text-[#5d4037]" : "text-gray-100")}>
                    {immersiveMode ? 'Pergaminho de Consumo' : `Mesa ${comanda.tableIdentifier || 'N/A'}`}
                  </span>
                </div>
                <span className={clsx(
                  "text-[10px] font-bold uppercase px-2.5 py-1 rounded-full",
                  immersiveMode ? "bg-[#5d4037]/10 text-[#5d4037]" : "bg-emerald-500/10 text-emerald-400"
                )}>
                  {comanda.status}
                </span>
              </div>

              {/* Itens */}
              <div className="px-5 py-4">
                {comanda.items.length === 0 ? (
                  <p className={clsx("text-center text-sm py-6 italic", immersiveMode ? "text-[#5d4037]/50" : "text-gray-500")}>
                    Nenhum item ainda...
                  </p>
                ) : (
                  <div className="space-y-3">
                    {comanda.items.map((item, idx) => (
                      <div key={item.id}>
                        <div className="flex justify-between items-center gap-4">
                          <div className="flex-1 min-w-0">
                            <p className={clsx("text-sm font-semibold leading-tight truncate", immersiveMode ? "text-[#5d4037]" : "text-gray-100")}>
                              {item.itemNameSnapshot}
                            </p>
                            <p className={clsx("text-xs mt-0.5", immersiveMode ? "text-[#5d4037]/60" : "text-gray-500")}>
                              {item.quantity}× R$ {item.unitPriceInReais.toFixed(2).replace('.', ',')}
                            </p>
                          </div>
                          <span className={clsx("font-bold text-sm shrink-0", immersiveMode ? "text-[#5d4037]" : "text-accent-green")}>
                            R$ {item.subtotalInReais.toFixed(2).replace('.', ',')}
                          </span>
                        </div>
                        {idx < comanda.items.length - 1 && (
                          <div className={clsx("mt-3 border-b", immersiveMode ? "border-[#5d4037]/10" : "border-surface-700")} />
                        )}
                      </div>
                    ))}
                  </div>
                )}

                {/* Total */}
                {comanda.items.length > 0 && (
                  <div className={clsx("flex justify-between items-center mt-4 pt-4 border-t", immersiveMode ? "border-[#5d4037]/20" : "border-surface-600")}>
                    <span className={clsx("font-bold text-sm uppercase tracking-wide", immersiveMode ? "text-[#5d4037]" : "text-gray-400")}>Total</span>
                    <span className={clsx("text-2xl font-black", immersiveMode ? "text-[#5d4037]" : "text-white")}>
                      R$ {comanda.totalInReais.toFixed(2).replace('.', ',')}
                    </span>
                  </div>
                )}
              </div>

              {/* Pontos */}
              {comanda.status !== 'Fechada' && comanda.status !== 'Cancelada' && (
                <div className={clsx("px-5 pb-5")}>
                  {comanda.pointsApplied > 0 ? (
                    <button onClick={removePoints} disabled={removingPts} className={clsx(
                      "w-full p-3 rounded-xl flex items-center justify-between transition-opacity",
                      immersiveMode ? "bg-[#5d4037]/10 text-[#5d4037]" : "bg-emerald-500/10 text-emerald-400"
                    )}>
                      <div className="flex items-center gap-2 font-semibold text-xs">
                        <Star className="w-4 h-4" /> {(comanda.pointsApplied / 100).toFixed(2).replace('.', ',')} pts aplicados
                      </div>
                      <Trash2 className="w-3.5 h-3.5 opacity-50" />
                    </button>
                  ) : profile && profile.pointsBalance > 0 && !profile.pointsExpired && (
                    <button onClick={applyPoints} disabled={applyingPts} className={clsx(
                      "w-full p-3 rounded-xl flex items-center justify-between border border-dashed transition-all active:scale-95",
                      immersiveMode ? "border-[#5d4037]/30 text-[#5d4037]" : "border-brand-500/30 text-brand-400 hover:bg-brand-500/5"
                    )}>
                      <span className="font-semibold text-xs">Usar {profile.pointsBalance} pontos acumulados</span>
                      <Plus className="w-4 h-4" />
                    </button>
                  )}
                </div>
              )}
            </section>

            {/* ITENS PARA SUA JORNADA */}
            {comanda.status !== 'Fechada' && comanda.status !== 'Cancelada' && (
              <section className="space-y-5">
                <div className="flex items-center justify-between">
                  <h2 className="font-bold text-gray-100 uppercase tracking-widest text-xs flex items-center gap-2">
                    <ShoppingCart className="w-4 h-4 text-brand-500" /> Itens para sua Jornada
                  </h2>
                  <span className="text-[10px] text-gray-500 font-bold uppercase">{filteredProducts.length} Itens</span>
                </div>

                {/* Busca */}
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500 pointer-events-none" />
                  <input
                    type="text"
                    placeholder="Buscar item..."
                    value={searchQuery}
                    onChange={e => setSearchQuery(e.target.value)}
                    className="w-full bg-surface-800 border border-surface-600 rounded-xl pl-9 pr-4 py-2.5 text-sm text-gray-100 placeholder-gray-500 focus:outline-none focus:border-brand-500 transition-colors"
                  />
                </div>

                {/* Por categoria */}
                {groupedProducts.map(({ category, items }) => (
                  <div key={category.id} className="space-y-3">
                    <h3 className="text-xs font-bold text-gray-400 flex items-center gap-1.5">
                      {category.emoji && <span className="text-sm">{category.emoji}</span>}
                      <span className="uppercase tracking-widest">{category.name}</span>
                    </h3>
                    <div className="grid grid-cols-2 gap-3">
                      {items.map(p => <ProductCard key={p.id} p={p} adding={adding} onAdd={() => setConfirmItem(p)} />)}
                    </div>
                  </div>
                ))}

                {/* Itens sem categoria */}
                {uncategorized.length > 0 && (
                  <div className="grid grid-cols-2 gap-3">
                    {uncategorized.map(p => <ProductCard key={p.id} p={p} adding={adding} onAdd={() => setConfirmItem(p)} />)}
                  </div>
                )}

                {filteredProducts.length === 0 && (
                  <p className="text-center text-gray-500 text-sm py-8">Nenhum item encontrado.</p>
                )}
              </section>
            )}
          </>
        )}
      </main>
    </div>
  )
}
