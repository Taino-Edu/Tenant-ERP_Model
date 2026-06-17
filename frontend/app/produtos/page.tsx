'use client'
import { useEffect, useState, useMemo } from 'react'
import { productApi, Product } from '@/lib/api'
import Link from 'next/link'
import {
  Package, ShoppingBag, X, ChevronLeft,
  Sun, Moon, Search, Tag,
} from 'lucide-react'

const NAVY = '#0C3D5A'

type Theme = { bg: string; card: string; border: string; blue: string; yellow: string; text: string; navy: string; muted: string }

function fmt(v: number) { return `R$ ${v.toFixed(2).replace('.', ',')}` }

function ProductBadge({ p }: { p: Product }) {
  if (p.isPreVenda) return (
    <span className="absolute top-2 left-2 text-[9px] font-black uppercase tracking-wide px-1.5 py-0.5 rounded text-white z-10"
      style={{ backgroundColor: '#7C3AED' }}>Pré-venda</span>
  )
  if (p.isOnPromo) return (
    <span className="absolute top-2 left-2 text-[9px] font-black uppercase tracking-wide px-1.5 py-0.5 rounded text-white z-10"
      style={{ backgroundColor: '#EF4444' }}>Promoção</span>
  )
  return null
}

function ProductCard({ p, onClick, C }: { p: Product; onClick: () => void; C: Theme }) {
  return (
    <button
      onClick={onClick}
      className="relative text-left rounded-2xl overflow-hidden border transition-all hover:scale-[1.02] active:scale-95 flex flex-col"
      style={{ backgroundColor: C.card, borderColor: C.border }}
    >
      <ProductBadge p={p} />
      <div className="w-full aspect-square overflow-hidden flex items-center justify-center p-3"
        style={{ backgroundColor: C.bg }}>
        {p.imageUrl
          ? <img src={p.imageUrl} alt={p.name} className="w-full h-full object-contain" />
          : <Package className="w-10 h-10 opacity-20" style={{ color: C.blue }} />
        }
      </div>
      <div className="p-3 flex flex-col gap-1 flex-1">
        <p className="text-xs font-bold leading-snug line-clamp-2 flex-1" style={{ color: C.navy }}>{p.name}</p>
        <div className="flex items-center justify-between mt-1 gap-1">
          {p.isOnPromo && p.discountPriceInReais != null ? (
            <div className="flex flex-col">
              <span className="text-[10px] line-through" style={{ color: C.muted }}>
                {fmt(p.priceInReais)}
              </span>
              <span className="text-sm font-black" style={{ color: '#EF4444' }}>
                {fmt(p.discountPriceInReais)}
              </span>
            </div>
          ) : (
            <span className="text-sm font-black" style={{ color: C.blue }}>{fmt(p.priceInReais)}</span>
          )}
          <span className="text-[10px]" style={{ color: C.muted }}>
            {p.stockQuantity > 0 ? `${p.stockQuantity} un.` : 'Esgotado'}
          </span>
        </div>
      </div>
    </button>
  )
}

