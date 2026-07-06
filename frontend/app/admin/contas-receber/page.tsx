'use client'

import { useEffect, useState, useCallback } from 'react'
import { api } from '@/lib/api'
import toast, { Toaster } from 'react-hot-toast'
import clsx from 'clsx'
import {
  Wallet, Plus, Upload, RefreshCw, Loader2,
  ChevronLeft, ChevronRight, CheckCircle, Clock,
  AlertTriangle, TrendingDown, TrendingUp, DollarSign,
  X, Pencil, Trash2, FileText,
} from 'lucide-react'

type Transaction = {
  id: string
  source: string
  type: 'income' | 'expense'
  amount: number
  description: string
  dueDate?: string
  paidAt?: string
  status: string
  category?: string
  supplier?: string
  notes?: string
  createdAt: string
}

type Summary = {
  aPagar:   { total: number; atrasado: number; vence7d: number; qtd: number }
  aReceber: { total: number; qtd: number }
  pagoMes:  number
}

const STATUS_OPTS = [
  { value: '',          label: 'Todas' },
  { value: 'pending',   label: 'Pendente' },
  { value: 'overdue',   label: 'Atrasada' },
  { value: 'paid',      label: 'Paga' },
  { value: 'cancelled', label: 'Cancelada' },
]

const TYPE_OPTS = [
  { value: '',        label: 'Entrada + Saída' },
  { value: 'expense', label: 'A Pagar' },
  { value: 'income',  label: 'A Receber' },
]

const CATEGORIES = ['Fornecedor', 'Aluguel', 'Salário', 'Imposto', 'Marketing', 'Serviço', 'Equipamento', 'Outro']

const statusCls: Record<string, string> = {
  pending:   'bg-blue-500/15 text-blue-400 border-blue-500/30',
  overdue:   'bg-red-500/15 text-red-400 border-red-500/30',
  paid:      'bg-green-500/15 text-green-400 border-green-500/30',
  cancelled: 'bg-gray-500/15 text-gray-400 border-gray-500/30',
}

const statusLabel: Record<string, string> = {
  pending: 'Pendente', overdue: 'Atrasada', paid: 'Paga', cancelled: 'Cancelada',
}

const sourceIcon: Record<string, string> = {
  manual: '✍️', inter: '🏦', mercadopago: '💳', sefaz: '📋', ofx: '📂',
}

function fmtMoney(v: number) {
  return `R$ ${Math.abs(v).toFixed(2).replace('.', ',')}`
}

function fmtDate(d?: string) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

// Vencimento é uma data pura (sem hora significativa, sempre salva como meia-noite UTC) —
// nunca reinterpretar pelo fuso do navegador, senão qualquer fuso atrás de UTC (incluindo
// o Brasil) mostra um dia a menos do que o que está salvo.
function fmtDataPura(d?: string) {
  if (!d) return '—'
  const [ano, mes, dia] = d.slice(0, 10).split('-')
  return `${dia}/${mes}/${ano}`
}

function isOverdue(t: Transaction) {
  if (t.status === 'overdue') return true
  if (t.status !== 'pending' || !t.dueDate) return false
  const hojeUtc = new Date().toISOString().slice(0, 10)
  return t.dueDate.slice(0, 10) < hojeUtc
}

