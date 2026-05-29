'use client'
import { useEffect, useState, useCallback } from 'react'
import { comandaApi, userApi, productApi, categoryApi, ComandaDto, Product, ProductCategory, UserProfile } from '@/lib/api'
import { getUserName } from '@/lib/auth'
import { startHub, stopHub, ComandaOpenedEvent } from '@/lib/signalr'
import toast, { Toaster } from 'react-hot-toast'
import { ShoppingCart, Plus, Trash2, Loader2, Clock, TableProperties, Receipt, PackageOpen, Star } from 'lucide-react'
import Link from 'next/link'
import clsx from 'clsx'

export default function ClientePage() {
  const [comanda, setComanda]         = useState<ComandaDto | null>(null)
  const [products, setProducts]       = useState<Product[]>([])
  const [categories, setCategories]   = useState<ProductCategory[]>([])
  const [profile, setProfile]         = useState<UserProfile | null>(null)
  const [loading, setLoading]         = useState(true)
  const [adding, setAdding]           = useState<string | null>(null)
  const [applyingPts,  setApplyingPts]  = useState(false)
  const [removingPts,  setRemovingPts]  = useState(false)

  const fetchComanda = useCallback(async () => {
    try { const { data } = await comandaApi.myComanda(); setComanda(data) }
    catch (err: unknown) {
      // 404 → comanda encerrada/cancelada ou não existe → limpa o estado
      // Outros erros (rede, 500) → mantém estado anterior para não confundir o cliente
      const status = (err as { response?: { status?: number } })?.response?.status
      if (status === 404) setComanda(null)
    }
    finally { setLoading(false) }
  }, [])

  useEffect(() => {
    fetchComanda()
    productApi.list().then(r => setProducts(r.data)).catch(() => {})
    categoryApi.list().then(r => setCategories(r.data)).catch(() => {})
    userApi.me().then(r => setProfile(r.data)).catch(() => {})

    startHub().then(hub => {
      // Admin abriu uma comanda pro cliente → entrar no grupo e buscar a comanda
      hub.on('ComandaOpened', async (data: ComandaOpenedEvent) => {
        await hub.invoke('JoinComandaGroup', data.comandaId).catch(() => {})
        await fetchComanda()
        toast.success('Sua comanda foi aberta! 🎉', { duration: 5000 })
      })

      // Admin fechou a comanda
      hub.on('ComandaClosed', () => {
        toast.success('Sua comanda foi fechada! Obrigado pela visita 🎉', { duration: 6000 })
        fetchComanda()
      })

      // Admin cancelou a comanda
      hub.on('ComandaCancelled', () => {
        toast.error('Sua comanda foi cancelada.', { duration: 6000 })
        fetchComanda()
      })

      // Admin adicionou item manualmente
      hub.on('ItemAddedByAdmin', (data: { itemName: string; newTotalInReais: number }) => {
        toast(`+${data.itemName} adicionado pelo atendente`, { icon: '🛒' })
        fetchComanda()
      })

      // Admin atualizou/removeu item → sincroniza a comanda
      hub.on('ComandaUpdated', () => {
        fetchComanda()
      })

      // Reconectou após queda → sincroniza estado
      hub.onreconnected(() => {
        fetchComanda()
      })
    }).catch(() => {})

    return () => { stopHub() }
  }, [fetchComanda])

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
    } catch { toast.error('Não foi possível adicionar o item.') }
    finally { setAdding(null) }
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

      {/* Modal de confirmação de item */}
      {confirmItem && (
        <div className="fixed inset-0 z-50 flex items-end justify-center p-4 bg-black/60 backdrop-blur-sm">
          <div className="bg-surface-800 border border-surface-600 rounded-2xl w-full max-w-sm p-5 space-y-4">
            <div>
              <p className="font-semibold text-white">{confirmItem.name}</p>
              <p className="text-accent-gold font-bold text-lg mt-0.5">
                R$ {confirmItem.priceInReais.toFixed(2).replace('.', ',')}
              </p>
            </div>
            <p className="text-gray-400 text-sm">Deseja adicionar este item à sua comanda?</p>
            <div className="flex gap-3">
              <button
                onClick={() => setConfirmItem(null)}
                className="btn-secondary flex-1 justify-center"
              >
                Cancelar
              </button>
              <button
                onClick={() => addProduct(confirmItem)}
                disabled={adding === confirmItem.id}
                className="btn-primary flex-1 justify-center"
              >
                {adding === confirmItem.id
                  ? <Loader2 className="w-4 h-4 animate-spin" />
                  : <Plus className="w-4 h-4" />
                }
                Adicionar
              </button>
            </div>
          </div>
        </div>
      )}

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

              {/* Pontos aplicados + botão para desfazer */}
              {comanda.pointsApplied > 0 && (
                <div className="flex items-center justify-between bg-amber-500/10 border border-amber-500/20 rounded-lg px-4 py-2.5 gap-2">
                  <div className="flex items-center gap-2 text-amber-400 text-sm font-medium">
                    <Star className="w-4 h-4 shrink-0" />
                    Pontos aplicados
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-amber-400 font-bold text-sm">
                      -{(comanda.pointsApplied / 100).toFixed(2).replace('.', ',')} pts
                    </span>
                    {comanda.status !== 'Fechada' && comanda.status !== 'Cancelada' && (
                      <button
                        onClick={removePoints}
                        disabled={removingPts}
                        title="Desfazer aplicação de pontos"
                        className="text-gray-500 hover:text-red-400 transition-colors disabled:opacity-40"
                      >
                        {removingPts
                          ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                          : <Trash2 className="w-3.5 h-3.5" />}
                      </button>
                    )}
                  </div>
                </div>
              )}

              {/* Botão usar pontos */}
              {comanda.status !== 'Fechada' && comanda.status !== 'Cancelada' &&
               comanda.pointsApplied === 0 &&
               profile && profile.pointsBalance > 0 && !profile.pointsExpired && (
                <button
                  onClick={applyPoints}
                  disabled={applyingPts}
                  className="w-full flex items-center justify-between bg-amber-500/10 hover:bg-amber-500/20 border border-amber-500/30 rounded-lg px-4 py-2.5 transition-colors"
                >
                  <div className="flex items-center gap-2 text-amber-400 text-sm font-medium">
                    <Star className="w-4 h-4" />
                    Usar {profile.pointsBalance} pontos
                  </div>
                  {applyingPts
                    ? <Loader2 className="w-4 h-4 animate-spin text-amber-400" />
                    : <span className="text-xs text-amber-500">Aplicar →</span>
                  }
                </button>
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
                      onClick={() => p.stockQuantity > 0 && setConfirmItem(p)}
                      disabled={adding === p.id || p.stockQuantity === 0}
                      className={clsx(
                        'card text-left hover:border-brand-500/50 transition-all duration-150 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed',
                        adding === p.id && 'border-brand-500/50 bg-brand-600/5'
                      )}
                    >
                      <div className="flex items-start justify-between gap-2 mb-2">
                        <span className="text-2xl">{getCategoryEmoji(p.category, categories)}</span>
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

function getCategoryEmoji(cat: string, categories: ProductCategory[]): string {
  const found = categories.find(c => c.name === cat)
  return found?.emoji ?? '📦'
}