function ProductModal({ p, onClose, C }: { p: Product; onClose: () => void; C: Theme }) {
  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-0 sm:p-4 bg-black/60 backdrop-blur-sm"
      onClick={e => { if (e.target === e.currentTarget) onClose() }}>
      <div className="relative w-full sm:max-w-md rounded-t-3xl sm:rounded-2xl overflow-hidden max-h-[90vh] flex flex-col"
        style={{ backgroundColor: C.card, border: `1px solid ${C.border}` }}>
        <button onClick={onClose}
          className="absolute top-3 right-3 z-10 w-8 h-8 rounded-full flex items-center justify-center transition-colors"
          style={{ backgroundColor: C.bg, color: C.navy }}>
          <X className="w-4 h-4" />
        </button>
        {p.imageUrl && (
          <div className="w-full h-56 shrink-0 overflow-hidden" style={{ backgroundColor: C.bg }}>
            <img src={p.imageUrl} alt={p.name} className="w-full h-full object-contain p-4" />
          </div>
        )}
        <div className="p-5 overflow-y-auto">
          <div className="flex gap-2 mb-2 flex-wrap">
            {p.isPreVenda && (
              <span className="text-[9px] font-black uppercase tracking-wide px-2 py-1 rounded text-white" style={{ backgroundColor: '#7C3AED' }}>Pré-venda</span>
            )}
            {!p.isPreVenda && p.isOnPromo && (
              <span className="text-[9px] font-black uppercase tracking-wide px-2 py-1 rounded text-white" style={{ backgroundColor: '#EF4444' }}>Promoção</span>
            )}
            <span className="text-[9px] font-bold uppercase tracking-wide px-2 py-1 rounded border" style={{ color: C.text, borderColor: C.border }}>{p.category}</span>
          </div>
          <h2 className="text-xl font-black leading-tight mb-1" style={{ color: C.navy }}>{p.name}</h2>
          {p.description && <p className="text-sm leading-relaxed mb-4" style={{ color: C.text }}>{p.description}</p>}
          <div className="flex items-baseline gap-3 mb-4">
            {p.isOnPromo && p.discountPriceInReais != null ? (
              <>
                <span className="text-2xl font-black" style={{ color: '#EF4444' }}>{fmt(p.discountPriceInReais)}</span>
                <span className="text-sm line-through" style={{ color: C.muted }}>{fmt(p.priceInReais)}</span>
              </>
            ) : (
              <span className="text-2xl font-black" style={{ color: C.blue }}>{fmt(p.priceInReais)}</span>
            )}
          </div>
          <p className="text-xs mb-4" style={{ color: p.stockQuantity > 0 ? C.text : '#EF4444' }}>
            {p.stockQuantity > 0 ? `${p.stockQuantity} unidades disponíveis` : 'Produto esgotado'}
          </p>
          <a
            href={`https://wa.me/5517997633103?text=${encodeURIComponent(`Olá! Tenho interesse no produto: ${p.name}`)}`}
            target="_blank" rel="noopener noreferrer"
            className="flex items-center justify-center gap-2 w-full py-3 rounded-xl font-black text-sm transition-all active:scale-95"
            style={{ backgroundColor: '#25D366', color: '#fff' }}>
            Falar no WhatsApp
          </a>
        </div>
      </div>
    </div>
  )
}

