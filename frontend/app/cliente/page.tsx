'use client'
import { useEffect, useState, useCallback } from 'react'
import { comandaApi, userApi, productApi, categoryApi, ComandaDto, Product, ProductCategory, UserProfile } from '@/lib/api'
import { getUserName } from '@/lib/auth'
import { startHub, stopHub, ComandaOpenedEvent } from '@/lib/signalr'
import toast, { Toaster } from 'react-hot-toast'
import {
  ShoppingCart, Plus, Trash2, Loader2, Search,
  Receipt, PackageOpen, Star, User as UserIcon, Package, ChevronRight, ChevronDown
} from 'lucide-react'
import Link from 'next/link'

const C = {
  navy:   '#0C3D5A',
  blue:   '#3EC2F2',
  blue2:  '#1A9DD4',
  yellow: '#FFE45E',
  bg:     '#EBF7FD',
  white:  '#FFFFFF',
  muted:  '#4D8FAC',
  border: 'rgba(62,194,242,0.18)',
}

function ProductCard({ p, adding, onAdd }: {
  p: Product
  adding: string | null
  onAdd: () => void
}) {
  const isAdding     = adding === p.id
  const unavailable  = p.stockQuantity === 0
  return (
    <button
      onClick={unavailable ? undefined : onAdd}
      disabled={isAdding || unavailable}
      className="text-left rounded-2xl overflow-hidden flex flex-col transition-all duration-150"
      style={{
        backgroundColor: C.white,
        border: `1px solid ${C.border}`,
        boxShadow: '0 2px 8px rgba(12,61,90,0.06)',
        opacity: unavailable ? 0.7 : 1,
        cursor: unavailable ? 'default' : undefined,
      }}
    >
      <div className="relative w-full aspect-square overflow-hidden flex items-center justify-center p-2"
        style={{ backgroundColor: C.bg }}>
        {p.imageUrl
          ? <img src={p.imageUrl} alt={p.name} className="w-full h-full object-contain" />
          : <Package className="w-10 h-10 opacity-20" style={{ color: C.blue2 }} />
        }
        {p.isPreVenda && !unavailable && (
          <span className="absolute top-1.5 left-1.5 text-[9px] font-black uppercase tracking-wide px-1.5 py-0.5 rounded-md"
            style={{ backgroundColor: '#7C3AED', color: '#fff' }}>
            Pré-venda
          </span>
        )}
        {!p.isPreVenda && p.isOnPromo && !unavailable && (
          <span className="absolute top-1.5 left-1.5 text-[9px] font-black uppercase tracking-wide px-1.5 py-0.5 rounded-md"
            style={{ backgroundColor: '#FF3B3B', color: '#fff' }}>
            Promoção
          </span>
        )}
        {unavailable && (
          <div className="absolute inset-0 flex items-center justify-center"
            style={{ backgroundColor: 'rgba(255,255,255,0.55)' }}>
            <span className="text-[10px] font-black uppercase tracking-wide px-2 py-1 rounded-md text-white"
              style={{ backgroundColor: '#6B7280' }}>
              Indisponível
            </span>
          </div>
        )}
      </div>
      <div className="p-3 flex flex-col gap-1.5 flex-1">
        <p className="text-xs font-bold leading-snug line-clamp-2 flex-1" style={{ color: C.navy }}>{p.name}</p>
        <div className="flex items-center justify-between mt-1 gap-1">
          {p.isOnPromo && p.discountPriceInReais != null && !unavailable ? (
            <div className="flex flex-col">
              <span className="text-[10px] line-through" style={{ color: C.muted }}>
                R$ {p.priceInReais.toFixed(2).replace('.', ',')}
              </span>
              <span className="text-sm font-black" style={{ color: '#FF3B3B' }}>
                R$ {p.discountPriceInReais.toFixed(2).replace('.', ',')}
              </span>
            </div>
          ) : (
            <span className="text-sm font-black" style={{ color: unavailable ? C.muted : C.blue2 }}>
              R$ {p.priceInReais.toFixed(2).replace('.', ',')}
            </span>
          )}
          <div className="w-8 h-8 rounded-full flex items-center justify-center shrink-0 transition-colors"
            style={{ backgroundColor: unavailable ? '#E5E7EB' : isAdding ? `${C.blue}25` : C.blue }}>
            {isAdding
              ? <Loader2 className="w-4 h-4 animate-spin" style={{ color: C.blue }} />
              : unavailable
                ? <span className="text-[10px] font-bold text-gray-400">—</span>
                : <Plus className="w-4 h-4 text-white" />
            }
          </div>
        </div>
      </div>
    </button>
  )
}

