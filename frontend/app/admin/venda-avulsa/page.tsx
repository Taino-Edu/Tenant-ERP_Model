'use client'
import { useEffect, useState, useMemo, useCallback } from 'react'
import { productApi, vendaAvulsaApi, PAYMENT_METHODS, Product, VendaAvulsaDto } from '@/lib/api'
import { useThrottle } from '@/lib/hooks'
import toast from 'react-hot-toast'
import {
  ShoppingBag, Plus, Minus, Trash2, User, CheckCircle, RotateCcw,
  Loader2, Receipt, PackageOpen, Banknote, CreditCard, QrCode,
  Tag, History, TrendingUp, Clock, FileText,
} from 'lucide-react'
import clsx from 'clsx'

interface CartItem { product: Product; quantity: number }

// ── Geração de PDF via print window ──────────────────────────────────────────

function printReceiptPDF(receipt: VendaAvulsaDto, payLabel: string) {
  const w = window.open('', '_blank', 'width=420,height=640')
  if (!w) { alert('Permita pop-ups para gerar o PDF'); return }

  const date = new Date(receipt.soldAt).toLocaleString('pt-BR')
  const itemsHTML = receipt.items.map(it => `
    <tr>
      <td>${it.quantity}× ${it.productName}</td>
      <td align="right">R$&nbsp;${it.subtotalInReais.toFixed(2).replace('.', ',')}</td>
    </tr>
  `).join('')

  const discountRow = receipt.discountPercent > 0 ? `
    <tr style="color:#16a34a;">
      <td>Desconto (${receipt.discountPercent}%)</td>
      <td align="right">−R$&nbsp;${receipt.discountInReais.toFixed(2).replace('.', ',')}</td>
    </tr>
  ` : ''

  w.document.write(`<!DOCTYPE html>
<html lang="pt-BR"><head>
<meta charset="UTF-8">
<title>Comprovante — softNerd</title>
<style>
  @page { size: 80mm auto; margin: 6mm; }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { font-family: 'Courier New', monospace; font-size: 11px; color: #111; padding: 8px; }
  h1 { font-size: 15px; text-align: center; letter-spacing: 1px; }
  .sub { text-align: center; font-size: 10px; color: #555; margin-bottom: 8px; }
  hr { border: none; border-top: 1px dashed #999; margin: 6px 0; }
  table { width: 100%; border-collapse: collapse; }
  td { padding: 2px 0; vertical-align: top; }
  .total-row td { font-size: 14px; font-weight: bold; padding-top: 4px; }
  .payment { font-size: 10px; color: #555; margin-top: 4px; }
  .footer { text-align: center; font-size: 10px; color: #777; margin-top: 10px; }
  @media print { body { padding: 0; } }
</style>
</head><body>
<h1>softNerd</h1>
<p class="sub">${date}</p>
${receipt.clientName ? `<p class="sub">Cliente: <strong>${receipt.clientName}</strong></p>` : ''}
<hr>
<table>
  ${itemsHTML}
  ${discountRow}
</table>
<hr>
<table>
  <tr class="total-row">
    <td>TOTAL</td>
    <td align="right">R$&nbsp;${receipt.totalInReais.toFixed(2).replace('.', ',')}</td>
  </tr>
</table>
<p class="payment">Pagamento: ${payLabel}</p>
<hr>
<p class="footer">Obrigado pela preferência! 🃏</p>
<script>window.onload = function() { window.print(); }<\/script>
</body></html>`)
  w.document.close()
}