export default function ProdutosPage() {
  const [products, setProducts] = useState<Product[]>([])
  const [loading,  setLoading]  = useState(true)
  const [search,   setSearch]   = useState('')
  const [catFilter, setCatFilter] = useState<string | null>(null)
  const [modal,    setModal]    = useState<Product | null>(null)
  const [isDark,   setIsDark]   = useState(false)

  const C: Theme = isDark ? {
    bg: '#121215', card: '#1A1A1F', border: 'rgba(255,255,255,0.07)',
    blue: '#3EC2F2', yellow: '#FFE45E', text: 'rgba(255,255,255,0.60)', navy: '#FFFFFF', muted: 'rgba(255,255,255,0.35)',
  } : {
    bg: '#EBF7FD', card: '#FFFFFF', border: 'rgba(12,61,90,0.10)',
    blue: '#3EC2F2', yellow: '#FFE45E', text: '#4D8FAC', navy: '#0C3D5A', muted: '#9CA3AF',
  }

  useEffect(() => {
    const saved = localStorage.getItem('landing-theme')
    if (saved === 'dark') setIsDark(true)
    productApi.list()
      .then(r => setProducts(r.data.filter(p => p.isActive && p.stockQuantity > 0 && p.showOnSite !== false)))
      .finally(() => setLoading(false))
  }, [])

  const categories = useMemo(() => [...new Set(products.map(p => p.category))].sort(), [products])

  const filtered = useMemo(() => {
    const q = search.toLowerCase()
    return products.filter(p => {
      const matchCat = catFilter ? p.category === catFilter : true
      const matchSearch = !q || p.name.toLowerCase().includes(q) || p.category.toLowerCase().includes(q)
      return matchCat && matchSearch
    })
  }, [products, search, catFilter])

  const grouped = useMemo(() => {
    if (catFilter) return { [catFilter]: filtered }
    return categories.reduce<Record<string, Product[]>>((acc, cat) => {
      const items = filtered.filter(p => p.category === cat)
      if (items.length) acc[cat] = items
      return acc
    }, {})
  }, [filtered, categories, catFilter])

  return (
    <div className="min-h-screen" style={{ backgroundColor: C.bg }}>

      {/* Navbar */}
      <nav className="fixed inset-x-0 top-0 z-50 h-14 flex items-center px-5 gap-4"
        style={{ backgroundColor: '#0F3460', backdropFilter: 'blur(16px)' }}>
        <Link href="/" className="flex items-center gap-1.5 shrink-0 hover:opacity-80 transition-opacity"
          style={{ color: '#ffffff' }}>
          <ChevronLeft className="w-4 h-4" />
          <span className="text-sm font-semibold">Início</span>
        </Link>
        <div className="flex-1 flex justify-center">
          <span className="font-black text-lg" style={{ color: '#ffffff' }}>Loja</span>
        </div>
        <button onClick={() => {
          const next = !isDark
          setIsDark(next)
          localStorage.setItem('landing-theme', next ? 'dark' : 'light')
        }} className="p-2 rounded-xl hover:bg-white/10 transition-colors" style={{ color: '#ffffff' }}>
          {isDark ? <Sun className="w-4 h-4" /> : <Moon className="w-4 h-4" />}
        </button>
      </nav>

      <div className="max-w-6xl mx-auto px-5 pt-20 pb-16">

        {/* Busca */}
        <div className="relative mb-6">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4" style={{ color: C.muted }} />
          <input
            className="w-full pl-10 pr-4 py-3 rounded-2xl border text-sm focus:outline-none transition-all"
            style={{ backgroundColor: C.card, borderColor: C.border, color: C.navy }}
            placeholder="Buscar produto..."
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>

        {/* Filtro de categorias */}
        {categories.length > 1 && (
          <div className="flex gap-2 overflow-x-auto pb-2 mb-8" style={{ scrollbarWidth: 'none' }}>
            <button
              onClick={() => setCatFilter(null)}
              className="shrink-0 flex items-center gap-1.5 text-xs font-bold px-3.5 py-2 rounded-xl border transition-all"
              style={!catFilter
                ? { backgroundColor: C.blue, borderColor: C.blue, color: '#fff' }
                : { backgroundColor: C.card, borderColor: C.border, color: C.text }}>
              <Tag className="w-3 h-3" /> Todos
            </button>
            {categories.map(cat => (
              <button key={cat}
                onClick={() => setCatFilter(catFilter === cat ? null : cat)}
                className="shrink-0 text-xs font-bold px-3.5 py-2 rounded-xl border transition-all"
                style={catFilter === cat
                  ? { backgroundColor: C.blue, borderColor: C.blue, color: '#fff' }
                  : { backgroundColor: C.card, borderColor: C.border, color: C.text }}>
                {cat}
              </button>
            ))}
          </div>
        )}

        {/* Produtos */}
        {loading ? (
          <div className="flex justify-center py-24">
            <div className="w-8 h-8 border-2 rounded-full animate-spin" style={{ borderColor: C.blue, borderTopColor: 'transparent' }} />
          </div>
        ) : Object.keys(grouped).length === 0 ? (
          <div className="text-center py-24">
            <ShoppingBag className="w-12 h-12 mx-auto mb-4 opacity-20" style={{ color: C.navy }} />
            <p className="font-bold opacity-40" style={{ color: C.navy }}>Nenhum produto encontrado.</p>
          </div>
        ) : (
          <div className="space-y-12">
            {Object.entries(grouped).map(([cat, items]) => (
              <section key={cat}>
                <div className="flex items-center gap-3 mb-5">
                  <h2 className="text-lg font-black" style={{ color: C.navy }}>{cat}</h2>
                  <span className="text-xs font-bold px-2 py-0.5 rounded-lg" style={{ backgroundColor: C.blue + '22', color: C.blue }}>
                    {items.length}
                  </span>
                  <div className="flex-1 h-px" style={{ backgroundColor: C.border }} />
                </div>
                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3">
                  {items.map(p => (
                    <ProductCard key={p.id} p={p} onClick={() => setModal(p)} C={C} />
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
      </div>

      {modal && <ProductModal p={modal} onClose={() => setModal(null)} C={C} />}
    </div>
  )
}
