'use client'
import { useEffect, useState, useCallback } from 'react'
import { crediarioApi, CrediariosDto } from '@/lib/api'
import toast from 'react-hot-toast'
import {
  CreditCard, CheckCircle, Clock, AlertTriangle,
  Filter, Loader2, User, Calendar,
} from 'lucide-react'
import clsx from 'clsx'

const fmt = (n: number) => `R$ ${n.toFixed(2).replace('.', ',')}`
const fmtDate = (d: string) => new Date(d).toLocaleDateString('pt-BR')

type FilterStatus = 'todos' | 'Aberto' | 'Pago'

export default function CrediarioPage() {
  const [crediarios, setCrediarios] = useState<CrediariosDto[]>([])
  const [filter, setFilter]         = useState<FilterStatus>('Aberto')
  const [loading, setLoading]       = useState(true)
  const [pagando, setPagando]       = useState<string | null>(null)

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

  async function handleMarcarPago(id: string, userName: string, valor: number) {
    if (!confirm(`Confirmar pagamento de ${fmt(valor)} de ${userName}?`)) return
    setPagando(id)
    try {
      await crediarioApi.marcarPago(id)
      toast.success(`Crediário de ${userName} quitado!`)
      fetchCrediarios()
    } catch {
      toast.error('Erro ao registrar pagamento')
    } finally {
      setPagando(null)
    }
  }

  const totais = {
    abertos:  crediarios.filter(c => c.status === 'Aberto').length,
    vencidos: crediarios.filter(c => c.vencido).length,
    pagos:    crediarios.filter(c => c.status === 'Pago').length,
    valorAberto: crediarios
      .filter(c => c.status === 'Aberto' || c.status === 'Vencido')
      .reduce((s, c) => s + c.valorEmReais, 0),
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-white flex items-center gap-2">
          <CreditCard className="w-6 h-6 text-brand-400" /> Crediário
        </h1>
        <p className="text-gray-400 text-sm mt-0.5">
          Clientes com pagamento em aberto — vencimento automático em 30 dias
        </p>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {[
          { label: 'Em Aberto',     value: totais.abertos,               icon: CreditCard,     color: 'text-amber-400',    bg: 'bg-amber-500/10'  },
          { label: 'Vencidos',      value: totais.vencidos,              icon: AlertTriangle,  color: 'text-red-400',      bg: 'bg-red-500/10'    },
          { label: 'Valor em Aberto',value: fmt(totais.valorAberto),     icon: CreditCard,     color: 'text-accent-gold',  bg: 'bg-amber-500/10'  },
          { label: 'Quitados',      value: totais.pagos,                 icon: CheckCircle,    color: 'text-accent-green', bg: 'bg-accent-green/10'},
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
            <div
              key={c.id}
              className={clsx(
                'card flex flex-col sm:flex-row sm:items-center gap-4',
                c.vencido && 'border-red-500/30'
              )}
            >
              {/* Info do cliente */}
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
                        ? `Pago em ${fmtDate(c.dataPagamento!)}`
                        : c.vencido
                          ? `Vencido há ${Math.abs(c.diasRestantes)} dias`
                          : `Vence em ${c.diasRestantes} dias (${fmtDate(c.dataVencimento)})`
                      }
                    </span>
                  </div>
                </div>
              </div>

              {/* Valor + ação */}
              <div className="flex items-center gap-4 sm:flex-col sm:items-end">
                <p className={clsx(
                  'text-xl font-bold',
                  c.status === 'Pago' ? 'text-gray-500' : 'text-accent-gold'
                )}>
                  {fmt(c.valorEmReais)}
                </p>
                {c.status !== 'Pago' && (
                  <button
                    onClick={() => handleMarcarPago(c.id, c.userName, c.valorEmReais)}
                    disabled={pagando === c.id}
                    className="btn-success text-sm py-1.5 px-4 whitespace-nowrap disabled:opacity-50"
                  >
                    {pagando === c.id
                      ? <Loader2 className="w-4 h-4 animate-spin" />
                      : <><CheckCircle className="w-4 h-4" /> Marcar Pago</>
                    }
                  </button>
                )}
              </div>
            </div>
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