export default function ClientePage() {
  const [comanda,      setComanda]      = useState<ComandaDto | null>(null)
  const [products,     setProducts]     = useState<Product[]>([])
  const [categories,   setCategories]   = useState<ProductCategory[]>([])
  const [profile,      setProfile]      = useState<UserProfile | null>(null)
  const [loading,      setLoading]      = useState(true)
  const [adding,       setAdding]       = useState<string | null>(null)
  const [applyingPts,  setApplyingPts]  = useState(false)
  const [removingPts,  setRemovingPts]  = useState(false)
  const [searchQuery,  setSearchQuery]  = useState('')
  const [activeCategory, setActiveCategory] = useState<string | null>(null)
  const [confirmItem,  setConfirmItem]  = useState<Product | null>(null)
  const [showComandaSheet, setShowComandaSheet] = useState(false)

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
    fetchComanda()
    productApi.listStore().then(r => setProducts(r.data)).catch(() => {})
    categoryApi.list().then(r => setCategories(r.data)).catch(() => {})
    userApi.me().then(r => setProfile(r.data)).catch(() => {})

    startHub().then(hub => {
      hub.on('ComandaOpened', async (data: ComandaOpenedEvent) => {
        await hub.invoke('JoinComandaGroup', data.comandaId).catch(() => {})
        await fetchComanda()
        toast.success('Sua comanda foi aberta!', { duration: 5000 })
      })
      hub.on('ComandaClosed', () => {
        toast.success('Comanda fechada! Obrigado pela visita.', { duration: 6000 })
        fetchComanda()
      })
      hub.on('ComandaCancelled', () => {
        toast.error('Sua comanda foi cancelada.', { duration: 6000 })
        fetchComanda()
      })
      hub.on('ItemAddedByAdmin', (data: { itemName: string }) => {
        toast(`+${data.itemName} adicionado pelo atendente`, { icon: '🛒' })
        fetchComanda()
      })
      hub.on('ComandaUpdated', () => fetchComanda())
      hub.onreconnected(() => fetchComanda())
    }).catch(() => {})

    return () => { stopHub() }
  }, [fetchComanda])

  async function addProduct(product: Product) {
    if (!comanda) return
    setConfirmItem(null)
    setAdding(product.id)
    try {
      const { data } = await comandaApi.addItem(comanda.id, {
        productId: product.id,
        itemName: product.name,
        unitPriceInCents: product.isOnPromo && product.discountPriceInCents != null
          ? product.discountPriceInCents
          : product.priceInCents,
        quantity: 1,
      })
      setComanda(data)
      toast.success(`${product.name} adicionado!`)
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
      toast.success(`${profile.pointsBalance} pontos aplicados!`)
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
      const devolvidos = comanda.pointsApplied
      setComanda(data)
      setProfile(prev => prev ? { ...prev, pointsBalance: (prev.pointsBalance ?? 0) + devolvidos } : prev)
      toast.success('Pontos devolvidos ao saldo!')
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao remover pontos.')
    } finally { setRemovingPts(false) }
  }

  // Backend já filtra isActive + showOnSite; exibimos todos inclusive sem estoque (badge "Indisponível")
  const activeProducts = products.filter(p => p.isActive)

  const filteredProducts = activeProducts.filter(p => {
    const matchSearch = p.name.toLowerCase().includes(searchQuery.toLowerCase())
    const matchCat    = !activeCategory || p.category === activeCategory
    return matchSearch && matchCat
  })

  const activeCategories = categories
    .filter(cat => cat.isActive && activeProducts.some(p => p.category === cat.name))
    .sort((a, b) => a.displayOrder - b.displayOrder)

  const groupedProducts = activeCategories
    .filter(cat => filteredProducts.some(p => p.category === cat.name))
    .map(cat => ({ category: cat, items: filteredProducts.filter(p => p.category === cat.name) }))

  const uncategorized = filteredProducts.filter(p =>
    !categories.some(cat => cat.name === p.category)
  )

  return (
    <div className="min-h-screen" style={{ backgroundColor: C.bg }}>
      <Toaster position="top-center" toastOptions={{
        style: { background: C.white, color: C.navy, border: `1px solid ${C.border}`, fontWeight: 600 }
      }} />

      {/* Modal de confirmação — z-[60] fica acima do bottom sheet (z-50) */}
      {confirmItem && (
        <div className="fixed inset-0 z-[60] flex items-end justify-center px-4 pb-20 pt-4 bg-black/50 backdrop-blur-sm"
          onClick={() => setConfirmItem(null)}>
          <div className="w-full max-w-sm max-h-[85vh] overflow-y-auto rounded-3xl shadow-2xl"
            style={{ backgroundColor: C.white }}
            onClick={e => e.stopPropagation()}>
            {confirmItem.imageUrl && (
              <div className="w-full h-48 flex items-center justify-center p-4"
                style={{ backgroundColor: C.bg }}>
                <img src={confirmItem.imageUrl} alt={confirmItem.name} className="h-full w-full object-contain" />
              </div>
            )}
            <div className="p-5 space-y-4">
              <div>
                <p className="text-[10px] font-black uppercase tracking-widest mb-1" style={{ color: C.muted }}>
                  Adicionar à comanda
                </p>
                <p className="font-black text-lg leading-snug" style={{ color: C.navy }}>{confirmItem.name}</p>
                {confirmItem.isOnPromo && confirmItem.discountPriceInReais != null ? (
                  <div className="flex items-baseline gap-2 mt-1">
                    <span className="text-xl font-black" style={{ color: '#FF3B3B' }}>
                      R$ {confirmItem.discountPriceInReais.toFixed(2).replace('.', ',')}
                    </span>
                    <span className="text-sm line-through" style={{ color: C.muted }}>
                      R$ {confirmItem.priceInReais.toFixed(2).replace('.', ',')}
                    </span>
                  </div>
                ) : (
                  <p className="text-xl font-black mt-1" style={{ color: C.blue2 }}>
                    R$ {confirmItem.priceInReais.toFixed(2).replace('.', ',')}
                  </p>
                )}
              </div>
              <div className="flex gap-3">
                <button onClick={() => setConfirmItem(null)}
                  className="flex-1 py-3 rounded-2xl font-bold text-sm border transition-colors"
                  style={{ borderColor: C.border, color: C.muted }}>
                  Cancelar
                </button>
                <button onClick={() => addProduct(confirmItem)}
                  disabled={adding === confirmItem.id}
                  className="flex-1 py-3 rounded-2xl font-black text-sm flex items-center justify-center gap-2 transition-all active:scale-95"
                  style={{ backgroundColor: C.yellow, color: C.navy }}>
                  {adding === confirmItem.id
                    ? <Loader2 className="w-5 h-5 animate-spin" />
                    : <><Plus className="w-5 h-5" /> Confirmar</>}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ── HEADER ──────────────────────────────────────────────────── */}
      <header style={{ backgroundColor: C.navy }}>
        <div className="max-w-lg mx-auto px-5 pt-10 pb-6 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <img src="/logo-maikon.png" alt="Santuário Nerd" className="w-10 h-10 object-contain drop-shadow-md" />
            <div>
              <p className="text-white font-black text-base leading-tight">Santuário Nerd</p>
              {profile && (
                <p className="text-xs font-bold mt-0.5" style={{ color: 'rgba(255,255,255,0.7)' }}>
                  Olá, {profile.name.split(' ')[0]}!
                </p>
              )}
            </div>
          </div>
          <Link href="/cliente/perfil">
            {profile?.profileImageUrl
              ? <img src={profile.profileImageUrl} alt={profile.name}
                  className="w-10 h-10 rounded-full object-cover ring-2 ring-white/40" />
              : <div className="w-10 h-10 rounded-full flex items-center justify-center font-black text-sm"
                  style={{ backgroundColor: C.yellow, color: C.navy }}>
                  {profile?.name?.charAt(0).toUpperCase() ?? <UserIcon className="w-5 h-5" />}
                </div>
            }
          </Link>
        </div>
      </header>

      {/* ── CONTENT ─────────────────────────────────────────────────── */}
      <main className="max-w-lg mx-auto px-4 py-6 space-y-4 pb-24">

        {/* Pontos */}
        {profile && (
          <div className="rounded-2xl p-4 flex items-center justify-between"
            style={{ backgroundColor: C.white, border: `1px solid ${C.border}`, boxShadow: '0 2px 10px rgba(12,61,90,0.06)' }}>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl flex items-center justify-center"
                style={{ backgroundColor: `${C.blue}18` }}>
                <Star className="w-5 h-5" style={{ color: C.blue }} />
              </div>
              <div>
                <p className="text-[10px] font-black uppercase tracking-widest" style={{ color: C.muted }}>Seus Pontos</p>
                <p className="text-xl font-black leading-tight" style={{ color: C.navy }}>
                  {profile.pointsBalance} <span className="text-xs font-semibold" style={{ color: C.muted }}>pts</span>
                </p>
              </div>
            </div>
            {profile.balanceInCents > 0 && (
              <div className="text-right">
                <p className="text-[10px] font-black uppercase tracking-widest" style={{ color: C.muted }}>Cashback</p>
                <p className="text-base font-black" style={{ color: '#22c55e' }}>
                  R$ {(profile.balanceInCents / 100).toFixed(2).replace('.', ',')}
                </p>
              </div>
            )}
          </div>
        )}

        {loading ? (
          <div className="flex flex-col items-center justify-center py-20 gap-4">
            <Loader2 className="w-8 h-8 animate-spin" style={{ color: C.blue }} />
            <p className="text-sm font-semibold" style={{ color: C.muted }}>Carregando sua comanda...</p>
          </div>
        ) : !comanda ? (
          <div className="text-center py-16 space-y-4">
            <div className="w-20 h-20 rounded-full flex items-center justify-center mx-auto"
              style={{ backgroundColor: C.white, border: `1px solid ${C.border}` }}>
              <PackageOpen className="w-10 h-10" style={{ color: C.muted }} />
            </div>
            <div>
              <h2 className="font-black text-lg" style={{ color: C.navy }}>Nenhuma comanda aberta</h2>
              <p className="text-sm mt-1" style={{ color: C.muted }}>Escaneie o QR Code da sua mesa para começar.</p>
            </div>
          </div>
        ) : (
          <>
            {/* ── COMANDA ───────────────────────────────────────────── */}
            <div className="rounded-2xl overflow-hidden"
              style={{ backgroundColor: C.white, border: `1px solid ${C.border}`, boxShadow: '0 2px 10px rgba(12,61,90,0.06)' }}>

              {/* Header da comanda */}
              <div className="flex items-center justify-between px-5 py-4 border-b"
                style={{ borderColor: C.border }}>
                <div className="flex items-center gap-3">
                  {profile?.profileImageUrl ? (
                    <img
                      src={profile.profileImageUrl}
                      alt={profile.name}
                      className="w-10 h-10 rounded-full object-cover shrink-0"
                      style={{ border: `2px solid ${C.border}` }}
                    />
                  ) : (
                    <div
                      className="w-10 h-10 rounded-full flex items-center justify-center font-black text-sm shrink-0"
                      style={{ backgroundColor: C.yellow, color: C.navy }}
                    >
                      {profile?.name?.charAt(0).toUpperCase() ?? <UserIcon className="w-4 h-4" />}
                    </div>
                  )}
                  <div>
                    <span className="font-black text-sm block" style={{ color: C.navy }}>
                      Mesa {comanda.tableIdentifier || 'N/A'}
                    </span>
                    {profile && (
                      <span className="text-[11px] font-semibold block" style={{ color: C.muted }}>
                        {profile.name}
                      </span>
                    )}
                  </div>
                </div>
                <span className="text-[10px] font-black uppercase px-2.5 py-1 rounded-full"
                  style={{ backgroundColor: `${C.blue}15`, color: C.blue2 }}>
                  {comanda.status}
                </span>
              </div>

              {/* Itens */}
              <div className="px-5 py-4">
                {comanda.items.length === 0 ? (
                  <p className="text-center text-sm py-4 italic font-medium" style={{ color: C.muted }}>
                    Nenhum item ainda...
                  </p>
                ) : (
                  <div className="space-y-3">
                    {comanda.items.map((item, idx) => (
                      <div key={item.id}>
                        <div className="flex justify-between items-center gap-4">
                          <div className="flex-1 min-w-0">
                            <p className="text-sm font-bold leading-tight truncate" style={{ color: C.navy }}>
                              {item.itemNameSnapshot}
                            </p>
                            <p className="text-xs mt-0.5 font-medium" style={{ color: C.muted }}>
                              {item.quantity}× R$ {item.unitPriceInReais.toFixed(2).replace('.', ',')}
                            </p>
                          </div>
                          <span className="font-black text-sm shrink-0" style={{ color: C.blue2 }}>
                            R$ {item.subtotalInReais.toFixed(2).replace('.', ',')}
                          </span>
                        </div>
                        {idx < comanda.items.length - 1 && (
                          <div className="mt-3 border-b" style={{ borderColor: C.border }} />
                        )}
                      </div>
                    ))}
                  </div>
                )}

                {comanda.items.length > 0 && (
                  <div className="flex justify-between items-center mt-4 pt-4 border-t" style={{ borderColor: C.border }}>
                    <span className="font-black text-xs uppercase tracking-wide" style={{ color: C.muted }}>Total</span>
                    <span className="text-2xl font-black" style={{ color: C.navy }}>
                      R$ {comanda.totalInReais.toFixed(2).replace('.', ',')}
                    </span>
                  </div>
                )}
              </div>

              {/* Usar pontos */}
              {comanda.status !== 'Fechada' && comanda.status !== 'Cancelada' && (
                <div className="px-5 pb-5">
                  {comanda.pointsApplied > 0 ? (
                    <button onClick={removePoints} disabled={removingPts}
                      className="w-full p-3 rounded-xl flex items-center justify-between transition-opacity"
                      style={{ backgroundColor: '#22c55e18', color: '#16a34a' }}>
                      <div className="flex items-center gap-2 font-bold text-xs">
                        <Star className="w-4 h-4" />
                        {(comanda.pointsApplied / 100).toFixed(2).replace('.', ',')} pts aplicados
                      </div>
                      <Trash2 className="w-3.5 h-3.5 opacity-60" />
                    </button>
                  ) : profile && profile.pointsBalance > 0 && !profile.pointsExpired && (
                    <button onClick={applyPoints} disabled={applyingPts}
                      className="w-full p-3 rounded-xl flex items-center justify-between border border-dashed transition-all active:scale-95"
                      style={{ borderColor: `${C.blue}40`, color: C.blue2 }}>
                      <span className="font-bold text-xs">Usar {profile.pointsBalance} pontos acumulados</span>
                      {applyingPts ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />}
                    </button>
                  )}
                </div>
              )}
            </div>

            {/* ── PRODUTOS ──────────────────────────────────────────── */}
            {comanda.status !== 'Fechada' && comanda.status !== 'Cancelada' && (
              <div className="space-y-4">
                {/* Título */}
                <div className="flex items-center justify-between pt-2">
                  <h2 className="font-black text-xs uppercase tracking-widest flex items-center gap-2"
                    style={{ color: C.navy }}>
                    <ShoppingCart className="w-4 h-4" style={{ color: C.blue }} />
                    Itens para sua Jornada
                  </h2>
                  <span className="text-[10px] font-black uppercase" style={{ color: C.muted }}>
                    {filteredProducts.length} itens
                  </span>
                </div>

                {/* Busca */}
                <div className="relative">
                  <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 pointer-events-none"
                    style={{ color: C.muted }} />
                  <input
                    type="text"
                    placeholder="Buscar item..."
                    value={searchQuery}
                    onChange={e => setSearchQuery(e.target.value)}
                    className="w-full rounded-2xl pl-10 pr-4 py-3 text-sm font-semibold outline-none transition-all"
                    style={{
                      backgroundColor: C.white,
                      border: `1.5px solid ${C.border}`,
                      color: C.navy,
                    }}
                  />
                </div>

                {/* Chips de categoria */}
                {activeCategories.length > 1 && (
                  <div className="flex gap-2 overflow-x-auto pb-1 scrollbar-hide">
                    <button
                      onClick={() => setActiveCategory(null)}
                      className="shrink-0 px-4 py-2 rounded-full text-xs font-black transition-all"
                      style={{
                        backgroundColor: activeCategory === null ? C.blue : C.white,
                        color: activeCategory === null ? C.white : C.muted,
                        border: `1.5px solid ${activeCategory === null ? C.blue : C.border}`,
                      }}>
                      Todos
                    </button>
                    {activeCategories.map(cat => (
                      <button
                        key={cat.id}
                        onClick={() => setActiveCategory(activeCategory === cat.name ? null : cat.name)}
                        className="shrink-0 px-4 py-2 rounded-full text-xs font-black transition-all whitespace-nowrap"
                        style={{
                          backgroundColor: activeCategory === cat.name ? C.blue : C.white,
                          color: activeCategory === cat.name ? C.white : C.muted,
                          border: `1.5px solid ${activeCategory === cat.name ? C.blue : C.border}`,
                        }}>
                        {cat.emoji && <span className="mr-1">{cat.emoji}</span>}
                        {cat.name}
                      </button>
                    ))}
                  </div>
                )}

                {/* Grid por categoria */}
                {groupedProducts.map(({ category, items }) => (
                  <div key={category.id} className="space-y-3">
                    <h3 className="text-xs font-black uppercase tracking-widest flex items-center gap-1.5"
                      style={{ color: C.blue2 }}>
                      {category.emoji && <span className="text-base">{category.emoji}</span>}
                      {category.name}
                    </h3>
                    <div className="grid grid-cols-2 gap-3">
                      {items.map(p => (
                        <ProductCard key={p.id} p={p} adding={adding} onAdd={() => setConfirmItem(p)} />
                      ))}
                    </div>
                  </div>
                ))}

                {uncategorized.length > 0 && (
                  <div className="grid grid-cols-2 gap-3">
                    {uncategorized.map(p => (
                      <ProductCard key={p.id} p={p} adding={adding} onAdd={() => setConfirmItem(p)} />
                    ))}
                  </div>
                )}

                {filteredProducts.length === 0 && (
                  <p className="text-center text-sm py-8 font-semibold" style={{ color: C.muted }}>
                    Nenhum item encontrado.
                  </p>
                )}
              </div>
            )}
          </>
        )}
      </main>

      {/* Bottom sheet flutuante — comanda resumida */}
      {comanda && comanda.items.length > 0 && (
        <>
          {/* Overlay */}
          {showComandaSheet && (
            <div
              className="fixed inset-0 z-40 bg-black/30 backdrop-blur-sm"
              onClick={() => setShowComandaSheet(false)}
            />
          )}

          {/* Sheet */}
          <div
            className="fixed bottom-0 left-0 right-0 z-50 transition-transform duration-300 ease-out"
            style={{ transform: showComandaSheet ? 'translateY(0)' : 'translateY(calc(100% - 64px))' }}
          >
            <div className="max-w-lg mx-auto">
              {/* Handle — clique para abrir/fechar */}
              <button
                onClick={() => setShowComandaSheet(v => !v)}
                className="w-full flex items-center justify-between px-5 py-4 rounded-t-3xl shadow-[0_-4px_24px_rgba(12,61,90,0.22)]"
                style={{ backgroundColor: C.navy }}
              >
                <div className="flex items-center gap-3">
                  <ShoppingCart className="w-4 h-4 shrink-0" style={{ color: C.yellow }} />
                  <div className="text-left">
                    <p className="text-[10px] font-black uppercase tracking-widest leading-none"
                      style={{ color: 'rgba(255,255,255,0.5)' }}>
                      {comanda.items.length} {comanda.items.length === 1 ? 'item' : 'itens'}
                    </p>
                    <p className="text-base font-black leading-tight text-white">
                      R$ {comanda.totalInReais.toFixed(2).replace('.', ',')}
                    </p>
                  </div>
                </div>
                <ChevronDown
                  className="w-5 h-5 transition-transform duration-300"
                  style={{
                    color: C.yellow,
                    transform: showComandaSheet ? 'rotate(0deg)' : 'rotate(180deg)',
                  }}
                />
              </button>

              {/* Conteúdo da comanda */}
              <div className="max-h-72 overflow-y-auto px-5 pt-4 pb-6" style={{ backgroundColor: C.white }}>
                <div className="space-y-3">
                  {comanda.items.map((item, idx) => (
                    <div key={item.id}>
                      <div className="flex justify-between items-center gap-4">
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-bold leading-tight truncate" style={{ color: C.navy }}>
                            {item.itemNameSnapshot}
                          </p>
                          <p className="text-xs mt-0.5 font-medium" style={{ color: C.muted }}>
                            {item.quantity}× R$ {item.unitPriceInReais.toFixed(2).replace('.', ',')}
                          </p>
                        </div>
                        <span className="font-black text-sm shrink-0" style={{ color: C.blue2 }}>
                          R$ {item.subtotalInReais.toFixed(2).replace('.', ',')}
                        </span>
                      </div>
                      {idx < comanda.items.length - 1 && (
                        <div className="mt-3 border-b" style={{ borderColor: C.border }} />
                      )}
                    </div>
                  ))}
                </div>
                <div className="flex justify-between items-center mt-4 pt-4 border-t" style={{ borderColor: C.border }}>
                  <span className="font-black text-xs uppercase tracking-wide" style={{ color: C.muted }}>Total</span>
                  <span className="text-2xl font-black" style={{ color: C.navy }}>
                    R$ {comanda.totalInReais.toFixed(2).replace('.', ',')}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  )
}
