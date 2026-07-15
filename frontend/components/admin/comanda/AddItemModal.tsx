'use client'
import { useEffect, useState, useRef } from 'react'
import { comandaApi, productApi, Product, ComandaDto, ProductVariant, getErrorMessage } from '@/lib/api'
import toast from 'react-hot-toast'
import CameraScanner from '@/components/CameraScanner'
import VariantPicker from '@/components/admin/VariantPicker'
import { XCircle, Search, ScanBarcode, Camera, Loader2, Plus } from 'lucide-react'
import clsx from 'clsx'
import { fmt } from './shared'

// ── Modal: adicionar item a uma comanda ───────────────────────────────────────

export function AddItemModal({
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
  const [variantPickerProduct, setVariantPickerProduct] = useState<Product | null>(null)
  const barcodeRef                      = useRef<HTMLInputElement>(null)

  useEffect(() => {
    productApi.listAdmin()
      // Produtos com grade têm o estoque de verdade nas variantes, não no
      // produto-pai — não pode filtrar por stockQuantity aqui, senão some da
      // lista mesmo tendo variante disponível.
      .then(r => setProducts(r.data.filter(p => p.isActive && (p.hasVariants || p.stockQuantity > 0))))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar produtos')))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    if (mode === 'barcode') setTimeout(() => barcodeRef.current?.focus(), 100)
  }, [mode])

  async function handleAdd(product: Product, variant?: ProductVariant) {
    if (product.hasVariants && !variant) {
      setVariantPickerProduct(product)
      return
    }
    setAdding(product.id)
    try {
      // Mesmo padrão de venda-avulsa: o preço usado é sempre o do produto-pai
      // (promoção ou base) — variante não tem preço próprio na UI hoje.
      const effectivePrice = product.isOnPromo && product.discountPriceInCents != null
        ? product.discountPriceInCents : product.priceInCents
      const { data } = await comandaApi.addItem(comandaId, {
        productId:        product.id,
        variantId:        variant?.id,
        itemName:         variant ? `${product.name} (${variant.label})` : product.name,
        unitPriceInCents: effectivePrice,
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
    } catch (err) {
      toast.error(getErrorMessage(err, 'Produto não encontrado para este código de barras.'))
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
    } catch (err) {
      toast.error(getErrorMessage(err, 'Produto não encontrado para este código de barras.'))
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
                filtered.map(p => {
                  const onPromo = p.isOnPromo && p.discountPriceInReais != null
                  return (
                    <button
                      key={p.id}
                      onClick={() => handleAdd(p)}
                      disabled={adding === p.id}
                      title={p.hasVariants ? 'Escolher tamanho/cor' : undefined}
                      className="w-full flex items-center justify-between px-3 py-2.5 rounded-lg hover:bg-surface-500 transition-colors text-left disabled:opacity-50"
                    >
                      <div>
                        <div className="flex items-center gap-1.5">
                          <p className="text-sm text-white font-medium">{p.name}</p>
                          {onPromo && (
                            <span className="text-[9px] font-black uppercase tracking-wide px-1.5 py-0.5 rounded-md bg-red-500/20 text-red-400">
                              Promoção
                            </span>
                          )}
                        </div>
                        <p className="text-xs text-gray-500">
                          {p.category} · {p.hasVariants ? 'grade (tamanho/cor)' : `${p.stockQuantity} un.`}
                        </p>
                      </div>
                      <div className="flex items-center gap-2 shrink-0 ml-3">
                        {onPromo ? (
                          <div className="flex flex-col items-end">
                            <span className="text-[10px] text-gray-500 line-through">{fmt(p.priceInReais)}</span>
                            <span className="text-red-400 text-sm font-bold">{fmt(p.discountPriceInReais!)}</span>
                          </div>
                        ) : (
                          <span className="text-accent-gold text-sm font-bold">{fmt(p.priceInReais)}</span>
                        )}
                        {adding === p.id
                          ? <Loader2 className="w-4 h-4 animate-spin text-brand-400" />
                          : <Plus className="w-4 h-4 text-brand-400" />
                        }
                      </div>
                    </button>
                  )
                })
              )}
            </div>
          </>
        )}
      </div>
    </div>

    {/* Seletor de variante (tamanho/cor) */}
    {variantPickerProduct && (
      <VariantPicker
        productId={variantPickerProduct.id}
        productName={variantPickerProduct.name}
        onConfirm={variant => {
          handleAdd(variantPickerProduct, variant)
          setVariantPickerProduct(null)
        }}
        onClose={() => setVariantPickerProduct(null)}
      />
    )}
    </>
  )
}
