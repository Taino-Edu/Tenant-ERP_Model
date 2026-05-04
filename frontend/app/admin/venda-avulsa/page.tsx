'use client'
import { useEffect, useState } from 'react'
import { productApi, comandaApi, Product, ComandaDto } from '@/lib/api'
import toast from 'react-hot-toast'
import {
  ShoppingBag, Plus, Minus, Trash2, User,
  CheckCircle, RotateCcw, Loader2, Receipt, PackageOpen
} from 'lucide-react'
import clsx from 'clsx'

// ── Tipo local para o carrinho ────────────────────────────────────────────────
interface CartItem {
  product: Product
  quantity: number
}

// ── Componente principal ──────────────────────────────────────────────────────
export default function VendaAvulsaPage() {
  const [products, setProducts]   = useState<Product[]>([])
  const [cart, setCart]           = useState<CartItem[]>([])
  const [clientName, setClientName] = useState('')
  const [loading, setLoading]     = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [receipt, setReceipt]     = useState<ComandaDto | null>(null)
  const [search, setSearch]       = useState('')

  useEffect(() => {
    productApi.list()
      .then(r => setProducts(r.data.filter(p => p.isActive && p.stockQuantity > 0)))
      .catch(() => toast.error('Erro ao carregar produtos'))
      .finally(() => setLoading(false))
  }, [])

  // ── Carrinho ────────────────────────────────────────────────────────────────

  function addToCart(product: Product) {
    setCart(prev => {
      const existing = prev.find(i => i.product.id === product.id)
      if (existing) {
        // Limita pela quantidade em estoque
        if (existing.quantity >= product.stockQuantity) {
          toast.error(`Estoque máximo: ${product.stockQuantity} un.`, { id: product.id })
          return prev
        }
        return prev.map(i => i.product.id === product.id
          ? { ...i, quantity: i.quantity + 1 }
          : i
        )
      }
      return [...prev, { product, quantity: 1 }]
    })
  }

  function changeQty(productId: string, delta: number) {
    setCart(prev => prev
      .map(i => i.product.id === productId
        ? { ...i, quantity: Math.max(1, Math.min(i.quantity + delta, i.product.stockQuantity)) }
        : i
      )
    )
  }

  function removeFromCart(productId: string) {
    setCart(prev => prev.filter(i => i.product.id !== productId))
  }

  function clearCart() {
    setCart([])
    setClientName('')
    setReceipt(null)
  }

  const total = cart.reduce((sum, i) => sum + (i.product.priceInCents * i.quantity), 0)

  // ── Registrar venda ─────────────────────────────────────────────────────────

  async function handleSubmit() {
    if (cart.length === 0) { toast.error('Adicione pelo menos um produto.'); return }
    setSubmitting(true)
    try {
      const { data } = await comandaApi.vendaAvulsa(
        clientName.trim() || null,
        cart.map(i => ({ productId: i.product.id, quantity: i.quantity }))
      )
      setReceipt(data)
      setCart([])
      setClientName('')
      toast.success('Venda registrada com sucesso!')
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao registrar venda. Tente novamente.')
    } finally {
      setSubmitting(false)
    }
  }

  // ── Filtro de busca ─────────────────────────────────────────────────────────

  const filtered = products.filter(p =>
    p.name.toLowerCase().includes(search.toLowerCase()) ||
    p.category.toLowerCase().includes(search.toLowerCase())
  )

  // ── Tela de comprovante ─────────────────────────────────────────────────────

  if (receipt) {
    return (
      <div className="p-6 max-w-lg mx-auto">
        <div className="card space-y-5 text-center">
          <div className="w-16 h-16 bg-accent-green/10 rounded-2xl flex items-center justify-center mx-auto">
            <CheckCircle className="w-8 h-8 text-accent-green" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-white">Venda Registrada!</h2>
            <p className="text-gray-400 text-sm mt-1">
              Cliente: <span className="text-gray-200">{receipt.userName}</span>
            </p>
          </div>

          <div className="bg-surface-800 rounded-xl divide-y divide-surface-500 text-left">
            {receipt.items.map(item => (
              <div key={item.id} className="flex justify-between items-center px-4 py-3 text-sm">
                <span className="text-gray-300">
                  {item.quantity}× {item.itemNameSnapshot}
                </span>
                <span className="text-gray-400 font-mono">
                  R$ {item.subtotalInReais.toFixed(2).replace('.', ',')}
                </span>
              </div>
            ))}
            <div className="flex justify-between items-center px-4 py-3 font-bold">
              <span className="text-white">Total</span>
              <span className="text-accent-gold text-lg">
                R$ {receipt.totalInReais.toFixed(2).replace('.', ',')}
              </span>
            </div>
          </div>

          <p className="text-xs text-gray-600">
            {new Date(receipt.openedAt).toLocaleString('pt-BR')}
          </p>

          <button onClick={clearCart} className="btn-primary w-full justify-center">
            <RotateCcw className="w-4 h-4" /> Nova Venda
          </button>
        </div>
      </div>
    )
  }

  // ── Tela principal ──────────────────────────────────────────────────────────

  return (
    <div className="p-6 h-full">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-white flex items-center gap-2">
          <ShoppingBag className="w-6 h-6 text-brand-400" /> Venda Avulsa
        </h1>
        <p className="text-gray-400 text-sm mt-0.5">
          Venda direta no balcão — sem QR Code ou login do cliente
        </p>
      </div>

      <div className="flex gap-6 h-[calc(100vh-180px)]">

        {/* ── Coluna esquerda: catálogo de produtos ──────────────────────────── */}
        <div className="flex-1 flex flex-col min-w-0">
          {/* Busca */}
          <input
            className="input mb-4"
            placeholder="Buscar produto ou categoria..."
            value={search}
            onChange={e => setSearch(e.target.value)}
          />

          {loading ? (
            <div className="flex-1 flex items-center justify-center">
              <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <div className="flex-1 flex flex-col items-center justify-center text-gray-500 gap-3">
              <PackageOpen className="w-10 h-10 text-gray-600" />
              <p className="text-sm">Nenhum produto encontrado</p>
            </div>
          ) : (
            <div className="flex-1 overflow-y-auto grid grid-cols-2 xl:grid-cols-3 gap-3 content-start pr-1">
              {filtered.map(p => {
                const inCart = cart.find(i => i.product.id === p.id)
                return (
                  <button
                    key={p.id}
                    onClick={() => addToCart(p)}
                    className={clsx(
                      'card text-left hover:border-brand-500/50 transition-all duration-150 active:scale-95 relative',
                      inCart && 'border-brand-500/40 bg-brand-600/5'
                    )}
                  >
                    {inCart && (
                      <span className="absolute top-2 right-2 w-5 h-5 bg-brand-600 rounded-full text-xs text-white flex items-center justify-center font-bold">
                        {inCart.quantity}
                      </span>
                    )}
                    <p className="text-xs text-gray-500 mb-1">{p.category}</p>
                    <p className="text-sm font-medium text-white leading-tight line-clamp-2">{p.name}</p>
                    <div className="flex items-center justify-between mt-2">
                      <p className="text-accent-gold font-bold text-sm">
                        R$ {p.priceInReais.toFixed(2).replace('.', ',')}
                      </p>
                      <span className="text-xs text-gray-600">{p.stockQuantity} un.</span>
                    </div>
                    <div className="mt-2 flex items-center justify-center w-full gap-1 text-xs text-brand-400 opacity-0 group-hover:opacity-100 transition-opacity">
                      <Plus className="w-3 h-3" /> Adicionar
                    </div>
                  </button>
                )
              })}
            </div>
          )}
        </div>

        {/* ── Coluna direita: carrinho ────────────────────────────────────────── */}
        <div className="w-80 flex flex-col gap-4 shrink-0">

          {/* Nome do cliente */}
          <div className="card">
            <label className="label text-xs">Nome do cliente (opcional)</label>
            <div className="relative">
              <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
              <input
                className="input pl-9 text-sm"
                placeholder="Cliente Balcão"
                value={clientName}
                onChange={e => setClientName(e.target.value)}
                maxLength={100}
              />
            </div>
          </div>

          {/* Itens do carrinho */}
          <div className="card flex-1 flex flex-col min-h-0">
            <div className="flex items-center justify-between mb-3">
              <h2 className="font-semibold text-white flex items-center gap-2 text-sm">
                <Receipt className="w-4 h-4 text-brand-400" /> Itens
              </h2>
              {cart.length > 0 && (
                <button
                  onClick={() => setCart([])}
                  className="text-xs text-gray-600 hover:text-red-400 transition-colors"
                >
                  Limpar
                </button>
              )}
            </div>

            {cart.length === 0 ? (
              <div className="flex-1 flex flex-col items-center justify-center text-gray-600 gap-2">
                <ShoppingBag className="w-8 h-8" />
                <p className="text-xs">Clique nos produtos para adicionar</p>
              </div>
            ) : (
              <div className="flex-1 overflow-y-auto space-y-2 pr-0.5">
                {cart.map(({ product, quantity }) => (
                  <div key={product.id} className="bg-surface-800 rounded-lg p-2.5">
                    <div className="flex items-start justify-between gap-2 mb-2">
                      <p className="text-xs text-white font-medium leading-tight flex-1">
                        {product.name}
                      </p>
                      <button
                        onClick={() => removeFromCart(product.id)}
                        className="text-gray-600 hover:text-red-400 transition-colors shrink-0"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>
                    <div className="flex items-center justify-between">
                      {/* Controle de quantidade */}
                      <div className="flex items-center gap-1.5">
                        <button
                          onClick={() => quantity === 1
                            ? removeFromCart(product.id)
                            : changeQty(product.id, -1)
                          }
                          className="w-6 h-6 rounded bg-surface-600 hover:bg-surface-500 flex items-center justify-center transition-colors"
                        >
                          <Minus className="w-3 h-3 text-gray-300" />
                        </button>
                        <span className="text-sm font-bold text-white w-5 text-center">{quantity}</span>
                        <button
                          onClick={() => changeQty(product.id, 1)}
                          disabled={quantity >= product.stockQuantity}
                          className="w-6 h-6 rounded bg-surface-600 hover:bg-surface-500 flex items-center justify-center transition-colors disabled:opacity-40"
                        >
                          <Plus className="w-3 h-3 text-gray-300" />
                        </button>
                      </div>
                      <span className="text-accent-gold font-bold text-sm font-mono">
                        R$ {(product.priceInCents * quantity / 100).toFixed(2).replace('.', ',')}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Total + botão */}
          <div className="card space-y-3">
            <div className="flex justify-between items-center">
              <span className="text-gray-400 text-sm">Total</span>
              <span className="text-2xl font-bold text-accent-gold">
                R$ {(total / 100).toFixed(2).replace('.', ',')}
              </span>
            </div>
            <button
              onClick={handleSubmit}
              disabled={cart.length === 0 || submitting}
              className="btn-success w-full justify-center py-3 text-base disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {submitting
                ? <><Loader2 className="w-5 h-5 animate-spin" /> Registrando...</>
                : <><CheckCircle className="w-5 h-5" /> Registrar Venda</>
              }
            </button>
          </div>

        </div>
      </div>
    </div>
  )
}