function printDailyReportPDF(history: VendaAvulsaDto[], payMethods: typeof PAYMENT_METHODS) {
  const w = window.open('', '_blank', 'width=700,height=900')
  if (!w) { alert('Permita pop-ups para gerar o PDF'); return }

  const today = new Date().toLocaleDateString('pt-BR')
  const totalDia = history.reduce((s, v) => s + v.totalInReais, 0)

  // Agrupar por método de pagamento
  const byMethod = payMethods.map(m => ({
    label: m.label,
    total: history.filter(v => v.paymentMethod === m.value).reduce((s, v) => s + v.totalInReais, 0),
    count: history.filter(v => v.paymentMethod === m.value).length,
  })).filter(m => m.count > 0)

  const vendasHTML = history.map(v => {
    const payLabel = payMethods.find(m => m.value === v.paymentMethod)?.label ?? v.paymentMethod
    const hora = new Date(v.soldAt).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
    const itens = v.items.map(it => `${it.quantity}× ${it.productName}`).join(', ')
    return `<tr>
      <td>${hora}</td>
      <td>${v.clientName ?? '—'}</td>
      <td style="max-width:200px;font-size:10px;">${itens}</td>
      <td>${payLabel}</td>
      <td align="right" style="font-weight:bold;">R$&nbsp;${v.totalInReais.toFixed(2).replace('.', ',')}</td>
    </tr>`
  }).join('')

  const byMethodHTML = byMethod.map(m => `
    <tr>
      <td>${m.label}</td>
      <td align="center">${m.count} venda${m.count !== 1 ? 's' : ''}</td>
      <td align="right">R$&nbsp;${m.total.toFixed(2).replace('.', ',')}</td>
    </tr>`).join('')

  w.document.write(`<!DOCTYPE html>
<html lang="pt-BR"><head>
<meta charset="UTF-8">
<title>Relatório Diário — softNerd</title>
<style>
  @page { size: A4; margin: 15mm; }
  * { box-sizing: border-box; }
  body { font-family: Arial, sans-serif; font-size: 12px; color: #111; }
  h1 { font-size: 20px; margin-bottom: 4px; }
  h2 { font-size: 14px; margin: 16px 0 6px; color: #444; border-bottom: 1px solid #ddd; padding-bottom: 4px; }
  .meta { color: #666; font-size: 11px; margin-bottom: 16px; }
  .cards { display: flex; gap: 12px; margin-bottom: 16px; }
  .card { border: 1px solid #e0e0e0; border-radius: 8px; padding: 10px 16px; flex: 1; }
  .card .val { font-size: 22px; font-weight: bold; color: #6C3FC5; }
  .card .lbl { font-size: 10px; color: #888; }
  table { width: 100%; border-collapse: collapse; }
  th { background: #f4f4f8; text-align: left; padding: 5px 8px; font-size: 11px; color: #444; }
  td { padding: 4px 8px; border-bottom: 1px solid #f0f0f0; font-size: 11px; }
  tr:last-child td { border-bottom: none; }
  .total-row td { font-weight: bold; font-size: 13px; background: #f9f6ff; }
  @media print { body { padding: 0; } }
</style>
</head><body>
<h1>softNerd — Relatório Diário</h1>
<p class="meta">Data: ${today} &nbsp;|&nbsp; Gerado em: ${new Date().toLocaleTimeString('pt-BR')}</p>

<div class="cards">
  <div class="card">
    <div class="val">${history.length}</div>
    <div class="lbl">Vendas realizadas</div>
  </div>
  <div class="card">
    <div class="val">R$&nbsp;${totalDia.toFixed(2).replace('.', ',')}</div>
    <div class="lbl">Total do dia</div>
  </div>
</div>

<h2>Por Forma de Pagamento</h2>
<table>
  <thead><tr><th>Método</th><th>Qtd.</th><th>Total</th></tr></thead>
  <tbody>${byMethodHTML}</tbody>
  <tfoot>
    <tr class="total-row">
      <td>TOTAL GERAL</td>
      <td align="center">${history.length}</td>
      <td align="right">R$&nbsp;${totalDia.toFixed(2).replace('.', ',')}</td>
    </tr>
  </tfoot>
</table>

<h2>Detalhamento das Vendas</h2>
<table>
  <thead><tr><th>Hora</th><th>Cliente</th><th>Itens</th><th>Pagamento</th><th>Total</th></tr></thead>
  <tbody>${vendasHTML}</tbody>
</table>

<script>window.onload = function() { window.print(); }<\/script>
</body></html>`)
  w.document.close()
}

