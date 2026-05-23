'use client'
import { useEffect, useState } from 'react'
import { productApi, Product } from '@/lib/api'
import toast from 'react-hot-toast'
import { Plus, Edit2, Trash2, AlertTriangle, Package, Search, X, Loader2, Check } from 'lucide-react'
import ImageUpload from '@/components/admin/ImageUpload'

const CATEGORIES = ['Bebida', 'Salgadinho', 'Acessório', 'Carta Avulsa', 'Deck Pronto', 'Sleeves', 'Outro']

function ProductModal({
  product, onClose, onSave
}: {
  product: Partial<Product> | null
  onClose: () => void
  onSave:  (p: Partial<Product>) => Promise<void>
}) {
  const [form, setForm]   = useState<Partial<Product>>(product ?? { stockQuantity: 0, minimumStock: 5, priceInCents: 0, costPriceInCents: 0 })
  const [saving, setSaving] = useState(false)
  const set = (k: keyof Product, v: unknown) => setForm(f => ({ ...f, [k]: v }))

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault(); setSaving(true)
    try { await onSave(form) } finally { setSaving(false) }
  }

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="card w-full max-w-md animate-bounce-in">
        <div className="flex items-center justify-between mb-6">
          <h2 className="text-lg font-bold text-white">{form.id ? 'Editar Produto' : 'Novo Produto'}</h2>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-300"><X className="w-5 h-5" /></button>
        </div>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="label">Nome *</label>
            <input className="input" required value={form.name ?? ''} onChange={e => set('name', e.target.value)} placeholder="Ex: Coca-Cola Lata 350ml" />
          </div>
          <div>
            <label className="label">Categoria *</label>
            <select className="input" required value={form.category ?? ''} onChange={e => set('category', e.target.value)}>
              <option value="">Selecione...</option>
              {CATEGORIES.map(c => <option key={c}>{c}</option>)}
            </select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Preço de custo (R$)</label>
              <input className="input" type="number" min="0" step="0.01"
                value={(form.costPriceInCents ?? 0) / 100}
                onChange={e => set('costPriceInCents', Math.round(parseFloat(e.target.value || '0') * 100))}
                placeholder="0,00"
              />
            </div>
            <div>
              <label className="label">Preço de venda (R$) *</label>
              <input className="input" type="number" min="0" step="0.01" required
                value={(form.priceInCents ?? 0) / 100}
                onChange={e => set('priceInCents', Math.round(parseFloat(e.target.value || '0') * 100))}
                placeholder="0,00"
              />
            </div>
          </div>
          {(form.costPriceInCents ?? 0) > 0 && (form.priceInCents ?? 0) > 0 && (() => {
            const cost  = (form.costPriceInCents ?? 0) / 100
            const price = (form.priceInCents ?? 0) / 100
            const margin = price - cost
            const pct    = ((margin / cost) * 100).toFixed(1)
            return (
              <div className="rounded-lg bg-surface-700/60 px-4 py-2 text-sm flex gap-4 flex-wrap">
                <span className="text-gray-400">Margem: <span className={margin >= 0 ? 'text-emerald-400 font-semibold' : 'text-red-400 font-semibold'}>R$ {margin.toFixed(2).replace('.', ',')} ({pct}%)</span></span>
              </div>
            )
          })()}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Estoque *</label>
              <input className="input" type="number" min="0" required
                value={form.stockQuantity ?? 0}
                onChange={e => set('stockQuantity', parseInt(e.target.value))}
              />
            </div>
            <div>
              <label className="label">Estoque mínimo</label>
              <input className="input" type="number" min="0"
                value={form.minimumStock ?? 5}
                onChange={e => set('minimumStock', parseInt(e.target.value))}
              />
            </div>
          </div>
          <div>
            <label className="label">Descrição</label>
            <input className="input" value={form.description ?? ''} onChange={e => set('description', e.target.value)} placeholder="Opcional" />
          </div>
          <div>
            <ImageUpload
              label="Imagem do produto"
              currentUrl={form.imageUrl ?? null}
              onUpload={url => set('imageUrl', url || null)}
            />
          </div>
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
            <button type="submit" disabled={saving} className="btn-primary flex-1 justify-center">
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
              {saving ? 'Salvando...' : 'Salvar'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export default function EstoquePage() {
  const [products, setProducts] = useState<Product[]>([])
  const [loading, setLoading]   = useState(true)
  const [search, setSearch]     = useState('')
  const [modal, setModal]       = useState<Partial<Product> | null | undefined>(undefined) // undefined = fechado
  const [catFilter, setCatFilter] = useState('')

  const fetch = async () => {
    setLoading(true)
    try { const { data } = await productApi.list(); setProducts(data) }
    catch { toast.error('Erro ao carregar produtos') }
    finally { setLoading(false) }
  }
  useEffect(() => { fetch() }, [])

  const filtered = products.filter(p =>
    p.name.toLowerCase().includes(search.toLowerCase()) &&
    (!catFilter || p.category === catFilter)
  )

  async function handleSave(form: Partial<Product>) {
    try {
      if (form.id) await productApi.update(form.id, form)
      else         await productApi.create(form)
      toast.success(form.id ? 'Produto atualizado!' : 'Produto criado!')
      setModal(undefined); fetch()
    } catch { toast.error('Erro ao salvar produto') }
  }

  async function handleDeactivate(id: string, name: string) {
    if (!confirm(`Desativar "${name}"?`)) return
    try { await productApi.deactivate(id); toast.success('Produto desativado'); fetch() }
    catch { toast.error('Erro ao desativar') }
  }

  async function handleStock(id: string, delta: number) {
    try { await productApi.adjustStock(id, delta); fetch() }
    catch { toast.error('Estoque insuficiente') }
  }

  return (
    <div className="p-6 space-y-6">
      {modal !== undefined && (
        <ProductModal product={modal} onClose={() => setModal(undefined)} onSave={handleSave} />
      )}

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white">Estoque</h1>
          <p className="text-gray-400 text-sm mt-0.5">{products.length} produtos cadastrados</p>
        </div>
        <button onClick={() => setModal(null)} className="btn-primary">
          <Plus className="w-4 h-4" /> Novo Produto
        </button>
      </div>

      {/* Filtros */}
      <div className="flex flex-col sm:flex-row gap-3">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
          <input className="input pl-9" placeholder="Buscar produto..." value={search} onChange={e => setSearch(e.target.value)} />
        </div>
        <select className="input sm:w-48" value={catFilter} onChange={e => setCatFilter(e.target.value)}>
          <option value="">Todas as categorias</option>
          {CATEGORIES.map(c => <option key={c}>{c}</option>)}
        </select>
      </div>

      {/* Tabela */}
      {loading ? (
        <div className="flex justify-center py-16"><div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" /></div>
      ) : (
        <div className="card p-0 overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-surface-800 border-b border-surface-500">
              <tr className="text-left">
                {['Produto', 'Categoria', 'Custo', 'Venda', 'Margem', 'Estoque', 'Ações'].map(h => (
                  <th key={h} className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wider">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-surface-500">
              {filtered.map(p => (
                <tr key={p.id} className="hover:bg-surface-600/30 transition-colors">
                  <td className="px-4 py-3">
                    <p className="font-medium text-white">{p.name}</p>
                    {p.description && <p className="text-xs text-gray-500 truncate max-w-[200px]">{p.description}</p>}
                  </td>
                  <td className="px-4 py-3">
                    <span className="badge bg-surface-600 text-gray-300 border-surface-500">{p.category}</span>
                  </td>
                  <td className="px-4 py-3 font-mono text-gray-400 text-xs">
                    {p.costPriceInCents > 0 ? `R$ ${p.costPriceInReais.toFixed(2).replace('.', ',')}` : '—'}
                  </td>
                  <td className="px-4 py-3 font-mono text-accent-gold font-semibold">
                    R$ {p.priceInReais.toFixed(2).replace('.', ',')}
                  </td>
                  <td className="px-4 py-3 font-mono text-xs">
                    {p.costPriceInCents > 0
                      ? <span className={p.marginInReais >= 0 ? 'text-emerald-400' : 'text-red-400'}>
                          {p.marginPercent.toFixed(1)}%
                        </span>
                      : <span className="text-gray-600">—</span>
                    }
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <button onClick={() => handleStock(p.id, -1)} className="w-6 h-6 rounded bg-surface-600 hover:bg-red-600/30 text-gray-400 hover:text-red-400 transition-colors flex items-center justify-center text-lg leading-none">−</button>
                      <span className={p.isLowStock ? 'text-red-400 font-bold' : 'text-white'}>{p.stockQuantity}</span>
                      <button onClick={() => handleStock(p.id, +1)} className="w-6 h-6 rounded bg-surface-600 hover:bg-emerald-600/30 text-gray-400 hover:text-emerald-400 transition-colors flex items-center justify-center text-lg leading-none">+</button>
                      {p.isLowStock && <AlertTriangle className="w-3.5 h-3.5 text-red-400" aria-label="Estoque baixo" />}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <button onClick={() => setModal(p)} className="p-1.5 rounded hover:bg-brand-600/20 text-gray-500 hover:text-brand-400 transition-colors">
                        <Edit2 className="w-4 h-4" />
                      </button>
                      <button onClick={() => handleDeactivate(p.id, p.name)} className="p-1.5 rounded hover:bg-red-600/20 text-gray-500 hover:text-red-400 transition-colors">
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {filtered.length === 0 && (
            <div className="text-center py-12 text-gray-500">
              <Package className="w-8 h-8 mx-auto mb-2 text-gray-600" />
              Nenhum produto encontrado
            </div>
          )}
        </div>
      )}
    </div>
  )
}
