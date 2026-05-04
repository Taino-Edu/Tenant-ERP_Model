'use client'
import { useEffect, useState, useCallback } from 'react'
import { comandaApi, productApi, ComandaDto, Product } from '@/lib/api'
import { getUserName } from '@/lib/auth'
import { startHub, stopHub } from '@/lib/signalr'
import toast, { Toaster } from 'react-hot-toast'
import { ShoppingCart, Plus, Trash2, Loader2, Clock, TableProperties, Receipt, PackageOpen, Star } from 'lucide-react'
import Link from 'next/link'
import clsx from 'clsx'

export default function ClientePage() {
  const [comanda, setComanda]   = useState<ComandaDto | null>(null)
  const [products, setProducts] = useState<Product[]>([])
  const [loading, setLoading]   = useState(true)
  const [adding, setAdding]     = useState<string | null>(null)

  const fetchComanda = useCallback(async () => {
    try { const { data } = await comandaApi.myComanda(); setComanda(data) }
    catch { /* comanda ainda não existe */ }
    finally { setLoading(false) }
  }, [])

  useEffect(() => {
    fetchComanda()
    productApi.list().then(r => setProducts(r.data)).catch(() => {})

    startHub().then(hub => {
      hub.on('ComandaClosed', () => {
        toast.success('Sua comanda foi fechada! Obrigado pela visita 🎉', { duration: 6000 })
        fetchComanda()
      })
      hub.on('ItemAddedByAdmin', (data: { itemName: string; newTotalInReais: number }) => {
        toast(`+${data.itemName} adicionado pelo atendente`, { icon: '🛒' })
        fetchComanda()
      })
    }).catch(() => {})

    return () => { stopHub() }
  }, [fetchComanda])

  async function addProduct(product: Product) {
    if (!comanda) return
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
    } catch { toast.error('Não foi possível adicionar o item.') }
    finally { setAdding(null) }
  }

  async function removeItem(itemId: string) {
    if (!comanda) return
    try {
      const { data } = await comandaApi.removeItem(comanda.id, itemId)
      setComanda(data)
    } catch { toast.error('Não foi possível remover o item.') }
  }

  const statusColors: Record<string, string> = {
    Aberta: 'text-blue-400', EmAndamento: 'text-amber-400',
    Fechada: 'text-emerald-400', Cancelada: 'text-red-400',
  }

  return (
    <div className="min-h-screen bg-surface-900 pb-32">
      <Toaster position="top-center" toastOptions={{ style: { background: '#1e1e28', color: '#fff', border: '1px solid #32323f' }}} />

      {/* Header */}
      <div className="bg-surface-800 border-b border-surface-500 px-4 py-4 sticky top-0 z-10">
        <div className="max-w-lg mx-auto flex items-center justify-between">
          <div>
            <p className="font-bold text-white">Olá, {getUserName() || 'Visitante'}!</p>
            {comanda && (
              <div className="flex items-center gap-3 mt-0.5">
                {comanda.tableIdentifier && (
                  <span className="text-xs text-gray-400 flex items-center gap-1">
                    <TableProperties className="w-3 h-3" />{comanda.tableIdentifier}
                  </span>
                )}
                <span className={clsx('text-xs font-medium', statusColors[comanda.status])}>
                  ● {comanda.status}
                </span>
              </div>
            )}
          </div>
          <div className="flex items-center gap-3">
            <Link href="/cliente/perfil" className="flex items-center gap-1.5 text-gray-400 hover:text-accent-gold transition-colors">
              <Star className="w-4 h-4" />
              <span className="text-xs font-medium">Pontos</span>
            </Link>
            <div className="flex items-center gap-1.5">
              <ShoppingCart className="w-5 h-5 text-gray-400" />
              <span className="font-bold text-white">{comanda?.items.length ?? 0}</span>
            </div>
          </div>
        </div>
      </div>

      <div className="max-w-lg mx-auto px-4 py-6 space-y-6">
        {loading ? (
          <div className="flex justify-center py-16">
            <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          </div>
        ) : !comanda ? (
          <div className="text-center py-16">
            <PackageOpen className="w-12 h-12 text-gray-600 mx-auto mb-3" />
            <p className="text-gray-400">Nenhuma comanda aberta.</p>
            <p className="text-gray-600 text-sm mt-1">Escaneie o QR Code da sua mesa.</p>
          </div>
        ) : (
          <>
            {/* Itens da comanda */}
            <div className="space-y-3">
              <h2 className="font-bold text-white flex items-center gap-2">
                <Receipt className="w-4.5 h-4.5 text-brand-400" /> Sua Comanda
              </h2>

              {comanda.items.length === 0 ? (
                <div className="card text-center py-8 text-gray-500">
                  <ShoppingCart className="w-8 h-8 mx-auto mb-2 text-gray-600" />
                  <p className="text-sm">Adicione itens abaixo</p>
                </div>
              ) : (
                <div className="card divide-y divide-surface-500 p-0 overflow-hidden">
                  {comanda.items.map(item => (
                    <div key={item.id} className="flex items-center gap-3 px-4 py-3">
                      <div className="flex-1 min-w-0">
                        <p className="text-sm text-white font-medium truncate">{item.itemNameSnapshot}</p>
                        <p className="text-xs text-gray-500">
                          {item.quantity}× R$ {item.unitPriceInReais.toFixed(2).replace('.', ',')}
                        </p>
                      </div>
                      <div className="flex items-center gap-2">
                        <span className="font-mono text-accent-gold text-sm font-semibold">
                          R$ {item.subtotalInReais.toFixed(2).replace('.', ',')}
                        </span>
                        {comanda.status !== 'Fechada' && comanda.status !== 'Cancelada' && (
                          <button onClick={() => removeItem(item.id)} className="p-1 rounded hover:bg-red-600/20 text-gray-600 hover:text-red-400 transition-colors">
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                        )}
                      </div>
                    </div>
                  ))}
                  <div className="flex justify-between items-center px-4 py-3 bg-surface-800">
                    <span className="font-bold text-white">Total</span>
                    <span className="text-2xl font-bold text-accent-gold">
                      R$ {comanda.totalInReais.toFixed(2).replace('.', ',')}
                    </span>
                  </div>
                </div>
              )}

              {/* Info da abertura */}
              <div className="flex items-center gap-1.5 text-xs text-gray-600">
                <Clock className="w-3 h-3" />
                Comanda aberta às {new Date(comanda.openedAt).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
              </div>
            </div>

            {/* Cardápio de produtos */}
            {comanda.status !== 'Fechada' && comanda.status !== 'Cancelada' && (
              <div className="space-y-3">
                <h2 className="font-bold text-white flex items-center gap-2">
                  <ShoppingCart className="w-4.5 h-4.5 text-brand-400" /> Adicionar ao Pedido
                </h2>
                <div className="grid grid-cols-2 gap-3">
                  {products.map(p => (
                    <button
                      key={p.id}
                      onClick={() => addProduct(p)}
                      disabled={adding === p.id || p.stockQuantity === 0}
                      className={clsx(
                        'card text-left hover:border-brand-500/50 transition-all duration-150 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed',
                        adding === p.id && 'border-brand-500/50 bg-brand-600/5'
                      )}
                    >
                      <div className="flex items-start justify-between gap-2 mb-2">
                        <span className="text-2xl">{getCategoryEmoji(p.category)}</span>
                        {adding === p.id
                          ? <Loader2 className="w-4 h-4 animate-spin text-brand-400 shrink-0" />
                          : <Plus className="w-4 h-4 text-gray-500 group-hover:text-brand-400 shrink-0" />
                        }
                      </div>
                      <p className="text-sm font-medium text-white leading-tight">{p.name}</p>
                      <p className="text-accent-gold font-bold text-sm mt-1">
                        R$ {p.priceInReais.toFixed(2).replace('.', ',')}
                      </p>
                      {p.stockQuantity === 0 && (
                        <p className="text-red-400 text-xs mt-1">Sem estoque</p>
                      )}
                    </button>
                  ))}
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}

function getCategoryEmoji(cat: string): string {
  const map: Record<string, string> = {
    'Bebida': '🥤', 'Salgadinho': '🍿', 'Acessório': '🎮',
    'Carta Avulsa': '🃏', 'Deck Pronto': '🗂️', 'Sleeves': '🧤', 'Outro': '📦',
  }
  return map[cat] ?? '📦'
}
