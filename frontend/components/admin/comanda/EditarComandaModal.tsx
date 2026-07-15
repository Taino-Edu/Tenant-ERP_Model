'use client'
import { useState } from 'react'
import { ComandaDto, UserSummary, Product, EditarComandaRequest, EditarItemRequest, COMANDA_PAYMENT_METHODS } from '@/lib/api'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import { X, UserSearch, Search, Plus, Trash2, CheckCircle, Loader2 } from 'lucide-react'
import { fmt, EditItemState } from './shared'

export function EditarComandaModal({
  comanda,
  clientes,
  produtos,
  onSave,
  onClose,
}: {
  comanda: ComandaDto
  clientes: UserSummary[]
  produtos: Product[]
  onSave: (req: EditarComandaRequest) => Promise<void>
  onClose: () => void
}) {
  const { site } = useSiteConfig()
  const paymentMethods = site.pontosFidelidadeAtivo ? COMANDA_PAYMENT_METHODS : COMANDA_PAYMENT_METHODS.filter(m => m.value !== 'Pontos')
  const [pm,       setPm]       = useState(comanda.paymentMethod ?? 'Dinheiro')
  const [pm2,      setPm2]      = useState(comanda.secondPaymentMethod ?? '')
  const [pm2val,   setPm2val]   = useState(String(comanda.secondPaymentAmountInCents / 100))
  const [desconto, setDesconto] = useState(String(comanda.discountInCents / 100))
  const [clienteId, setClienteId] = useState(comanda.userId)
  const [clienteSearch, setClienteSearch] = useState('')
  const [showClienteList, setShowClienteList] = useState(false)
  const [items, setItems] = useState<EditItemState[]>(
    comanda.items.map(i => ({
      id: i.id, productId: i.productId ?? undefined,
      itemName: i.itemNameSnapshot, unitPriceInCents: i.unitPriceInCents,
      quantity: i.quantity, remover: false,
    }))
  )
  const [prodSearch, setProdSearch] = useState('')
  const [showProdList, setShowProdList] = useState(false)
  const [saving, setSaving] = useState(false)

  const nomeCliente = clientes.find(u => u.id === clienteId)?.name ?? comanda.userName
  const filteredClientes = clientes.filter(u =>
    u.name.toLowerCase().includes(clienteSearch.toLowerCase()) ||
    (u.cpf ?? '').includes(clienteSearch)
  ).slice(0, 8)
  const filteredProds = produtos.filter(p =>
    p.name.toLowerCase().includes(prodSearch.toLowerCase()) && p.isActive
  ).slice(0, 6)

  function updateItem(idx: number, patch: Partial<EditItemState>) {
    setItems(prev => prev.map((it, i) => i === idx ? { ...it, ...patch } : it))
  }
  function removeItem(idx: number) {
    setItems(prev => prev.map((it, i) => i === idx ? { ...it, remover: true } : it))
  }
  function addManualItem() {
    setItems(prev => [...prev, { itemName: '', unitPriceInCents: 0, quantity: 1, remover: false }])
  }
  function addProductItem(p: Product) {
    const effectivePrice = p.isOnPromo && p.discountPriceInCents != null
      ? p.discountPriceInCents : p.priceInCents
    setItems(prev => [...prev, {
      productId: p.id, itemName: p.name,
      unitPriceInCents: effectivePrice, quantity: 1, remover: false,
    }])
    setProdSearch(''); setShowProdList(false)
  }

  async function handleSave() {
    setSaving(true)
    try {
      const descontoNum = Math.round(parseFloat(desconto.replace(',', '.') || '0') * 100)
      const pm2valNum   = Math.round(parseFloat(pm2val.replace(',', '.') || '0') * 100)

      const itens: EditarItemRequest[] = items.map(it => ({
        comandaItemId: it.id,
        remover: it.remover,
        productId: it.productId,
        itemName: it.itemName,
        unitPriceInCents: it.unitPriceInCents,
        quantity: it.quantity,
      }))

      await onSave({
        paymentMethod: pm || undefined,
        secondPaymentMethod: pm2 === '' ? '' : pm2 || undefined,
        secondPaymentAmountInCents: pm2 ? pm2valNum : 0,
        novoClienteId: clienteId !== comanda.userId ? clienteId : undefined,
        descontoEmCentavos: descontoNum,
        itens,
      })
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-[70] flex items-end sm:items-center justify-center bg-black/60 backdrop-blur-sm p-4"
      onClick={e => { if (e.target === e.currentTarget) onClose() }}>
      <div className="w-full max-w-lg bg-surface-800 rounded-3xl shadow-2xl flex flex-col max-h-[92vh] overflow-hidden">

        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-surface-600 shrink-0">
          <div>
            <h2 className="text-sm font-bold text-white">Editar Comanda</h2>
            <p className="text-xs text-gray-500 mt-0.5">{comanda.userName} · {fmt(comanda.totalInReais)}</p>
          </div>
          <button onClick={onClose} className="p-1.5 text-gray-500 hover:text-gray-300 rounded-xl hover:bg-surface-700 transition-colors">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="overflow-y-auto flex-1 p-5 space-y-5">

          {/* Pagamento */}
          <div>
            <p className="text-[10px] font-black text-gray-500 uppercase tracking-widest mb-2">Forma de Pagamento</p>
            <select value={pm} onChange={e => setPm(e.target.value)}
              className="w-full bg-surface-700 border border-surface-500 text-white rounded-xl px-3 py-2 text-sm">
              {paymentMethods.map(m => (
                <option key={m.value} value={m.value}>{m.label}</option>
              ))}
            </select>
          </div>

          {/* Segundo pagamento */}
          <div>
            <p className="text-[10px] font-black text-gray-500 uppercase tracking-widest mb-2">Segundo Pagamento (split)</p>
            <div className="flex gap-2">
              <select value={pm2} onChange={e => setPm2(e.target.value)}
                className="flex-1 bg-surface-700 border border-surface-500 text-white rounded-xl px-3 py-2 text-sm">
                <option value="">Nenhum</option>
                {paymentMethods.map(m => (
                  <option key={m.value} value={m.value}>{m.label}</option>
                ))}
              </select>
              {pm2 && (
                <input type="number" step="0.01" min="0" value={pm2val} onChange={e => setPm2val(e.target.value)}
                  placeholder="Valor R$"
                  className="w-28 bg-surface-700 border border-surface-500 text-white rounded-xl px-3 py-2 text-sm" />
              )}
            </div>
          </div>

          {/* Desconto */}
          <div>
            <p className="text-[10px] font-black text-gray-500 uppercase tracking-widest mb-2">Desconto (R$)</p>
            <input type="number" step="0.01" min="0" value={desconto} onChange={e => setDesconto(e.target.value)}
              className="w-full bg-surface-700 border border-surface-500 text-white rounded-xl px-3 py-2 text-sm" />
          </div>

          {/* Cliente */}
          <div className="relative">
            <p className="text-[10px] font-black text-gray-500 uppercase tracking-widest mb-2">Cliente</p>
            <div className="flex items-center gap-2 bg-surface-700 border border-surface-500 rounded-xl px-3 py-2">
              <UserSearch className="w-4 h-4 text-gray-500 shrink-0" />
              <input
                value={showClienteList ? clienteSearch : nomeCliente}
                onFocus={() => { setClienteSearch(''); setShowClienteList(true) }}
                onBlur={() => setTimeout(() => setShowClienteList(false), 150)}
                onChange={e => { setClienteSearch(e.target.value); setShowClienteList(true) }}
                className="flex-1 bg-transparent text-sm text-white outline-none"
                placeholder="Buscar cliente..."
              />
            </div>
            {showClienteList && filteredClientes.length > 0 && (
              <div className="absolute z-10 top-full left-0 right-0 mt-1 bg-surface-700 border border-surface-600 rounded-xl shadow-xl overflow-hidden">
                {filteredClientes.map(u => (
                  <button key={u.id} onMouseDown={() => { setClienteId(u.id); setShowClienteList(false) }}
                    className="w-full flex items-center gap-2 px-4 py-2 hover:bg-surface-500 transition-colors text-left">
                    <span className="text-sm text-white truncate">{u.name}</span>
                    {u.cpf && <span className="text-xs text-gray-500 shrink-0 font-mono">{u.cpf}</span>}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Itens */}
          <div>
            <p className="text-[10px] font-black text-gray-500 uppercase tracking-widest mb-2">Itens</p>
            <div className="space-y-2">
              {items.map((it, idx) => !it.remover && (
                <div key={idx} className="flex items-center gap-2 bg-surface-700 rounded-xl px-3 py-2">
                  <input value={it.itemName} onChange={e => updateItem(idx, { itemName: e.target.value })}
                    className="flex-1 bg-transparent text-xs text-white outline-none min-w-0"
                    placeholder="Nome do item" />
                  <input type="number" min="0.01" step="0.01"
                    value={(it.unitPriceInCents / 100).toFixed(2)}
                    onChange={e => updateItem(idx, { unitPriceInCents: Math.round(parseFloat(e.target.value || '0') * 100) })}
                    className="w-16 bg-surface-600 rounded-lg px-2 py-1 text-xs text-white text-right outline-none" />
                  <input type="number" min="1" step="1"
                    value={it.quantity}
                    onChange={e => updateItem(idx, { quantity: Math.max(1, parseInt(e.target.value) || 1) })}
                    className="w-10 bg-surface-600 rounded-lg px-2 py-1 text-xs text-white text-center outline-none" />
                  <button onClick={() => removeItem(idx)}
                    className="p-1 text-red-400 hover:text-red-300 hover:bg-red-400/10 rounded-lg transition-colors shrink-0">
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              ))}
            </div>

            {/* Adicionar produto */}
            <div className="mt-3 relative">
              <div className="flex gap-2">
                <div className="relative flex-1">
                  <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-gray-500" />
                  <input value={prodSearch}
                    onChange={e => { setProdSearch(e.target.value); setShowProdList(true) }}
                    onFocus={() => setShowProdList(true)}
                    onBlur={() => setTimeout(() => setShowProdList(false), 150)}
                    placeholder="Buscar produto no estoque..."
                    className="w-full pl-8 pr-3 py-2 bg-surface-700 border border-surface-600 rounded-xl text-xs text-white placeholder-gray-500 outline-none" />
                  {showProdList && filteredProds.length > 0 && (
                    <div className="absolute z-10 top-full left-0 right-0 mt-1 bg-surface-700 border border-surface-600 rounded-xl shadow-xl overflow-hidden">
                      {filteredProds.map(p => {
                        const onPromo = p.isOnPromo && p.discountPriceInReais != null
                        return (
                          <button key={p.id} onMouseDown={() => addProductItem(p)}
                            className="w-full flex items-center justify-between gap-2 px-3 py-2 hover:bg-surface-500 transition-colors text-left">
                            <span className="text-xs text-white truncate">
                              {p.name}{onPromo && <span className="text-red-400 ml-1">· promo</span>}
                            </span>
                            {onPromo ? (
                              <span className="text-xs shrink-0">
                                <span className="text-gray-500 line-through mr-1">{fmt(p.priceInReais)}</span>
                                <span className="text-red-400 font-semibold">{fmt(p.discountPriceInReais!)}</span>
                              </span>
                            ) : (
                              <span className="text-xs text-gray-400 shrink-0">{fmt(p.priceInReais)}</span>
                            )}
                          </button>
                        )
                      })}
                    </div>
                  )}
                </div>
                <button onClick={addManualItem}
                  className="shrink-0 flex items-center gap-1 px-3 py-2 bg-surface-700 border border-surface-600 rounded-xl text-xs text-gray-300 hover:text-white hover:border-brand-500 transition-colors">
                  <Plus className="w-3.5 h-3.5" /> Manual
                </button>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="px-5 py-4 border-t border-surface-600 shrink-0">
          <button onClick={handleSave} disabled={saving}
            className="w-full btn-primary justify-center py-2.5">
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle className="w-4 h-4" />}
            {saving ? 'Salvando...' : 'Salvar Alterações'}
          </button>
        </div>
      </div>
    </div>
  )
}