// ── Modal de criação/edição ───────────────────────────────────────────────────
function TransactionModal({ initial, onClose, onSaved }: {
  initial?: Transaction | null
  onClose: () => void
  onSaved: (t: Transaction) => void
}) {
  const [type,        setType]        = useState(initial?.type        ?? 'expense')
  const [amount,      setAmount]      = useState(initial?.amount?.toString() ?? '')
  const [description, setDescription] = useState(initial?.description ?? '')
  const [dueDate,     setDueDate]     = useState(initial?.dueDate?.slice(0,10) ?? '')
  const [category,    setCategory]    = useState(initial?.category    ?? '')
  const [supplier,    setSupplier]    = useState(initial?.supplier    ?? '')
  const [notes,       setNotes]       = useState(initial?.notes       ?? '')
  const [saving,      setSaving]      = useState(false)

  async function submit() {
    if (!amount || !description) { toast.error('Preencha valor e descrição'); return }
    setSaving(true)
    try {
      const payload = {
        type, amount: parseFloat(amount.replace(',', '.')),
        description, category: category || undefined,
        supplier: supplier || undefined, notes: notes || undefined,
        dueDate: dueDate ? new Date(dueDate).toISOString() : undefined,
      }
      const { data } = initial
        ? await api.put(`/api/contas-receber/${initial.id}`, payload)
        : await api.post('/api/contas-receber', payload)
      onSaved(data)
    } catch { toast.error('Erro ao salvar') }
    finally { setSaving(false) }
  }

  return (
    <div className="fixed inset-0 bg-black/70 flex items-center justify-center z-50 p-4">
      <div className="bg-surface-800 rounded-2xl w-full max-w-md p-6 flex flex-col gap-4">
        <div className="flex items-center justify-between">
          <h2 className="font-black text-white">{initial ? 'Editar lançamento' : 'Novo lançamento'}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-white"><X className="w-5 h-5" /></button>
        </div>

        {/* Tipo */}
        <div className="flex gap-2">
          {(['expense', 'income'] as const).map(t => (
            <button key={t} onClick={() => setType(t)}
              className={clsx('flex-1 py-2.5 rounded-xl border text-sm font-semibold transition-colors',
                type === t
                  ? t === 'expense' ? 'bg-red-500/20 text-red-300 border-red-500/40'
                                    : 'bg-green-500/20 text-green-300 border-green-500/40'
                  : 'bg-surface-700 text-gray-400 border-surface-600')}>
              {t === 'expense' ? '💸 A Pagar' : '💰 A Receber'}
            </button>
          ))}
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="col-span-2">
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Descrição *</label>
            <input value={description} onChange={e => setDescription(e.target.value)}
              placeholder="Ex: Aluguel Agosto" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Valor (R$) *</label>
            <input value={amount} onChange={e => setAmount(e.target.value)}
              type="number" min="0" step="0.01" placeholder="0,00" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Vencimento</label>
            <input value={dueDate} onChange={e => setDueDate(e.target.value)}
              type="date" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Fornecedor</label>
            <input value={supplier} onChange={e => setSupplier(e.target.value)}
              placeholder="Nome do fornecedor" className="input w-full" />
          </div>
          <div>
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Categoria</label>
            <select value={category} onChange={e => setCategory(e.target.value)} className="input w-full">
              <option value="">Sem categoria</option>
              {CATEGORIES.map(c => <option key={c}>{c}</option>)}
            </select>
          </div>
          <div className="col-span-2">
            <label className="text-xs text-gray-400 font-semibold mb-1 block">Observações</label>
            <textarea value={notes} onChange={e => setNotes(e.target.value)}
              rows={2} placeholder="Opcional" className="input w-full resize-none" />
          </div>
        </div>

        <div className="flex gap-3">
          <button onClick={onClose} className="flex-1 py-3 rounded-xl bg-surface-700 text-gray-300 text-sm font-semibold">
            Cancelar
          </button>
          <button onClick={submit} disabled={saving}
            className="flex-1 py-3 rounded-xl bg-brand-500 hover:bg-brand-400 disabled:opacity-50
                       text-white text-sm font-bold transition-colors flex items-center justify-center gap-2">
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle className="w-4 h-4" />}
            Salvar
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────
export default function ContasReceberPage() {
  const [items,       setItems]       = useState<Transaction[]>([])
  const [summary,     setSummary]     = useState<Summary | null>(null)
  const [loading,     setLoading]     = useState(true)
  const [typeFilter,  setTypeFilter]  = useState('')
  const [statusFilter,setStatusFilter]= useState('')
  const [page,        setPage]        = useState(1)
  const [totalPages,  setTotalPages]  = useState(1)
  const [totalCount,  setTotalCount]  = useState(0)

  const [createModal, setCreateModal] = useState(false)
  const [editModal,   setEditModal]   = useState<Transaction | null>(null)
  const [ofxLoading,  setOfxLoading]  = useState(false)

  const loadSummary = useCallback(async () => {
    try {
      const { data } = await api.get('/api/contas-receber/summary')
      setSummary(data)
    } catch { /* silencioso */ }
  }, [])

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const { data } = await api.get('/api/contas-receber', {
        params: { type: typeFilter || undefined, status: statusFilter || undefined, page, pageSize: 30 },
      })
      setItems(data.items)
      setTotalPages(data.totalPages)
      setTotalCount(data.total)
    } catch { toast.error('Erro ao carregar lançamentos') }
    finally  { setLoading(false) }
  }, [typeFilter, statusFilter, page])

  useEffect(() => { load(); loadSummary() }, [load, loadSummary])

  async function handleMarkPaid(t: Transaction) {
    try {
      const { data } = await api.put(`/api/contas-receber/${t.id}`, { status: 'paid' })
      setItems(prev => prev.map(i => i.id === t.id ? data : i))
      loadSummary()
      toast.success('Marcado como pago')
    } catch { toast.error('Erro') }
  }

  async function handleDelete(t: Transaction) {
    if (!confirm(`Excluir "${t.description}"?`)) return
    try {
      await api.delete(`/api/contas-receber/${t.id}`)
      setItems(prev => prev.filter(i => i.id !== t.id))
      setTotalCount(c => c - 1)
      loadSummary()
      toast.success('Excluído')
    } catch { toast.error('Erro ao excluir') }
  }

  function handleSaved(t: Transaction) {
    if (editModal) {
      setItems(prev => prev.map(i => i.id === t.id ? t : i))
      setEditModal(null)
    } else {
      setItems(prev => [t, ...prev])
      setTotalCount(c => c + 1)
      setCreateModal(false)
    }
    loadSummary()
    toast.success('Salvo!')
  }

  async function handleOfxUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setOfxLoading(true)
    try {
      const form = new FormData()
      form.append('file', file)
      const { data } = await api.post('/api/contas-receber/import-ofx', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      toast.success(`${data.imported} transações importadas (${data.skipped} duplicadas ignoradas)`)
      load(); loadSummary()
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? 'Erro ao importar OFX')
    } finally {
      setOfxLoading(false)
      e.target.value = ''
    }
  }

  return (
    <div className="p-4 md:p-6 max-w-5xl mx-auto">
      <Toaster />

      {/* Header */}
      <div className="flex items-center gap-3 mb-6 flex-wrap">
        <div className="p-2 rounded-xl bg-brand-500/10">
          <Wallet className="w-5 h-5 text-brand-400" />
        </div>
        <div>
          <h1 className="text-xl font-black text-white">Contas a Receber / Pagar</h1>
          <p className="text-sm text-gray-400">{totalCount} lançamentos</p>
        </div>
        <div className="ml-auto flex items-center gap-2 flex-wrap">
          {/* Upload OFX */}
          <label className={clsx(
            'flex items-center gap-2 px-3 py-2 rounded-xl bg-surface-700 hover:bg-surface-500',
            'border border-surface-600 text-sm text-gray-300 cursor-pointer transition-colors',
            ofxLoading && 'opacity-60 pointer-events-none')}>
            {ofxLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload className="w-4 h-4" />}
            {ofxLoading ? 'Importando…' : 'Importar OFX'}
            <input type="file" accept=".ofx,.OFX" className="hidden" onChange={handleOfxUpload} />
          </label>
          <button onClick={() => setCreateModal(true)}
            className="flex items-center gap-2 px-3 py-2 rounded-xl bg-brand-500 hover:bg-brand-400
                       text-white text-sm font-semibold transition-colors">
            <Plus className="w-4 h-4" /> Novo lançamento
          </button>
        </div>
      </div>

      {/* Cards resumo */}
      {summary && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-6">
          <div className="card p-4 flex flex-col gap-1">
            <div className="flex items-center gap-2 text-xs text-gray-400 font-semibold">
              <TrendingDown className="w-3.5 h-3.5 text-red-400" /> A Pagar
            </div>
            <p className="text-lg font-black text-white">{fmtMoney(summary.aPagar.total)}</p>
            <p className="text-xs text-gray-500">{summary.aPagar.qtd} lançamentos</p>
          </div>
          <div className="card p-4 flex flex-col gap-1 border-red-500/20">
            <div className="flex items-center gap-2 text-xs text-red-400 font-semibold">
              <AlertTriangle className="w-3.5 h-3.5" /> Atrasado
            </div>
            <p className="text-lg font-black text-red-400">{fmtMoney(summary.aPagar.atrasado)}</p>
            <p className="text-xs text-gray-500">Vence em 7d: {fmtMoney(summary.aPagar.vence7d)}</p>
          </div>
          <div className="card p-4 flex flex-col gap-1">
            <div className="flex items-center gap-2 text-xs text-gray-400 font-semibold">
              <TrendingUp className="w-3.5 h-3.5 text-green-400" /> A Receber
            </div>
            <p className="text-lg font-black text-white">{fmtMoney(summary.aReceber.total)}</p>
            <p className="text-xs text-gray-500">{summary.aReceber.qtd} lançamentos</p>
          </div>
          <div className="card p-4 flex flex-col gap-1">
            <div className="flex items-center gap-2 text-xs text-gray-400 font-semibold">
              <DollarSign className="w-3.5 h-3.5 text-brand-400" /> Pago este mês
            </div>
            <p className={clsx('text-lg font-black', summary.pagoMes >= 0 ? 'text-green-400' : 'text-red-400')}>
              {summary.pagoMes >= 0 ? '+' : ''}{fmtMoney(summary.pagoMes)}
            </p>
            <p className="text-xs text-gray-500">saldo (recebido − pago)</p>
          </div>
        </div>
      )}

      {/* Filtros */}
      <div className="flex gap-2 flex-wrap mb-4">
        {TYPE_OPTS.map(o => (
          <button key={o.value} onClick={() => { setTypeFilter(o.value); setPage(1) }}
            className={clsx('px-3 py-1.5 rounded-full text-xs font-semibold border transition-colors',
              typeFilter === o.value
                ? 'bg-brand-500/20 text-brand-300 border-brand-500/40'
                : 'bg-surface-700 text-gray-400 border-surface-600')}>
            {o.label}
          </button>
        ))}
        <div className="w-px bg-surface-600 mx-1" />
        {STATUS_OPTS.map(o => (
          <button key={o.value} onClick={() => { setStatusFilter(o.value); setPage(1) }}
            className={clsx('px-3 py-1.5 rounded-full text-xs font-semibold border transition-colors',
              statusFilter === o.value
                ? 'bg-brand-500/20 text-brand-300 border-brand-500/40'
                : 'bg-surface-700 text-gray-400 border-surface-600')}>
            {o.label}
          </button>
        ))}
        <button onClick={() => { load(); loadSummary() }} className="ml-auto p-1.5 rounded-lg bg-surface-700 text-gray-400">
          <RefreshCw className="w-4 h-4" />
        </button>
      </div>

      {/* Lista */}
      {loading ? (
        <div className="flex justify-center py-16"><Loader2 className="w-8 h-8 animate-spin text-brand-400" /></div>
      ) : items.length === 0 ? (
        <div className="text-center py-16 text-gray-500">
          <FileText className="w-10 h-10 mx-auto mb-3 opacity-30" />
          <p>Nenhum lançamento encontrado</p>
          <p className="text-xs mt-1">Adicione manualmente ou importe um arquivo OFX</p>
        </div>
      ) : (
        <div className="flex flex-col gap-2">
          {items.map(t => {
            const overdue = isOverdue(t)
            return (
              <div key={t.id}
                className={clsx('card flex items-center gap-3 p-3',
                  overdue && 'border-red-500/30 bg-red-500/5')}>

                {/* Tipo icon */}
                <div className={clsx('w-9 h-9 rounded-xl flex items-center justify-center flex-shrink-0',
                  t.type === 'expense' ? 'bg-red-500/10' : 'bg-green-500/10')}>
                  {t.type === 'expense'
                    ? <TrendingDown className="w-4 h-4 text-red-400" />
                    : <TrendingUp   className="w-4 h-4 text-green-400" />}
                </div>

                {/* Info */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <p className="font-semibold text-white text-sm truncate">{t.description}</p>
                    <span className="text-xs">{sourceIcon[t.source] ?? '📄'}</span>
                  </div>
                  <div className="flex items-center gap-2 mt-0.5 flex-wrap">
                    {t.supplier && <span className="text-xs text-gray-400">{t.supplier}</span>}
                    {t.category && <span className="text-xs text-gray-500">· {t.category}</span>}
                    <span className="text-xs text-gray-500">
                      {t.dueDate ? `Vence ${fmtDataPura(t.dueDate)}` : `Criado ${fmtDate(t.createdAt)}`}
                    </span>
                  </div>
                </div>

                {/* Valor */}
                <div className="text-right flex-shrink-0">
                  <p className={clsx('font-black text-base',
                    t.type === 'expense' ? 'text-red-400' : 'text-green-400')}>
                    {t.type === 'expense' ? '−' : '+'}{fmtMoney(t.amount)}
                  </p>
                  <span className={clsx('text-[10px] font-bold px-1.5 py-0.5 rounded-full border',
                    statusCls[overdue ? 'overdue' : t.status] ?? statusCls['pending'])}>
                    {statusLabel[overdue ? 'overdue' : t.status] ?? t.status}
                  </span>
                </div>

                {/* Ações */}
                <div className="flex items-center gap-1 flex-shrink-0">
                  {t.status !== 'paid' && t.status !== 'cancelled' && (
                    <button onClick={() => handleMarkPaid(t)} title="Marcar como pago"
                      className="p-1.5 rounded-lg bg-green-500/10 text-green-400 hover:bg-green-500/20 transition-colors">
                      <CheckCircle className="w-4 h-4" />
                    </button>
                  )}
                  <button onClick={() => setEditModal(t)} title="Editar"
                    className="p-1.5 rounded-lg bg-surface-700 text-gray-400 hover:text-white transition-colors">
                    <Pencil className="w-4 h-4" />
                  </button>
                  <button onClick={() => handleDelete(t)} title="Excluir"
                    className="p-1.5 rounded-lg bg-surface-700 text-gray-400 hover:text-red-400 transition-colors">
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </div>
            )
          })}
        </div>
      )}

      {/* Paginação */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-3 mt-6">
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
            className="p-2 rounded-lg bg-surface-700 disabled:opacity-40">
            <ChevronLeft className="w-4 h-4" />
          </button>
          <span className="text-sm text-gray-400">{page} / {totalPages}</span>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}
            className="p-2 rounded-lg bg-surface-700 disabled:opacity-40">
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Modais */}
      {(createModal || editModal) && (
        <TransactionModal
          initial={editModal}
          onClose={() => { setCreateModal(false); setEditModal(null) }}
          onSaved={handleSaved}
        />
      )}
    </div>
  )
}