const PAYMENT_ICONS: Record<string, React.ReactNode> = {
  Pix:           <QrCode     className="w-4 h-4" />,
  Dinheiro:      <Banknote   className="w-4 h-4" />,
  CartaoCredito: <CreditCard className="w-4 h-4" />,
  CartaoDebito:  <CreditCard className="w-4 h-4" />,
}

const fmt = (n: number) => `R$ ${n.toFixed(2).replace('.', ',')}`

export default function VendaAvulsaPage() {
  const [tab, setTab]               = useState<'venda' | 'historico'>('venda')
  const [products, setProducts]     = useState<Product[]>([])
  const [cart, setCart]             = useState<CartItem[]>([])
  const [clientName, setClientName] = useState('')
  const [payment, setPayment]       = useState<string>(PAYMENT_METHODS[0].value)
  const [discountPct, setDiscount]  = useState(0)
  const [discountMode, setDiscMode] = useState<'total' | 'per_item'>('total')
  const [received, setReceived]     = useState('')
  const [loading, setLoading]       = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [receipt, setReceipt]       = useState<VendaAvulsaDto | null>(null)
  const [search, setSearch]         = useState('')
  const [catFilter, setCat]         = useState<string | null>(null)
  const [history, setHistory]       = useState<VendaAvulsaDto[]>([])
  const [histLoading, setHistLoad]  = useState(false)

  useEffect(() => {
    productApi.list()
      .then(r => setProducts(r.data.filter(p => p.isActive && p.stockQuantity > 0)))
      .catch(() => toast.error('Erro ao carregar produtos'))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    if (tab !== 'historico') return
    setHistLoad(true)
    vendaAvulsaApi.recent(100)
      .then(r => {
        const today = new Date().toDateString()
        setHistory(r.data.filter(v => new Date(v.soldAt).toDateString() === today))
      })
      .catch(() => toast.error('Erro ao carregar histórico'))
      .finally(() => setHistLoad(false))
  }, [tab])

  // ── Carrinho ──────────────────────────────────────────────────────────────

  function addToCart(product: Product) {
    setCart(prev => {
      const ex = prev.find(i => i.product.id === product.id)
      if (ex) {
        if (ex.quantity >= product.stockQuantity) {
          toast.error(`Estoque máximo: ${product.stockQuantity} un.`, { id: product.id })
          return prev
        }
        return prev.map(i => i.product.id === product.id ? { ...i, quantity: i.quantity + 1 } : i)
      }
      return [...prev, { product, quantity: 1 }]
    })
  }

  function changeQty(productId: string, delta: number) {
    setCart(prev => prev.map(i =>
      i.product.id === productId
        ? { ...i, quantity: Math.max(1, Math.min(i.quantity + delta, i.product.stockQuantity)) }
        : i
    ))
  }

  function removeFromCart(productId: string) {
    setCart(prev => prev.filter(i => i.product.id !== productId))
  }

  function clearAll() {
    setCart([]); setClientName(''); setPayment(PAYMENT_METHODS[0].value)
    setDiscount(0); setDiscMode('total'); setReceived(''); setReceipt(null)
  }

  const subtotal = cart.reduce((s, i) => s + i.product.priceInCents * i.quantity, 0)

  // Cálculo de desconto: "no total" abate do subtotal final;
  // "por item" abate de cada unidade (mas o total exibido é o mesmo — a diferença é semântica para o cupom)
  const discountCents = discountMode === 'total'
    ? Math.round(subtotal * discountPct / 100)
    : cart.reduce((s, i) => s + Math.round(i.product.priceInCents * discountPct / 100) * i.quantity, 0)

  const total = subtotal - discountCents
  const receivedCents  = Math.round(parseFloat(received.replace(',', '.') || '0') * 100)
  const troco          = receivedCents - total

  // ── Filtros ───────────────────────────────────────────────────────────────

  const categories = useMemo(
    () => [...new Set(products.map(p => p.category))].sort(),
    [products]
  )

  const filtered = products.filter(p => {
    const matchCat    = !catFilter || p.category === catFilter
    const q           = search.toLowerCase()
    const matchSearch = p.name.toLowerCase().includes(q) ||
                        p.category.toLowerCase().includes(q) ||
                        (p.barcode != null && p.barcode.includes(search))
    return matchCat && matchSearch
  })

  // ── Registrar venda ───────────────────────────────────────────────────────

  const submitRaw = useCallback(async () => {
    if (cart.length === 0) { toast.error('Adicione pelo menos um produto.'); return }
    setSubmitting(true)
    try {
      const { data } = await vendaAvulsaApi.register(
        clientName.trim() || null,
        payment,
        cart.map(i => ({ productId: i.product.id, quantity: i.quantity })),
        discountPct,
      )
      setReceipt(data)
      setCart([])
      setClientName('')
      setDiscount(0)
      setDiscMode('total')
      setReceived('')
      toast.success('Venda registrada!')
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao registrar venda.')
    } finally {
      setSubmitting(false)
    }
  }, [cart, clientName, payment, discountPct, discountMode])

  const handleSubmit = useThrottle(submitRaw, 2000)

  // ── Comprovante ───────────────────────────────────────────────────────────

  if (receipt) {
    const payLabel = PAYMENT_METHODS.find(m => m.value === receipt.paymentMethod)?.label ?? receipt.paymentMethod
    return (
      <div className="p-6 max-w-lg mx-auto">
        <div className="card space-y-5 text-center">
          <div className="w-16 h-16 bg-accent-green/10 rounded-2xl flex items-center justify-center mx-auto">
            <CheckCircle className="w-8 h-8 text-accent-green" />
          </div>

          <div>
            <h2 className="text-xl font-bold text-white">Venda Registrada!</h2>
            {receipt.clientName && (
              <p className="text-gray-400 text-sm mt-1">
                Cliente: <span className="text-gray-200">{receipt.clientName}</span>
              </p>
            )}
            <p className="text-gray-500 text-xs mt-1 flex items-center justify-center gap-1">
              {PAYMENT_ICONS[receipt.paymentMethod]} {payLabel}
            </p>
          </div>

          <div className="bg-surface-800 rounded-xl divide-y divide-surface-500 text-left">
            {receipt.items.map((item, idx) => (
              <div key={idx} className="flex justify-between items-center px-4 py-3 text-sm">
                <span className="text-gray-300">{item.quantity}× {item.productName}</span>
                <span className="text-gray-400 font-mono">{fmt(item.subtotalInReais)}</span>
              </div>
            ))}
            {receipt.discountPercent > 0 && (
              <div className="flex justify-between items-center px-4 py-3 text-sm">
                <span className="text-accent-green">Desconto ({receipt.discountPercent}%)</span>
                <span className="text-accent-green font-mono">−{fmt(receipt.discountInReais)}</span>
              </div>
            )}
            <div className="flex justify-between items-center px-4 py-3 font-bold">
              <span className="text-white">Total</span>
              <span className="text-accent-gold text-lg">{fmt(receipt.totalInReais)}</span>
            </div>
          </div>

          <p className="text-xs text-gray-600">{new Date(receipt.soldAt).toLocaleString('pt-BR')}</p>

          <div className="flex gap-2">
            <button
              onClick={() => printReceiptPDF(receipt, payLabel)}
              className="btn-secondary flex-1 justify-center"
            >
              <FileText className="w-4 h-4" /> Imprimir / PDF
            </button>
            <button onClick={clearAll} className="btn-primary flex-1 justify-center">
              <RotateCcw className="w-4 h-4" /> Nova Venda
            </button>
          </div>
        </div>
      </div>
    )
  }

  // ── Tela principal ────────────────────────────────────────────────────────

  return (
    <div className="p-6 h-full flex flex-col">

      {/* Header + tabs */}
      <div className="mb-4 flex items-end justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <ShoppingBag className="w-6 h-6 text-brand-400" /> Venda Avulsa
          </h1>
          <p className="text-gray-400 text-sm mt-0.5">Venda direta no balcão — sem QR Code</p>
        </div>
        <div className="flex gap-1 bg-surface-800 p-1 rounded-lg">
          <button
            onClick={() => setTab('venda')}
            className={clsx('px-4 py-1.5 rounded-md text-sm font-medium transition-all',
              tab === 'venda' ? 'bg-brand-600 text-white' : 'text-gray-400 hover:text-gray-200')}
          >
            <ShoppingBag className="w-4 h-4 inline mr-1.5" />Nova Venda
          </button>
          <button
            onClick={() => setTab('historico')}
            className={clsx('px-4 py-1.5 rounded-md text-sm font-medium transition-all',
              tab === 'historico' ? 'bg-brand-600 text-white' : 'text-gray-400 hover:text-gray-200')}
          >
            <History className="w-4 h-4 inline mr-1.5" />Histórico do Dia
          </button>
        </div>
      </div>

      {/* ── Tab: Histórico ─────────────────────────────────────────────────── */}
      {tab === 'historico' && (
        <HistoricoTab history={history} loading={histLoading} />
      )}

      {/* ── Tab: Nova Venda ────────────────────────────────────────────────── */}
      {tab === 'venda' && (
        <div className="flex gap-6 flex-1 min-h-0 h-[calc(100vh-200px)]">

          {/* Catálogo */}
          <div className="flex-1 flex flex-col min-w-0 gap-3">
            <input
              className="input"
              placeholder="Buscar produto, categoria ou código de barras..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              onKeyDown={e => {
                if (e.key === 'Enter' && filtered.length === 1) {
                  addToCart(filtered[0])
                  setSearch('')
                }
              }}
            />

            {/* Chips de categoria */}
            {categories.length > 0 && (
              <div className="flex flex-wrap gap-1.5">
                <button
                  onClick={() => setCat(null)}
                  className={clsx('px-3 py-1 rounded-full text-xs font-medium border transition-all',
                    !catFilter
                      ? 'bg-brand-600/20 border-brand-500/60 text-brand-300'
                      : 'bg-surface-800 border-surface-500 text-gray-400 hover:border-surface-400'
                  )}
                >
                  Todos
                </button>
                {categories.map(cat => (
                  <button
                    key={cat}
                    onClick={() => setCat(cat === catFilter ? null : cat)}
                    className={clsx('px-3 py-1 rounded-full text-xs font-medium border transition-all',
                      catFilter === cat
                        ? 'bg-brand-600/20 border-brand-500/60 text-brand-300'
                        : 'bg-surface-800 border-surface-500 text-gray-400 hover:border-surface-400'
                    )}
                  >
                    {cat}
                  </button>
                ))}
              </div>
            )}

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
                          {fmt(p.priceInReais)}
                        </p>
                        <span className="text-xs text-gray-600">{p.stockQuantity} un.</span>
                      </div>
                    </button>
                  )
                })}
              </div>
            )}
          </div>

          {/* Carrinho */}
          <div className="w-80 flex flex-col gap-3 shrink-0">

            {/* Cliente + pagamento */}
            <div className="card space-y-3">
              <div>
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

              <div>
                <label className="label text-xs">Forma de pagamento</label>
                <div className="grid grid-cols-2 gap-1.5">
                  {PAYMENT_METHODS.map(m => (
                    <button
                      key={m.value}
                      onClick={() => { setPayment(m.value); setReceived('') }}
                      className={clsx(
                        'flex items-center gap-2 px-3 py-2 rounded-lg text-xs font-medium border transition-all',
                        payment === m.value
                          ? 'bg-brand-600/20 border-brand-500/60 text-brand-300'
                          : 'bg-surface-800 border-surface-500 text-gray-400 hover:border-surface-400'
                      )}
                    >
                      {PAYMENT_ICONS[m.value]} {m.label}
                    </button>
                  ))}
                </div>
              </div>
            </div>

            {/* Itens */}
            <div className="card flex-1 flex flex-col min-h-0">
              <div className="flex items-center justify-between mb-3">
                <h2 className="font-semibold text-white flex items-center gap-2 text-sm">
                  <Receipt className="w-4 h-4 text-brand-400" /> Itens
                </h2>
                {cart.length > 0 && (
                  <button onClick={() => setCart([])} className="text-xs text-gray-600 hover:text-red-400 transition-colors">
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
                        <p className="text-xs text-white font-medium leading-tight flex-1">{product.name}</p>
                        <button onClick={() => removeFromCart(product.id)} className="text-gray-600 hover:text-red-400 transition-colors shrink-0">
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </div>
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-1.5">
                          <button
                            onClick={() => quantity === 1 ? removeFromCart(product.id) : changeQty(product.id, -1)}
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
                          {fmt(product.priceInCents * quantity / 100)}
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Desconto + Total + Troco */}
            <div className="card space-y-3">
              {/* Modo de desconto */}
              <div className="flex items-center gap-1 bg-surface-800 p-1 rounded-lg">
                <button
                  onClick={() => setDiscMode('total')}
                  className={clsx(
                    'flex-1 py-1 rounded-md text-xs font-medium transition-all',
                    discountMode === 'total'
                      ? 'bg-brand-600 text-white'
                      : 'text-gray-400 hover:text-gray-200'
                  )}
                >
                  No total
                </button>
                <button
                  onClick={() => setDiscMode('per_item')}
                  className={clsx(
                    'flex-1 py-1 rounded-md text-xs font-medium transition-all',
                    discountMode === 'per_item'
                      ? 'bg-brand-600 text-white'
                      : 'text-gray-400 hover:text-gray-200'
                  )}
                >
                  Por item
                </button>
              </div>

              {/* Percentual de desconto */}
              <div className="flex items-center gap-2">
                <Tag className="w-4 h-4 text-gray-500 shrink-0" />
                <label className="text-xs text-gray-400 shrink-0">Desconto</label>
                <div className="flex items-center gap-1 ml-auto">
                  {[0, 5, 10, 15, 20].map(d => (
                    <button
                      key={d}
                      onClick={() => setDiscount(d)}
                      className={clsx(
                        'w-8 h-7 rounded text-xs font-medium border transition-all',
                        discountPct === d
                          ? 'bg-accent-green/20 border-accent-green/60 text-accent-green'
                          : 'bg-surface-700 border-surface-500 text-gray-400 hover:border-surface-400'
                      )}
                    >
                      {d === 0 ? '—' : `${d}%`}
                    </button>
                  ))}
                </div>
              </div>

              {discountPct > 0 && (
                <div className="flex justify-between text-sm">
                  <span className="text-gray-400">Subtotal</span>
                  <span className="text-gray-400 font-mono">{fmt(subtotal / 100)}</span>
                </div>
              )}
              {discountPct > 0 && (
                <div className="flex justify-between text-sm">
                  <span className="text-accent-green">
                    Desconto {discountPct}% {discountMode === 'per_item' ? '(por item)' : '(no total)'}
                  </span>
                  <span className="text-accent-green font-mono">−{fmt(discountCents / 100)}</span>
                </div>
              )}

              <div className="flex justify-between items-center">
                <span className="text-gray-400 text-sm">Total</span>
                <span className="text-2xl font-bold text-accent-gold">{fmt(total / 100)}</span>
              </div>

              {/* Troco (só aparece no modo Dinheiro) */}
              {payment === 'Dinheiro' && (
                <div className="border-t border-surface-500 pt-3 space-y-2">
                  <label className="text-xs text-gray-400 flex items-center gap-1">
                    <Banknote className="w-3.5 h-3.5" /> Valor recebido (R$)
                  </label>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    className="input text-sm"
                    placeholder="0,00"
                    value={received}
                    onChange={e => setReceived(e.target.value)}
                  />
                  {received && receivedCents >= 0 && (
                    <div className={clsx(
                      'flex justify-between text-sm font-semibold rounded-lg px-3 py-2',
                      troco >= 0 ? 'bg-accent-green/10 text-accent-green' : 'bg-red-500/10 text-red-400'
                    )}>
                      <span>{troco >= 0 ? 'Troco' : 'Falta'}</span>
                      <span className="font-mono">{fmt(Math.abs(troco) / 100)}</span>
                    </div>
                  )}
                </div>
              )}

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
      )}
    </div>
  )
}

