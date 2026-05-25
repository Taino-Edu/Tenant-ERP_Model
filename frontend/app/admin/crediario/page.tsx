'use client'
import { useEffect, useState, useCallback } from 'react'
import {
  crediarioApi, CrediariosDto, PagamentoCrediarioDto, FORMAS_PAGAMENTO_CREDIARIO,
} from '@/lib/api'
import toast from 'react-hot-toast'
import {
  CreditCard, CheckCircle, Clock, AlertTriangle,
  Filter, Loader2, User, Calendar, ChevronDown, ChevronUp,
  Plus, History, DollarSign, X,
} from 'lucide-react'
import clsx from 'clsx'

const fmt     = (n: number) => `R$ ${n.toFixed(2).replace('.', ',')}`
const fmtDate = (d: string) => new Date(d).toLocaleDateString('pt-BR')
const fmtDateHour = (d: string) =>
  new Date(d).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

type FilterStatus = 'todos' | 'Aberto' | 'Pago'

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
            { label: 'Restante',   val: crediario.saldoRestanteEmReais, cls: 'text-amber-400' },
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

function CrediarioCard({
  c,
  onPagamento,
}: {
  c: CrediariosDto
  onPagamento: (c: CrediariosDto) => void
}) {
  const [expandido, setExpandido] = useState(false)

  const progresso = c.valorEmReais > 0
    ? Math.min(100, (c.valorPagoEmReais / c.valorEmReais) * 100)
    : 0

  return (
    <div className={clsx('card', c.vencido && 'border-red-500/30')}>
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
              <p className="font-semibold text-white">{c.userName}</p>
              <StatusBadge status={c.status} vencido={c.vencido} />
            </div>
            {c.userEmail && (
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
                <p className="text-xl font-bold text-amber-400">{fmt(c.saldoRestanteEmReais)}</p>
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
            <button
              onClick={() => onPagamento(c)}
              className="btn-success text-sm py-1.5 px-4 whitespace-nowrap"
            >
              <Plus className="w-4 h-4" /> Registrar Pagamento
            </button>
          )}
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

// ── Página principal ──────────────────────────────────────────────────────────

export default function CrediarioPage() {
  const [crediarios, setCrediarios] = useState<CrediariosDto[]>([])
  const [filter, setFilter]         = useState<FilterStatus>('Aberto')
  const [loading, setLoading]       = useState(true)
  const [modalCrediario, setModalCrediario] = useState<CrediariosDto | null>(null)

  const fetchCrediarios = useCallback(async () => {
    setLoading(true)
    try {
      const { data } = await crediarioApi.list(filter === 'todos' ? undefined : filter)
      setCrediarios(data)
    } catch {
      toast.error('Erro ao carregar crediários')
    } finally {
      setLoading(false)
    }
  }, [filter])

  useEffect(() => { fetchCrediarios() }, [fetchCrediarios])

  const totais = {
    abertos:     crediarios.filter(c => c.status === 'Aberto' || c.status === 'Vencido').length,
    vencidos:    crediarios.filter(c => c.vencido).length,
    pagos:       crediarios.filter(c => c.status === 'Pago').length,
    valorAberto: crediarios
      .filter(c => c.status === 'Aberto' || c.status === 'Vencido')
      .reduce((s, c) => s + c.saldoRestanteEmReais, 0),
  }

  return (
    <div className="p-6 space-y-6">
      {/* Modal de pagamento */}
      {modalCrediario && (
        <PagamentoModal
          crediario={modalCrediario}
          onClose={() => setModalCrediario(null)}
          onSuccess={fetchCrediarios}
        />
      )}

      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-white flex items-center gap-2">
          <CreditCard className="w-6 h-6 text-brand-400" /> Crediário
        </h1>
        <p className="text-gray-400 text-sm mt-0.5">
          Clientes com pagamento em aberto — suporta pagamentos parciais
        </p>
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
      ) : crediarios.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <div className="w-16 h-16 bg-surface-700 rounded-2xl flex items-center justify-center mb-4">
            <CreditCard className="w-8 h-8 text-gray-600" />
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
