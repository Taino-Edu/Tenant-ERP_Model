'use client'
import { useEffect, useState, useCallback } from 'react'
import {
  crediarioApi, userApi, CrediariosDto, CrediariosClienteDto, PagamentoCrediarioDto,
  FORMAS_PAGAMENTO_CREDIARIO, UserSummary,
} from '@/lib/api'
import toast from 'react-hot-toast'
import {
  CreditCard, CheckCircle, Clock, AlertTriangle,
  Filter, Loader2, User, Calendar, ChevronDown, ChevronUp,
  Plus, History, DollarSign, X, Search, Pencil, Printer, Package, Trash2,
  MessageCircle,
} from 'lucide-react'
import { ItemCrediarioDto } from '@/lib/api'
import clsx from 'clsx'

const fmt     = (n: number) => `R$ ${n.toFixed(2).replace('.', ',')}`
const fmtDate = (d: string) => new Date(d).toLocaleDateString('pt-BR')
const fmtDateHour = (d: string) =>
  new Date(d).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

type FilterStatus = 'todos' | 'Aberto' | 'Pago'

// ── Modal de nova dívida manual ───────────────────────────────────────────────

interface ItemForm { nome: string; qty: string; preco: string }

const hoje = () => new Intl.DateTimeFormat('fr-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date())

function NovaDividaModal({ onClose, onSuccess }: { onClose: () => void; onSuccess: () => void }) {
  const [search, setSearch]         = useState('')
  const [results, setResults]       = useState<UserSummary[]>([])
  const [searching, setSearching]   = useState(false)
  const [selected, setSelected]     = useState<UserSummary | null>(null)
  const [showDrop, setShowDrop]     = useState(false)
  const [valor, setValor]           = useState('')
  const [obs, setObs]               = useState('')
  const [dataAbertura, setDataAbertura] = useState(hoje())
  const [loading, setLoading]       = useState(false)
  const [itens, setItens]           = useState<ItemForm[]>([])
  const [novoItem, setNovoItem]     = useState<ItemForm>({ nome: '', qty: '1', preco: '' })

  useEffect(() => {
    if (search.trim().length < 2) { setResults([]); return }
    const t = setTimeout(async () => {
      setSearching(true)
      try {
        const { data } = await userApi.list(search)
        setResults(data.filter(u => u.role === 'Customer' && u.isActive).slice(0, 6))
        setShowDrop(true)
      } catch { /* ignore */ }
      finally { setSearching(false) }
    }, 300)
    return () => clearTimeout(t)
  }, [search])

  function selectUser(u: UserSummary) {
    setSelected(u)
    setSearch(u.name)
    setShowDrop(false)
  }

  // Calcula total dos itens em R$
  const totalItens = itens.reduce((acc, i) => {
    const q = parseFloat(i.qty) || 0
    const p = parseFloat(i.preco.replace(',', '.')) || 0
    return acc + q * p
  }, 0)

  // Sincroniza campo de valor quando itens mudam
  function addItem() {
    const q = parseFloat(novoItem.qty) || 0
    const p = parseFloat(novoItem.preco.replace(',', '.')) || 0
    if (!novoItem.nome.trim() || q <= 0 || p <= 0) {
      toast.error('Preencha nome, quantidade e preço do item.')
      return
    }
    const novos = [...itens, { ...novoItem }]
    setItens(novos)
    const total = novos.reduce((acc, i) => acc + (parseFloat(i.qty) || 0) * (parseFloat(i.preco.replace(',', '.')) || 0), 0)
    setValor(total.toFixed(2).replace('.', ','))
    setNovoItem({ nome: '', qty: '1', preco: '' })
  }

  function removeItem(idx: number) {
    const novos = itens.filter((_, i) => i !== idx)
    setItens(novos)
    if (novos.length > 0) {
      const total = novos.reduce((acc, i) => acc + (parseFloat(i.qty) || 0) * (parseFloat(i.preco.replace(',', '.')) || 0), 0)
      setValor(total.toFixed(2).replace('.', ','))
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selected) { toast.error('Selecione um cliente'); return }
    const valorNum = parseFloat(valor.replace(',', '.'))
    if (isNaN(valorNum) || valorNum <= 0) { toast.error('Informe um valor válido'); return }

    const itensDto = itens.length > 0 ? itens.map(i => ({
      itemName:        i.nome,
      quantity:        parseInt(i.qty) || 1,
      unitPriceInReais: parseFloat(i.preco.replace(',', '.')) || 0,
      subtotalInReais:  (parseInt(i.qty) || 1) * (parseFloat(i.preco.replace(',', '.')) || 0),
    })) : undefined

    setLoading(true)
    try {
      await crediarioApi.criarManual({
        userId:          selected.id,
        valorEmCentavos: Math.round(valorNum * 100),
        observacao:      obs || undefined,
        dataAbertura:    dataAbertura || undefined,
        itens:           itensDto,
      })
      toast.success(`Crediário de R$ ${valorNum.toFixed(2).replace('.', ',')} criado para ${selected.name}!`)
      onSuccess()
      onClose()
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao criar crediário')
    } finally { setLoading(false) }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-md shadow-2xl">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-surface-500">
          <div>
            <h2 className="font-bold text-white text-lg flex items-center gap-2">
              <CreditCard className="w-5 h-5 text-amber-400" /> Nova Dívida
            </h2>
            <p className="text-sm text-gray-400 mt-0.5">Registrar dívida anterior ao sistema</p>
          </div>
          <button onClick={onClose} className="text-gray-500 hover:text-white">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="px-6 py-5 space-y-4">
          {/* Busca de cliente */}
          <div>
            <label className="label">Cliente</label>
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
              <input
                type="text"
                placeholder="Buscar por nome ou CPF..."
                value={search}
                onChange={e => { setSearch(e.target.value); setSelected(null) }}
                onFocus={() => results.length > 0 && setShowDrop(true)}
                className="input pl-9"
                autoComplete="off"
              />
              {searching && (
                <Loader2 className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 animate-spin text-gray-400" />
              )}
              {showDrop && results.length > 0 && (
                <div className="absolute top-full left-0 right-0 z-10 mt-1 bg-surface-700 border border-surface-500 rounded-xl shadow-xl overflow-hidden">
                  {results.map(u => (
                    <button
                      key={u.id}
                      type="button"
                      onClick={() => selectUser(u)}
                      className="w-full flex items-center gap-3 px-4 py-2.5 hover:bg-surface-600 transition-colors text-left"
                    >
                      <User className="w-4 h-4 text-gray-500 shrink-0" />
                      <div className="min-w-0">
                        <p className="text-sm text-white font-medium truncate">{u.name}</p>
                        {u.cpf && <p className="text-xs text-gray-500">CPF: {u.cpf}</p>}
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </div>
            {selected && (
              <p className="text-xs text-accent-green mt-1.5 flex items-center gap-1">
                <CheckCircle className="w-3 h-3" /> {selected.name} selecionado
              </p>
            )}
          </div>

          {/* Itens da dívida */}
          <div>
            <label className="label flex items-center gap-1">
              <Package className="w-3.5 h-3.5" /> Itens da dívida (opcional)
            </label>

            {/* Lista de itens adicionados */}
            {itens.length > 0 && (
              <div className="bg-surface-900 rounded-xl border border-surface-600 divide-y divide-surface-600 mb-2">
                {itens.map((it, idx) => (
                  <div key={idx} className="flex items-center gap-2 px-3 py-2 text-sm">
                    <span className="flex-1 text-white truncate">{it.nome}</span>
                    <span className="text-gray-400 shrink-0">{it.qty}× R$ {parseFloat(it.preco.replace(',', '.')).toFixed(2).replace('.', ',')}</span>
                    <button type="button" onClick={() => removeItem(idx)} className="text-gray-400 hover:text-red-400 transition-colors shrink-0">
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </div>
                ))}
                <div className="px-3 py-2 flex justify-between text-xs font-semibold text-accent-gold">
                  <span>Total calculado</span>
                  <span>R$ {totalItens.toFixed(2).replace('.', ',')}</span>
                </div>
              </div>
            )}

            {/* Formulário de novo item */}
            <div className="grid grid-cols-[1fr_60px_80px_32px] gap-1.5 items-end">
              <input
                type="text"
                placeholder="Nome do item"
                value={novoItem.nome}
                onChange={e => setNovoItem(p => ({ ...p, nome: e.target.value }))}
                className="input text-sm"
              />
              <input
                type="number"
                min="1"
                placeholder="Qtd"
                value={novoItem.qty}
                onChange={e => setNovoItem(p => ({ ...p, qty: e.target.value }))}
                className="input text-sm text-center"
              />
              <input
                type="text"
                inputMode="decimal"
                placeholder="R$ unit."
                value={novoItem.preco}
                onChange={e => setNovoItem(p => ({ ...p, preco: e.target.value }))}
                className="input text-sm"
              />
              <button
                type="button"
                onClick={addItem}
                className="btn-secondary h-10 px-2 justify-center"
                title="Adicionar item"
              >
                <Plus className="w-4 h-4" />
              </button>
            </div>
          </div>

          {/* Valor */}
          <div>
            <label className="label">Valor total da dívida (R$)</label>
            <input
              type="text"
              inputMode="decimal"
              placeholder="0,00"
              value={valor}
              onChange={e => setValor(e.target.value)}
              className="input"
              required
            />
            {itens.length > 0 && (
              <p className="text-xs text-gray-500 mt-1">Calculado automaticamente pelos itens. Pode ajustar manualmente se necessário.</p>
            )}
          </div>

          {/* Data da dívida */}
          <div>
            <label className="label flex items-center gap-1">
              <Calendar className="w-3.5 h-3.5" /> Data da dívida
            </label>
            <input
              type="date"
              value={dataAbertura}
              onChange={e => setDataAbertura(e.target.value)}
              className="input"
            />
            <p className="text-xs text-gray-400 mt-1">Quando a dívida foi gerada. O vencimento é calculado como data + 30 dias.</p>
          </div>

          {/* Observação */}
          <div>
            <label className="label">Observação (opcional)</label>
            <input
              type="text"
              placeholder='Ex: "Dívida do torneio de abril"'
              value={obs}
              onChange={e => setObs(e.target.value)}
              className="input"
              maxLength={500}
            />
          </div>

          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">
              Cancelar
            </button>
            <button type="submit" disabled={loading || !selected} className="btn-primary flex-1 justify-center">
              {loading
                ? <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</>
                : <><CreditCard className="w-4 h-4" /> Criar Crediário</>
              }
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── Modal de edição do crediário ─────────────────────────────────────────────
interface EditarModalProps {
  crediario: CrediariosDto
  onClose: () => void
  onSuccess: () => void
}

function EditarCrediarioModal({ crediario, onClose, onSuccess }: EditarModalProps) {
  const [valor, setValor]       = useState(crediario.valorEmReais.toFixed(2).replace('.', ','))
  const [obs, setObs]           = useState(crediario.observacao ?? '')
  const [venc, setVenc]         = useState(crediario.dataVencimento.slice(0, 10))
  const [loading, setLoading]   = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const valorNum = parseFloat(valor.replace(',', '.'))
    if (isNaN(valorNum) || valorNum <= 0) { toast.error('Informe um valor válido'); return }

    setLoading(true)
    try {
      await crediarioApi.editar(crediario.id, {
        valorEmCentavos: Math.round(valorNum * 100),
        observacao:      obs || undefined,
        dataVencimento:  venc || undefined,
      })
      toast.success('Crediário atualizado!')
      onSuccess()
      onClose()
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao editar crediário')
    } finally { setLoading(false) }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-md shadow-2xl">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-surface-500">
          <div>
            <h2 className="font-bold text-white text-lg flex items-center gap-2">
              <Pencil className="w-5 h-5 text-brand-400" /> Editar Crediário
            </h2>
            <p className="text-sm text-gray-400 mt-0.5">{crediario.userName}</p>
          </div>
          <button onClick={onClose} className="text-gray-500 hover:text-white">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="px-6 py-5 space-y-4">
          {/* Valor */}
          <div>
            <label className="label">Valor total da dívida (R$)</label>
            <input
              type="text"
              inputMode="decimal"
              placeholder="0,00"
              value={valor}
              onChange={e => setValor(e.target.value)}
              className="input"
              required
            />
            {crediario.valorPagoEmReais > 0 && (
              <p className="text-xs text-amber-400 mt-1">
                Já pago: R$ {crediario.valorPagoEmReais.toFixed(2).replace('.', ',')} — o valor total não pode ser menor que isso.
              </p>
            )}
          </div>

          {/* Vencimento */}
          <div>
            <label className="label">Data de vencimento</label>
            <input
              type="date"
              value={venc}
              onChange={e => setVenc(e.target.value)}
              className="input"
            />
          </div>

          {/* Observação */}
          <div>
            <label className="label">Observação</label>
            <input
              type="text"
              placeholder="Ex: Corrigido — valor real da comanda"
              value={obs}
              onChange={e => setObs(e.target.value)}
              className="input"
              maxLength={500}
            />
          </div>

          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">
              Cancelar
            </button>
            <button type="submit" disabled={loading} className="btn-primary flex-1 justify-center">
              {loading
                ? <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</>
                : <><Pencil className="w-4 h-4" /> Salvar</>
              }
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── Modal de pagamento parcial ─────────────────────────────────────────────────
interface PagamentoModalProps {
  crediario: CrediariosDto
  onClose: () => void
  onSuccess: () => void
}

function PagamentoModal({ crediario, onClose, onSuccess }: PagamentoModalProps) {
  const [valor, setValor]           = useState('')
  const [forma, setForma]           = useState('Dinheiro')
  const [obs, setObs]               = useState('')
  const [loading, setLoading]       = useState(false)

  const saldo = crediario.saldoRestanteEmReais

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const valorNum = parseFloat(valor.replace(',', '.'))
    if (isNaN(valorNum) || valorNum <= 0) {
      toast.error('Informe um valor válido')
      return
    }
    if (valorNum > saldo) {
      toast.error(`Valor maior que o saldo restante (${fmt(saldo)})`)
      return
    }

    const centavos = Math.round(valorNum * 100)
    setLoading(true)
    try {
      await crediarioApi.registrarPagamento(crediario.id, centavos, forma, obs || undefined)
      toast.success(valorNum >= saldo ? 'Crediário quitado!' : `Pagamento de ${fmt(valorNum)} registrado!`)
      onSuccess()
      onClose()
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? 'Erro ao registrar pagamento')
    } finally {
      setLoading(false)
    }
  }

  function preencherTotal() {
    setValor(saldo.toFixed(2).replace('.', ','))
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-md shadow-2xl">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-surface-500">
          <div>
            <h2 className="font-bold text-white text-lg flex items-center gap-2">
              <DollarSign className="w-5 h-5 text-brand-400" /> Registrar Pagamento
            </h2>
            <p className="text-sm text-gray-400 mt-0.5">{crediario.userName}</p>
          </div>
          <button onClick={onClose} className="text-gray-500 hover:text-white">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Saldo info */}
        <div className="px-6 pt-4 pb-2 grid grid-cols-3 gap-3">
          {[
            { label: 'Total',      val: crediario.valorEmReais,         cls: 'text-gray-400' },
            { label: 'Pago',       val: crediario.valorPagoEmReais,     cls: 'text-accent-green' },
            { label: 'Restante',   val: crediario.saldoRestanteEmReais, cls: 'text-gray-200' },
          ].map(({ label, val, cls }) => (
            <div key={label} className="bg-surface-700 rounded-xl px-3 py-2 text-center">
              <p className={clsx('text-base font-bold', cls)}>{fmt(val)}</p>
              <p className="text-[10px] text-gray-500 mt-0.5">{label}</p>
            </div>
          ))}
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
          <div>
            <label className="label">Valor pago</label>
            <div className="flex gap-2">
              <input
                type="text"
                inputMode="decimal"
                placeholder="0,00"
                value={valor}
                onChange={e => setValor(e.target.value)}
                className="input flex-1"
                required
              />
              <button
                type="button"
                onClick={preencherTotal}
                className="btn-secondary text-sm px-3 whitespace-nowrap"
              >
                Total ({fmt(saldo)})
              </button>
            </div>
          </div>

          <div>
            <label className="label">Forma de pagamento</label>
            <select
              value={forma}
              onChange={e => setForma(e.target.value)}
              className="input"
            >
              {FORMAS_PAGAMENTO_CREDIARIO.map(f => (
                <option key={f.value} value={f.value}>{f.label}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="label">Observação (opcional)</label>
            <input
              type="text"
              placeholder="Ex: Pago no balcão"
              value={obs}
              onChange={e => setObs(e.target.value)}
              className="input"
              maxLength={500}
            />
          </div>

          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">
              Cancelar
            </button>
            <button type="submit" disabled={loading} className="btn-success flex-1 justify-center">
              {loading
                ? <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</>
                : <><CheckCircle className="w-4 h-4" /> Confirmar</>
              }
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── Card do crediário ─────────────────────────────────────────────────────────

function imprimirItens(c: CrediariosDto) {
  const w = window.open('', '_blank', 'width=480,height=640')
  if (!w) { alert('Permita pop-ups para imprimir'); return }
  const data = new Date(c.dataAbertura).toLocaleDateString('pt-BR')
  const linhas = c.itensComanda.map(i =>
    `<tr>
      <td>${i.quantity}× ${i.itemName}</td>
      <td style="text-align:right">R$ ${i.unitPriceInReais.toFixed(2).replace('.', ',')}</td>
      <td style="text-align:right">R$ ${i.subtotalInReais.toFixed(2).replace('.', ',')}</td>
    </tr>`
  ).join('')
  w.document.write(`<!DOCTYPE html>
<html lang="pt-BR"><head><meta charset="UTF-8">
<title>Crediário — ${c.userName}</title>
<style>
  @page { size: A5; margin: 16mm; }
  body { font-family: Arial, sans-serif; font-size: 12px; color: #111; }
  h1 { font-size: 16px; margin: 0 0 2px; }
  .sub { font-size: 11px; color: #555; margin: 0 0 12px; }
  table { width: 100%; border-collapse: collapse; margin-top: 8px; }
  th { text-align: left; font-size: 10px; text-transform: uppercase; border-bottom: 1px solid #ccc; padding: 4px 2px; }
  td { padding: 5px 2px; border-bottom: 1px solid #eee; vertical-align: top; }
  .total { font-weight: bold; font-size: 14px; margin-top: 12px; text-align: right; }
  .footer { margin-top: 20px; font-size: 10px; color: #888; border-top: 1px dashed #ccc; padding-top: 8px; }
  @media print { button { display: none; } }
</style>
</head><body>
<h1>Santuário Nerd — Crediário</h1>
<p class="sub">Cliente: <strong>${c.userName}</strong> · Data: ${data}</p>
<table>
  <thead><tr><th>Item</th><th style="text-align:right">Unit.</th><th style="text-align:right">Total</th></tr></thead>
  <tbody>${linhas}</tbody>
</table>
<p class="total">Total: R$ ${c.valorEmReais.toFixed(2).replace('.', ',')}</p>
<div class="footer">Assinatura do cliente: ____________________________</div>
<script>window.onload = () => { window.print(); }</script>
</body></html>`)
  w.document.close()
}

function CrediarioCard({
  c,
  compact = false,
  onPagamento,
  onEditar,
  onDeletar,
}: {
  c: CrediariosDto
  compact?: boolean
  onPagamento: (c: CrediariosDto) => void
  onEditar: (c: CrediariosDto) => void
  onDeletar: (c: CrediariosDto) => void
}) {
  const [expandido, setExpandido]         = useState(false)
  const [expandItens, setExpandItens]     = useState(false)

  const progresso = c.valorEmReais > 0
    ? Math.min(100, (c.valorPagoEmReais / c.valorEmReais) * 100)
    : 0

  return (
    <div className={clsx(
      compact ? 'bg-surface-700 rounded-xl p-4 border border-surface-500' : 'card',
      c.vencido && 'border-red-500/30'
    )}>
      {/* Linha principal */}
      <div className="flex flex-col sm:flex-row sm:items-center gap-4">
        {/* Ícone + info */}
        <div className="flex items-start gap-3 flex-1 min-w-0">
          <div className={clsx(
            'w-10 h-10 rounded-xl flex items-center justify-center shrink-0',
            c.status === 'Pago'   ? 'bg-accent-green/10'
            : c.vencido           ? 'bg-red-500/10'
            :                       'bg-amber-500/10'
          )}>
            {c.status === 'Pago'
              ? <CheckCircle className="w-5 h-5 text-accent-green" />
              : c.vencido
                ? <AlertTriangle className="w-5 h-5 text-red-400" />
                : <Clock className="w-5 h-5 text-amber-400" />
            }
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2 flex-wrap">
              {!compact && <p className="font-semibold text-white">{c.userName}</p>}
              <StatusBadge status={c.status} vencido={c.vencido} />
            </div>
            {!compact && c.userEmail && (
              <p className="text-xs text-gray-500 mt-0.5">{c.userEmail}</p>
            )}
            <div className="flex items-center gap-3 mt-1.5 text-xs text-gray-500 flex-wrap">
              <span className="flex items-center gap-1">
                <Calendar className="w-3 h-3" /> Aberto: {fmtDate(c.dataAbertura)}
              </span>
              <span className={clsx(
                'flex items-center gap-1',
                c.vencido ? 'text-red-400' : c.diasRestantes <= 7 ? 'text-amber-400' : 'text-gray-500'
              )}>
                <Clock className="w-3 h-3" />
                {c.status === 'Pago'
                  ? `Quitado em ${fmtDate(c.dataPagamento!)}`
                  : c.vencido
                    ? `Vencido há ${Math.abs(c.diasRestantes)} dias`
                    : `Vence em ${c.diasRestantes} dias (${fmtDate(c.dataVencimento)})`
                }
              </span>
            </div>
          </div>
        </div>

        {/* Valores + ação */}
        <div className="flex items-center gap-4 sm:flex-col sm:items-end shrink-0">
          <div className="text-right">
            {c.status !== 'Pago' && c.valorPagoEmReais > 0 ? (
              <>
                <p className="text-xs text-gray-500">Restante</p>
                <p className="text-xl font-bold text-gray-200">{fmt(c.saldoRestanteEmReais)}</p>
                <p className="text-xs text-gray-500 mt-0.5">de {fmt(c.valorEmReais)}</p>
              </>
            ) : (
              <p className={clsx(
                'text-xl font-bold',
                c.status === 'Pago' ? 'text-gray-500' : 'text-accent-gold'
              )}>
                {fmt(c.valorEmReais)}
              </p>
            )}
          </div>
          {c.status !== 'Pago' && (
            <div className="flex flex-col gap-2">
              <button
                onClick={() => onPagamento(c)}
                className="btn-success text-sm py-1.5 px-4 whitespace-nowrap"
              >
                <Plus className="w-4 h-4" /> Registrar Pagamento
              </button>
              <button
                onClick={() => onEditar(c)}
                className="btn-secondary text-sm py-1.5 px-4 whitespace-nowrap"
              >
                <Pencil className="w-4 h-4" /> Editar Valor
              </button>
            </div>
          )}
          <button
            onClick={() => onDeletar(c)}
            className="text-gray-400 hover:text-red-400 transition-colors p-1 mt-1"
            title="Excluir crediário"
          >
            <Trash2 className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Barra de progresso */}
      {c.status !== 'Pago' && c.valorPagoEmReais > 0 && (
        <div className="mt-3">
          <div className="h-1.5 bg-surface-600 rounded-full overflow-hidden">
            <div
              className="h-full bg-accent-green rounded-full transition-all duration-500"
              style={{ width: `${progresso}%` }}
            />
          </div>
          <p className="text-[10px] text-gray-500 mt-1">
            {progresso.toFixed(0)}% pago ({fmt(c.valorPagoEmReais)} de {fmt(c.valorEmReais)})
          </p>
        </div>
      )}

      {/* Itens da comanda de origem */}
      {c.itensComanda.length > 0 && (
        <div className="mt-3 border-t border-surface-600 pt-3">
          <div className="flex items-center justify-between mb-2">
            <button
              onClick={() => setExpandItens(v => !v)}
              className="flex items-center gap-1.5 text-xs text-gray-400 hover:text-white transition-colors"
            >
              <Package className="w-3.5 h-3.5" />
              {c.itensComanda.length} produto{c.itensComanda.length !== 1 ? 's' : ''} na comanda
              {expandItens ? <ChevronUp className="w-3 h-3 ml-1" /> : <ChevronDown className="w-3 h-3 ml-1" />}
            </button>
            <button
              onClick={() => imprimirItens(c)}
              className="flex items-center gap-1 text-xs text-brand-400 hover:text-brand-300 transition-colors"
              title="Imprimir lista de produtos"
            >
              <Printer className="w-3.5 h-3.5" /> Imprimir
            </button>
          </div>
          {expandItens && (
            <div className="space-y-1">
              {c.itensComanda.map((item: ItemCrediarioDto, idx: number) => (
                <div key={idx} className="flex items-center justify-between bg-surface-700 rounded-lg px-3 py-2 text-xs">
                  <span className="text-gray-300 flex-1 truncate">
                    {item.quantity}× {item.itemName}
                  </span>
                  <span className="text-accent-gold font-mono ml-2 shrink-0">
                    R$ {item.subtotalInReais.toFixed(2).replace('.', ',')}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Histórico de pagamentos */}
      {c.pagamentos.length > 0 && (
        <div className="mt-3 border-t border-surface-600 pt-3">
          <button
            onClick={() => setExpandido(v => !v)}
            className="flex items-center gap-1.5 text-xs text-gray-400 hover:text-white transition-colors"
          >
            <History className="w-3.5 h-3.5" />
            {c.pagamentos.length} pagamento{c.pagamentos.length > 1 ? 's' : ''} registrado{c.pagamentos.length > 1 ? 's' : ''}
            {expandido ? <ChevronUp className="w-3 h-3 ml-1" /> : <ChevronDown className="w-3 h-3 ml-1" />}
          </button>
          {expandido && (
            <div className="mt-2 space-y-1.5">
              {c.pagamentos.map((p: PagamentoCrediarioDto) => (
                <div key={p.id} className="flex items-center justify-between bg-surface-700 rounded-lg px-3 py-2">
                  <div>
                    <span className="text-xs text-white font-medium">{p.formaPagamento}</span>
                    {p.observacao && (
                      <span className="text-xs text-gray-500 ml-2">— {p.observacao}</span>
                    )}
                    <p className="text-[10px] text-gray-500 mt-0.5">{fmtDateHour(p.createdAt)}</p>
                  </div>
                  <span className="text-sm font-bold text-accent-green">{fmt(p.valorEmReais)}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// ── Card colapsável por cliente ───────────────────────────────────────────────

function ClienteCrediarioCard({
  grupo,
  onPagamento,
  onEditar,
  onDeletar,
}: {
  grupo: CrediariosClienteDto
  onPagamento: (c: CrediariosDto) => void
  onEditar: (c: CrediariosDto) => void
  onDeletar: (c: CrediariosDto) => void
}) {
  const [aberto, setAberto] = useState(grupo.temVencido)

  const whatsUrl = grupo.userWhatsApp
    ? `https://wa.me/55${grupo.userWhatsApp.replace(/\D/g, '')}`
    : null

  return (
    <div className={clsx('card', grupo.temVencido && 'border-red-500/30')}>
      {/* Cabeçalho da pessoa */}
      <button
        onClick={() => setAberto(v => !v)}
        className="w-full flex items-center gap-3 text-left"
      >
        <div className={clsx(
          'w-10 h-10 rounded-xl flex items-center justify-center shrink-0',
          grupo.temVencido ? 'bg-red-500/10' : 'bg-amber-500/10'
        )}>
          {grupo.temVencido
            ? <AlertTriangle className="w-5 h-5 text-red-400" />
            : <User className="w-5 h-5 text-amber-400" />
          }
        </div>

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <p className="font-bold text-white">{grupo.userName}</p>
            {grupo.temVencido && (
              <span className="text-[10px] px-2 py-0.5 rounded-full bg-red-500/15 text-red-400 border border-red-500/20 font-semibold">
                Com vencido
              </span>
            )}
          </div>
          <p className="text-xs text-gray-500 mt-0.5">
            {grupo.totalDividas} {grupo.totalDividas === 1 ? 'dívida' : 'dívidas'} · Próx. venc.{' '}
            {new Date(grupo.proximoVencimento).toLocaleDateString('pt-BR')}
          </p>
        </div>

        <div className="flex items-center gap-3 shrink-0">
          {whatsUrl && (
            <a
              href={whatsUrl}
              target="_blank"
              rel="noopener noreferrer"
              onClick={e => e.stopPropagation()}
              className="p-1.5 text-green-400 hover:text-green-300 hover:bg-green-400/10 rounded-lg transition-colors"
              title="Abrir WhatsApp"
            >
              <MessageCircle className="w-4 h-4" />
            </a>
          )}
          <div className="text-right">
            <p className="text-lg font-bold text-accent-gold">
              {fmt(grupo.saldoTotal)}
            </p>
            <p className="text-[10px] text-gray-500">total em aberto</p>
          </div>
          {aberto
            ? <ChevronUp className="w-4 h-4 text-gray-500" />
            : <ChevronDown className="w-4 h-4 text-gray-500" />
          }
        </div>
      </button>

      {/* Dívidas individuais */}
      {aberto && (
        <div className="mt-4 space-y-3 border-t border-surface-600 pt-4">
          {grupo.dividas.map(c => (
            <CrediarioCard
              key={c.id}
              c={c}
              compact
              onPagamento={onPagamento}
              onEditar={onEditar}
              onDeletar={onDeletar}
            />
          ))}
        </div>
      )}
    </div>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────

export default function CrediarioPage() {
  const [crediarios, setCrediarios]   = useState<CrediariosDto[]>([])
  const [clienteGroups, setClienteGroups] = useState<CrediariosClienteDto[]>([])
  const [filter, setFilter]           = useState<FilterStatus>('Aberto')
  const [loading, setLoading]         = useState(true)
  const [modalCrediario, setModalCrediario]   = useState<CrediariosDto | null>(null)
  const [editarCrediario, setEditarCrediario] = useState<CrediariosDto | null>(null)
  const [showNovaDivida, setShowNovaDivida] = useState(false)

  async function handleDeletar(crediario: CrediariosDto) {
    if (!window.confirm(`Excluir o crediário de ${crediario.userName}?\nEsta ação não pode ser desfeita.`)) return
    try {
      await crediarioApi.deletar(crediario.id)
      toast.success('Crediário excluído!')
      fetchCrediarios()
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao excluir crediário')
    }
  }

  const fetchCrediarios = useCallback(async () => {
    setLoading(true)
    try {
      if (filter === 'Aberto') {
        const { data } = await crediarioApi.porCliente()
        setClienteGroups(data)
        setCrediarios([])
      } else {
        const { data } = await crediarioApi.list(filter === 'todos' ? undefined : filter)
        setCrediarios(data)
        setClienteGroups([])
      }
    } catch {
      toast.error('Erro ao carregar crediários')
    } finally {
      setLoading(false)
    }
  }, [filter])

  useEffect(() => { fetchCrediarios() }, [fetchCrediarios])

  // KPIs: para 'Aberto' usa grupos, para outros usa lista flat
  const totaisAbertos = clienteGroups.reduce((acc, g) => {
    acc.saldo     += g.saldoTotal
    acc.dividas   += g.totalDividas
    acc.vencidos  += g.dividas.filter(d => d.vencido).length
    return acc
  }, { saldo: 0, dividas: 0, vencidos: 0 })

  const totais = filter === 'Aberto' ? {
    abertos:     totaisAbertos.dividas,
    vencidos:    totaisAbertos.vencidos,
    pagos:       0,
    valorAberto: totaisAbertos.saldo,
  } : {
    abertos:     crediarios.filter(c => c.status === 'Aberto').length,
    vencidos:    crediarios.filter(c => c.vencido).length,
    pagos:       crediarios.filter(c => c.status === 'Pago').length,
    valorAberto: crediarios
      .filter(c => c.status === 'Aberto')
      .reduce((s, c) => s + c.saldoRestanteEmReais, 0),
  }

  return (
    <div className="p-4 sm:p-6 space-y-4 sm:space-y-6">
      {/* Modais */}
      {showNovaDivida && (
        <NovaDividaModal
          onClose={() => setShowNovaDivida(false)}
          onSuccess={fetchCrediarios}
        />
      )}
      {modalCrediario && (
        <PagamentoModal
          crediario={modalCrediario}
          onClose={() => setModalCrediario(null)}
          onSuccess={fetchCrediarios}
        />
      )}
      {editarCrediario && (
        <EditarCrediarioModal
          crediario={editarCrediario}
          onClose={() => setEditarCrediario(null)}
          onSuccess={fetchCrediarios}
        />
      )}

      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <CreditCard className="w-6 h-6 text-brand-400" /> Crediário
          </h1>
          <p className="text-gray-400 text-sm mt-0.5">
            Clientes com pagamento em aberto — suporta pagamentos parciais
          </p>
        </div>
        <button
          onClick={() => setShowNovaDivida(true)}
          className="btn-primary shrink-0"
        >
          <Plus className="w-4 h-4" /> Nova Dívida
        </button>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {[
          { label: 'Em Aberto',      value: totais.abertos,              icon: CreditCard,     color: 'text-amber-400',    bg: 'bg-amber-500/10'  },
          { label: 'Vencidos',       value: totais.vencidos,             icon: AlertTriangle,  color: 'text-red-400',      bg: 'bg-red-500/10'    },
          { label: 'Saldo Restante', value: fmt(totais.valorAberto),     icon: DollarSign,     color: 'text-accent-gold',  bg: 'bg-amber-500/10'  },
          { label: 'Quitados',       value: totais.pagos,                icon: CheckCircle,    color: 'text-accent-green', bg: 'bg-accent-green/10'},
        ].map(m => (
          <div key={m.label} className="card flex items-center gap-3 py-3">
            <div className={clsx('w-10 h-10 rounded-xl flex items-center justify-center shrink-0', m.bg)}>
              <m.icon className={clsx('w-5 h-5', m.color)} />
            </div>
            <div className="min-w-0">
              <p className="text-lg font-bold text-white truncate">{m.value}</p>
              <p className="text-xs text-gray-400">{m.label}</p>
            </div>
          </div>
        ))}
      </div>

      {/* Filtro */}
      <div className="flex items-center gap-2">
        <Filter className="w-4 h-4 text-gray-500" />
        <div className="flex gap-1 bg-surface-800 p-1 rounded-lg">
          {(['Aberto', 'Pago', 'todos'] as FilterStatus[]).map(f => (
            <button
              key={f}
              onClick={() => setFilter(f)}
              className={clsx(
                'px-4 py-1.5 rounded-md text-sm font-medium transition-all capitalize',
                filter === f
                  ? 'bg-brand-600 text-white'
                  : 'text-gray-400 hover:text-gray-200'
              )}
            >
              {f === 'todos' ? 'Todos' : f === 'Aberto' ? 'Em Aberto' : 'Quitados'}
            </button>
          ))}
        </div>
      </div>

      {/* Lista */}
      {loading ? (
        <div className="flex items-center justify-center py-24">
          <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>
      ) : filter === 'Aberto' ? (
        clienteGroups.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-24 text-center">
            <div className="w-16 h-16 bg-surface-700 rounded-2xl flex items-center justify-center mb-4">
              <CreditCard className="w-8 h-8 text-gray-400" />
            </div>
            <p className="text-gray-400 font-medium">Nenhuma dívida em aberto</p>
          </div>
        ) : (
          <div className="space-y-3">
            {clienteGroups.map(g => (
              <ClienteCrediarioCard
                key={g.userId}
                grupo={g}
                onPagamento={setModalCrediario}
                onEditar={setEditarCrediario}
                onDeletar={handleDeletar}
              />
            ))}
          </div>
        )
      ) : crediarios.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <div className="w-16 h-16 bg-surface-700 rounded-2xl flex items-center justify-center mb-4">
            <CreditCard className="w-8 h-8 text-gray-400" />
          </div>
          <p className="text-gray-400 font-medium">Nenhum crediário encontrado</p>
        </div>
      ) : (
        <div className="space-y-3">
          {crediarios.map(c => (
            <CrediarioCard
              key={c.id}
              c={c}
              onPagamento={setModalCrediario}
              onEditar={setEditarCrediario}
              onDeletar={handleDeletar}
            />
          ))}
        </div>
      )}
    </div>
  )
}

function StatusBadge({ status, vencido }: { status: string; vencido: boolean }) {
  if (status === 'Pago')
    return <span className="text-xs px-2 py-0.5 rounded-full bg-accent-green/10 text-accent-green border border-accent-green/20">Quitado</span>
  if (vencido)
    return <span className="text-xs px-2 py-0.5 rounded-full bg-red-500/10 text-red-400 border border-red-500/20">Vencido</span>
  return <span className="text-xs px-2 py-0.5 rounded-full bg-amber-500/10 text-amber-400 border border-amber-500/20">Em Aberto</span>
}
