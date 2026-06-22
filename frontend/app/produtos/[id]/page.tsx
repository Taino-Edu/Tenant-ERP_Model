'use client'
import { useEffect, useState } from 'react'
import { useParams, useRouter } from 'next/navigation'
import { productApi, Product } from '@/lib/api'
import Link from 'next/link'
import { ChevronLeft, Package, ShoppingBag, MessageCircle, Sun, Moon, ChevronRight, Share2, Tag, CheckCircle } from 'lucide-react'

const NAVY = '#0C3D5A'
const BLUE = '#3EC2F2'
const YELLOW = '#FFE45E'
const WA_NUM = '5517997633103'

function fmt(v: number) { return `R$ ${v.toFixed(2).replace('.', ',')}` }

export default function ProductPage() {
  const { id } = useParams<{ id: string }>()
  const router = useRouter()
  const [product, setProduct] = useState<Product | null>(null)
  const [loading, setLoading] = useState(true)
  const [mainImg, setMainImg] = useState<string | null>(null)
  const [isDark, setIsDark] = useState(false)

  const C = isDark ? {
    bg: '#121215', card: '#1A1A1F', border: 'rgba(255,255,255,0.07)',
    navy: '#FFFFFF', text: 'rgba(255,255,255,0.65)', muted: 'rgba(255,255,255,0.35)',
    chip: '#1E1E24',
  } : {
    bg: '#F5F8FA', card: '#FFFFFF', border: 'rgba(12,61,90,0.10)',
    navy: NAVY, text: '#4D8FAC', muted: '#9CA3AF',
    chip: '#EBF7FD',
  }

  useEffect(() => {
    const saved = localStorage.getItem('landing-theme')
    if (saved === 'dark') setIsDark(true)
  }, [])

  useEffect(() => {
    if (!id) return
    productApi.get(id)
      .then(r => {
        setProduct(r.data)
        setMainImg(r.data.imageUrl)
      })
      .catch(() => router.replace('/produtos'))
      .finally(() => setLoading(false))
  }, [id, router])

  const allImgs = product
    ? [product.imageUrl, ...(product.imageUrls ?? [])].filter(Boolean) as string[]
    : []

  const waMsg = encodeURIComponent(
    `Olá! Tenho interesse no produto: ${product?.name ?? ''}\n${typeof window !== 'undefined' ? window.location.href : ''}`
  )

  if (loading) return (
    <div className="min-h-screen flex items-center justify-center" style={{ backgroundColor: '#F5F8FA' }}>
      <div className="w-8 h-8 border-2 border-t-transparent rounded-full animate-spin" style={{ borderColor: BLUE }} />
    </div>
  )

  if (!product) return null

  return (
    <div className="min-h-screen" style={{ backgroundColor: C.bg }}>

      {/* Nav */}
      <nav className="sticky top-0 z-40 h-14 flex items-center px-4 gap-3 border-b"
        style={{ backgroundColor: '#0F3460', borderColor: 'rgba(255,255,255,0.08)' }}>
        <button onClick={() => router.back()} className="flex items-center gap-1 text-sm hover:opacity-70 transition-opacity" style={{ color: '#fff' }}>
          <ChevronLeft className="w-4 h-4" /> Voltar
        </button>
        <div className="flex-1 min-w-0">
          <p className="text-sm font-semibold truncate" style={{ color: '#fff' }}>{product.name}</p>
        </div>
        <button onClick={() => { const n = !isDark; setIsDark(n); localStorage.setItem('landing-theme', n ? 'dark' : 'light') }}
          className="p-2 rounded-lg hover:bg-white/10 transition-colors" style={{ color: '#fff' }}>
          {isDark ? <Sun className="w-4 h-4" /> : <Moon className="w-4 h-4" />}
        </button>
        <button
          onClick={() => navigator.share?.({ title: product.name, url: window.location.href }).catch(() => navigator.clipboard?.writeText(window.location.href))}
          className="p-2 rounded-lg hover:bg-white/10 transition-colors" style={{ color: '#fff' }}>
          <Share2 className="w-4 h-4" />
        </button>
      </nav>

      <div className="max-w-5xl mx-auto px-4 py-6">
        <div className="flex flex-col lg:flex-row gap-6">

          {/* ── Galeria ── */}
          <div className="lg:w-[420px] shrink-0 space-y-3">
            {/* Imagem principal */}
            <div className="rounded-2xl overflow-hidden border flex items-center justify-center"
              style={{ backgroundColor: C.card, borderColor: C.border, aspectRatio: '1' }}>
              {mainImg
                ? <img src={mainImg} alt={product.name} className="w-full h-full object-contain p-6" />
                : <Package className="w-20 h-20 opacity-10" style={{ color: C.navy }} />
              }
            </div>
            {/* Thumbnails */}
            {allImgs.length > 1 && (
              <div className="flex gap-2 overflow-x-auto pb-1" style={{ scrollbarWidth: 'none' }}>
                {allImgs.map((url, i) => (
                  <button key={i} onClick={() => setMainImg(url)}
                    className="shrink-0 w-16 h-16 rounded-xl border-2 overflow-hidden transition-all"
                    style={{
                      borderColor: mainImg === url ? BLUE : C.border,
                      backgroundColor: C.card,
                    }}>
                    <img src={url} alt="" className="w-full h-full object-contain p-1" />
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* ── Info do produto ── */}
          <div className="flex-1 space-y-5">

            {/* Badges */}
            <div className="flex flex-wrap gap-2">
              {product.isPreVenda && (
                <span className="text-xs font-black uppercase tracking-wide px-3 py-1 rounded-full text-white"
                  style={{ backgroundColor: '#7C3AED' }}>Pré-venda</span>
              )}
              {!product.isPreVenda && product.isOnPromo && (
                <span className="text-xs font-black uppercase tracking-wide px-3 py-1 rounded-full text-white"
                  style={{ backgroundColor: '#EF4444' }}>Promoção</span>
              )}
              <span className="text-xs font-semibold px-3 py-1 rounded-full flex items-center gap-1"
                style={{ backgroundColor: C.chip, color: C.text }}>
                <Tag className="w-3 h-3" />{product.category}
              </span>
            </div>

            {/* Nome */}
            <h1 className="text-2xl md:text-3xl font-black leading-tight" style={{ color: C.navy }}>
              {product.name}
            </h1>

            {/* Preço */}
            <div className="rounded-2xl p-5 border" style={{ backgroundColor: C.card, borderColor: C.border }}>
              {product.isOnPromo && product.discountPriceInReais != null ? (
                <div>
                  <p className="text-sm line-through mb-1" style={{ color: C.muted }}>{fmt(product.priceInReais)}</p>
                  <p className="text-4xl font-black" style={{ color: '#EF4444' }}>{fmt(product.discountPriceInReais)}</p>
                  <p className="text-xs mt-1 font-semibold" style={{ color: '#22C55E' }}>
                    Economia de {fmt(product.priceInReais - product.discountPriceInReais)}
                  </p>
                </div>
              ) : (
                <p className="text-4xl font-black" style={{ color: BLUE }}>{fmt(product.priceInReais)}</p>
              )}
              <p className="text-xs mt-3 flex items-center gap-1.5" style={{ color: product.stockQuantity > 0 ? '#22C55E' : '#EF4444' }}>
                <CheckCircle className="w-3.5 h-3.5" />
                {product.stockQuantity > 0 ? `${product.stockQuantity} unidades em estoque` : 'Produto esgotado'}
              </p>
            </div>

            {/* CTAs */}
            <div className="flex flex-col sm:flex-row gap-3">
              <a href={`https://wa.me/${WA_NUM}?text=${waMsg}`}
                target="_blank" rel="noopener noreferrer"
                className="flex-1 flex items-center justify-center gap-2 py-4 rounded-2xl font-black text-base transition-all active:scale-95 shadow-lg"
                style={{ backgroundColor: '#25D366', color: '#fff', boxShadow: '0 8px 24px rgba(37,211,102,0.30)' }}>
                <MessageCircle className="w-5 h-5" /> Comprar pelo WhatsApp
              </a>
              <Link href="/produtos"
                className="flex items-center justify-center gap-2 px-6 py-4 rounded-2xl font-semibold text-sm border transition-all hover:opacity-80"
                style={{ borderColor: C.border, color: C.text, backgroundColor: C.card }}>
                <ShoppingBag className="w-4 h-4" /> Ver mais produtos
              </Link>
            </div>

            {/* Descrição curta */}
            {product.description && (
              <p className="text-base leading-relaxed" style={{ color: C.text }}>{product.description}</p>
            )}

            {/* Descrição completa */}
            {product.fullDescription && (
              <div className="rounded-2xl border p-5 space-y-2" style={{ backgroundColor: C.card, borderColor: C.border }}>
                <h2 className="text-sm font-black uppercase tracking-wide" style={{ color: C.navy }}>Descrição do produto</h2>
                <p className="text-sm leading-relaxed whitespace-pre-wrap" style={{ color: C.text }}>{product.fullDescription}</p>
              </div>
            )}

            {/* Detalhes técnicos */}
            <div className="rounded-2xl border divide-y text-sm" style={{ backgroundColor: C.card, borderColor: C.border }}>
              {([
                ['Categoria', product.category],
                ['Disponibilidade', product.stockQuantity > 0 ? 'Em estoque' : 'Esgotado'],
                product.barcode ? ['Código', product.barcode] : null,
              ].filter(Boolean) as string[][]).map(([label, value]) => (
                <div key={label as string} className="flex items-center justify-between px-5 py-3">
                  <span style={{ color: C.muted }}>{label}</span>
                  <span className="font-semibold" style={{ color: C.navy }}>{value}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
