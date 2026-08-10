'use client'

import { useEffect, useMemo, useState } from 'react'
import { CheckCircle, Loader2, PackageCheck, X } from 'lucide-react'
import toast from 'react-hot-toast'
import Modal from '@/components/admin/ui/Modal'
import { api, getErrorMessage } from '@/lib/api'

type Variant = { id: string; label: string; sku?: string; stockQuantity: number }
type Product = {
  id: string
  name: string
  barcode?: string
  stockQuantity: number
  costPriceInCents: number
  ncm?: string
  hasVariants: boolean
  variants: Variant[]
}
type SourceItem = {
  itemNumber: number
  supplierProductCode: string
  description: string
  gtin?: string
  ncm?: string
  unit?: string
  xmlQuantity: number
  suggestedQuantity?: number
  suggestedUnitCostInCents: number
  lineTotal: number
  suggestedProductId?: string
  suggestedVariantId?: string
  matchReason?: string
}
type Preview = {
  notaId: string
  chaveAcesso: string
  supplierName?: string
  total: number
  alreadyReceivedAt?: string
  items: SourceItem[]
  products: Product[]
}
type ReceiptRow = SourceItem & {
  productId: string
  variantId: string
  quantity: string
  unitCost: string
  ignore: boolean
}

function money(value: number) {
  return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

export function NfeReceiptModal({ notaId, onClose, onReceived }: {
  notaId: string
  onClose: () => void
  onReceived: () => void
}) {
  const [preview, setPreview] = useState<Preview | null>(null)
  const [rows, setRows] = useState<ReceiptRow[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    let active = true
    api.get<Preview>(`/api/contas-receber/notas-destinadas/${notaId}/recebimento`)
      .then(({ data }) => {
        if (!active) return
        setPreview(data)
        setRows(data.items.map(item => ({
          ...item,
          productId: item.suggestedProductId ?? '',
          variantId: item.suggestedVariantId ?? '',
          quantity: item.suggestedQuantity?.toString() ?? '',
          unitCost: (item.suggestedUnitCostInCents / 100).toFixed(2),
          ignore: false,
        })))
      })
      .catch(err => {
        toast.error(getErrorMessage(err, 'Não foi possível preparar o recebimento'))
        onClose()
      })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [notaId, onClose])

  const productMap = useMemo(
    () => new Map((preview?.products ?? []).map(product => [product.id, product])),
    [preview?.products],
  )

  function updateRow(itemNumber: number, patch: Partial<ReceiptRow>) {
    setRows(current => current.map(row => row.itemNumber === itemNumber ? { ...row, ...patch } : row))
  }

  async function confirm() {
    for (const row of rows) {
      if (row.ignore) continue
      const product = productMap.get(row.productId)
      if (!product) { toast.error(`Selecione o produto de “${row.description}”.`); return }
      if (product.hasVariants && !row.variantId) { toast.error(`Selecione a variante de “${row.description}”.`); return }
      if (!Number.isInteger(Number(row.quantity)) || Number(row.quantity) <= 0) {
        toast.error(`Informe uma quantidade inteira para “${row.description}”.`); return
      }
      if (row.unitCost.trim() === '' || Number(row.unitCost.replace(',', '.')) < 0) {
        toast.error(`Confira o custo de “${row.description}”.`); return
      }
    }

    setSaving(true)
    try {
      const payload = {
        items: rows.map(row => ({
          itemNumber: row.itemNumber,
          productId: row.ignore ? null : row.productId,
          productVariantId: row.ignore || !row.variantId ? null : row.variantId,
          quantity: row.ignore ? 0 : Number(row.quantity),
          unitCostInCents: row.ignore ? 0 : Math.round(Number(row.unitCost.replace(',', '.')) * 100),
          ignore: row.ignore,
          ignoreReason: row.ignore ? 'Item marcado como sem controle de estoque' : null,
        })),
      }
      const { data } = await api.post(`/api/contas-receber/notas-destinadas/${notaId}/receber`, payload)
      toast.success(`${data.receivedUnits} unidade(s) adicionada(s) ao estoque.`)
      onReceived()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não foi possível concluir o recebimento'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal onClose={onClose} closeOnBackdrop={false} className="p-0 overflow-hidden max-w-5xl">
      <div className="flex items-start justify-between gap-4 px-5 py-4 border-b border-surface-600">
        <div>
          <h2 className="font-black text-white flex items-center gap-2">
            <PackageCheck className="w-5 h-5 text-brand-400" /> Receber mercadoria
          </h2>
          <p className="text-xs text-gray-500 mt-1">
            Confira os vínculos uma vez; o sistema lembrará nas próximas notas deste fornecedor.
          </p>
        </div>
        <button onClick={onClose} aria-label="Fechar" className="text-gray-400 hover:text-white">
          <X className="w-5 h-5" />
        </button>
      </div>

      {loading || !preview ? (
        <div className="flex justify-center py-20"><Loader2 className="w-7 h-7 animate-spin text-brand-400" /></div>
      ) : (
        <>
          <div className="px-5 py-3 bg-surface-800/50 border-b border-surface-700 flex flex-wrap gap-x-6 gap-y-1 text-sm">
            <span className="text-gray-400">Fornecedor: <strong className="text-white">{preview.supplierName ?? 'Não identificado'}</strong></span>
            <span className="text-gray-400">Total da nota: <strong className="text-white">{money(preview.total)}</strong></span>
            <span className="text-gray-500 font-mono text-xs self-center" title={preview.chaveAcesso}>
              Chave {preview.chaveAcesso.slice(0, 8)}…{preview.chaveAcesso.slice(-8)}
            </span>
          </div>

          <div className="max-h-[62vh] overflow-y-auto p-4 space-y-3">
            {rows.map(row => {
              const selected = productMap.get(row.productId)
              return (
                <section key={row.itemNumber} className="rounded-xl border border-surface-600 bg-surface-800/35 p-4">
                  <div className="flex flex-wrap items-start justify-between gap-2 mb-3">
                    <div>
                      <p className="text-sm font-semibold text-white">{row.itemNumber}. {row.description}</p>
                      <p className="text-xs text-gray-500 mt-0.5">
                        Cód. fornecedor {row.supplierProductCode} · XML: {row.xmlQuantity} {row.unit ?? 'un'} · {money(row.lineTotal)}
                        {row.gtin ? ` · EAN ${row.gtin}` : ''}{row.ncm ? ` · NCM ${row.ncm}` : ' · NCM não informado no XML'}
                      </p>
                    </div>
                    {row.matchReason ? (
                      <span className="text-[10px] rounded-full border border-green-500/30 bg-green-500/10 text-green-400 px-2 py-1">
                        ✓ {row.matchReason}
                      </span>
                    ) : null}
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-12 gap-3 items-end">
                    <label className="md:col-span-5 text-xs text-gray-400">
                      Produto no estoque
                      <select
                        value={row.productId}
                        disabled={row.ignore}
                        onChange={event => updateRow(row.itemNumber, { productId: event.target.value, variantId: '' })}
                        className="input w-full mt-1 disabled:opacity-50"
                      >
                        <option value="">Selecione…</option>
                        {preview.products.map(product => (
                          <option key={product.id} value={product.id}>{product.name} · estoque {product.stockQuantity}</option>
                        ))}
                      </select>
                    </label>

                    {selected?.hasVariants ? (
                      <label className="md:col-span-3 text-xs text-gray-400">
                        Variante
                        <select
                          value={row.variantId}
                          disabled={row.ignore}
                          onChange={event => updateRow(row.itemNumber, { variantId: event.target.value })}
                          className="input w-full mt-1 disabled:opacity-50"
                        >
                          <option value="">Selecione…</option>
                          {selected.variants.map(variant => (
                            <option key={variant.id} value={variant.id}>{variant.label || variant.sku || 'Variante'} · {variant.stockQuantity}</option>
                          ))}
                        </select>
                      </label>
                    ) : null}

                    <label className={`${selected?.hasVariants ? 'md:col-span-2' : 'md:col-span-3'} text-xs text-gray-400`}>
                      Quantidade
                      <input
                        type="number" min="1" step="1" value={row.quantity} disabled={row.ignore}
                        onChange={event => updateRow(row.itemNumber, { quantity: event.target.value })}
                        className="input w-full mt-1 disabled:opacity-50"
                        placeholder={row.suggestedQuantity ? undefined : 'Informe em unidades'}
                      />
                    </label>
                    <label className="md:col-span-2 text-xs text-gray-400">
                      Custo unitário
                      <input
                        type="number" min="0" step="0.01" value={row.unitCost} disabled={row.ignore}
                        onChange={event => updateRow(row.itemNumber, { unitCost: event.target.value })}
                        className="input w-full mt-1 disabled:opacity-50"
                      />
                    </label>
                  </div>

                  {row.suggestedQuantity == null ? (
                    <p className="text-xs text-amber-400 mt-2">A quantidade do XML é fracionada. Informe quantas unidades entram no estoque.</p>
                  ) : null}
                  {!row.ignore && selected && row.ncm && !selected.ncm ? (
                    <p className="text-xs text-green-400 mt-2">
                      NCM {row.ncm} será preenchido no produto a partir desta NF-e de entrada.
                    </p>
                  ) : null}
                  {!row.ignore && selected?.ncm && row.ncm && selected.ncm !== row.ncm ? (
                    <p className="text-xs text-amber-400 mt-2">
                      Divergência de NCM: XML {row.ncm} × cadastro {selected.ncm}. O cadastro não será sobrescrito; revise com o contador.
                    </p>
                  ) : null}
                  <label className="inline-flex items-center gap-2 text-xs text-gray-400 mt-3 cursor-pointer">
                    <input
                      type="checkbox" checked={row.ignore}
                      onChange={event => updateRow(row.itemNumber, { ignore: event.target.checked })}
                      className="accent-brand-500"
                    />
                    Não controla estoque (serviço, frete ou outro item)
                  </label>
                </section>
              )
            })}
          </div>

          <div className="px-5 py-4 border-t border-surface-600 flex flex-col sm:flex-row sm:items-center gap-3">
            <p className="text-xs text-gray-500 flex-1">
              Ao confirmar, estoque, custo médio, NCM ausente e histórico da nota serão atualizados juntos. A mesma NF-e não poderá ser recebida novamente.
            </p>
            <button onClick={onClose} disabled={saving} className="px-4 py-2.5 rounded-xl bg-surface-700 text-gray-300 text-sm font-semibold">
              Voltar
            </button>
            <button onClick={confirm} disabled={saving} className="btn-primary px-5 py-2.5">
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle className="w-4 h-4" />}
              {saving ? 'Recebendo…' : 'Confirmar entrada'}
            </button>
          </div>
        </>
      )}
    </Modal>
  )
}