// ── Componente Histórico do Dia ───────────────────────────────────────────────

function HistoricoTab({ history, loading }: { history: VendaAvulsaDto[]; loading: boolean }) {
  const totalDia   = history.reduce((s, v) => s + v.totalInReais, 0)
  const countDia   = history.length

  if (loading) return (
    <div className="flex-1 flex items-center justify-center py-24">
      <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>
  )

  return (
    <div className="flex-1 overflow-y-auto space-y-4">
      {/* Resumo do dia + botão PDF */}
      <div className="flex items-center justify-between gap-3">
        {history.length > 0 && (
          <button
            onClick={() => printDailyReportPDF(history, PAYMENT_METHODS)}
            className="btn-secondary text-sm flex items-center gap-2 ml-auto"
          >
            <FileText className="w-4 h-4" /> Exportar PDF do Dia
          </button>
        )}
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div className="card flex items-center gap-4">
          <div className="w-11 h-11 rounded-xl bg-brand-600/10 flex items-center justify-center">
            <TrendingUp className="w-5 h-5 text-brand-400" />
          </div>
          <div>
            <p className="text-2xl font-bold text-white">{countDia}</p>
            <p className="text-xs text-gray-400">Vendas hoje</p>
          </div>
        </div>
        <div className="card flex items-center gap-4">
          <div className="w-11 h-11 rounded-xl bg-amber-500/10 flex items-center justify-center">
            <Banknote className="w-5 h-5 text-accent-gold" />
          </div>
          <div>
            <p className="text-2xl font-bold text-accent-gold">{fmt(totalDia)}</p>
            <p className="text-xs text-gray-400">Total do dia</p>
          </div>
        </div>
      </div>

      {history.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-gray-500 gap-3">
          <History className="w-10 h-10 text-gray-600" />
          <p className="text-sm">Nenhuma venda registrada hoje</p>
        </div>
      ) : (
        <div className="space-y-2">
          {history.map(v => {
            const payLabel = PAYMENT_METHODS.find(m => m.value === v.paymentMethod)?.label ?? v.paymentMethod
            return (
              <div key={v.id} className="card">
                <div className="flex items-start justify-between mb-2">
                  <div>
                    <p className="text-sm font-medium text-white">
                      {v.clientName ?? <span className="text-gray-500 italic">Cliente Balcão</span>}
                    </p>
                    <div className="flex items-center gap-2 mt-0.5 text-xs text-gray-500">
                      <Clock className="w-3 h-3" />
                      {new Date(v.soldAt).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
                      <span>·</span>
                      {PAYMENT_ICONS[v.paymentMethod]}
                      {payLabel}
                    </div>
                  </div>
                  <div className="text-right">
                    <p className="text-accent-gold font-bold">{fmt(v.totalInReais)}</p>
                    {v.discountPercent > 0 && (
                      <p className="text-xs text-accent-green">−{v.discountPercent}% desc.</p>
                    )}
                  </div>
                </div>
                <div className="text-xs text-gray-500">
                  {v.items.map((it, idx) => (
                    <span key={idx}>{idx > 0 && ', '}{it.quantity}× {it.productName}</span>
                  ))}
                </div>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
