'use client'
import { useState, useEffect, useCallback } from 'react'
import { eventosApi, EventoDto, EventoEntradaDto, EventoStatus, SaveEventoRequest, getErrorMessage } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import toast from 'react-hot-toast'
import clsx from 'clsx'
import {
  PartyPopper, Plus, Loader2, X, Calendar, Users, DollarSign, Check, Ban,
  Ticket, ChevronLeft,
} from 'lucide-react'

const STATUS_OPTIONS: EventoStatus[] = ['Planejado', 'EmAndamento', 'Concluido', 'Cancelado']

const STATUS_STYLES: Record<EventoStatus, string> = {
  Planejado:   'bg-brand-500/10 text-brand-300 border-brand-500/30',
  EmAndamento: 'bg-accent-green/10 text-accent-green border-accent-green/30',
  Concluido:   'bg-gray-500/10 text-gray-400 border-gray-500/30',
  Cancelado:   'bg-red-500/10 text-red-400 border-red-500/30',
}

const ENTRADA_PAYMENT_METHODS = [
  { value: 'Dinheiro',      label: 'Dinheiro' },
  { value: 'Pix',           label: 'Pix' },
  { value: 'CartaoCredito', label: 'Cartão de Crédito' },
  { value: 'CartaoDebito',  label: 'Cartão de Débito' },
]

function fmt(cents: number) {
  return (cents / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

function fmtDateTime(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

// ── Modal de criar/editar evento ──────────────────────────────────────────────

function EventoFormModal({ evento, onClose, onSaved }: { evento: EventoDto | null; onClose: () => void; onSaved: () => void }) {
  const [nome, setNome]           = useState(evento?.nome ?? '')
  const [descricao, setDescricao] = useState(evento?.descricao ?? '')
  const [data, setData]           = useState(evento ? evento.dataEvento.slice(0, 16) : '')
  const [preco, setPreco]         = useState(evento ? (evento.precoEntradaInCents / 100).toString() : '')
  const [capacidade, setCapacidade] = useState(evento?.capacidadeMaxima?.toString() ?? '')
  const [status, setStatus]       = useState<EventoStatus>(evento?.status ?? 'Planejado')
  const [saving, setSaving]       = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!nome.trim() || !data) {
      toast.error('Nome e data são obrigatórios.')
      return
    }
    setSaving(true)
    try {
      const body: SaveEventoRequest = {
        nome: nome.trim(),
        descricao: descricao.trim() || undefined,
        dataEvento: new Date(data).toISOString(),
        precoEntradaInCents: Math.round(Number(preco || 0) * 100),
        capacidadeMaxima: capacidade ? Number(capacidade) : null,
      }
      if (evento) await eventosApi.update(evento.id, { ...body, status })
      else await eventosApi.create(body)
      toast.success(evento ? 'Evento atualizado!' : 'Evento criado!')
      onSaved()
      onClose()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao salvar evento'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-md shadow-2xl">
        <div className="flex items-center justify-between px-6 py-4 border-b border-surface-500">
          <h2 className="font-bold text-white text-lg flex items-center gap-2">
            <PartyPopper className="w-5 h-5 text-brand-400" /> {evento ? 'Editar Evento' : 'Novo Evento'}
          </h2>
          <button onClick={onClose} className="text-gray-500 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
          <div>
            <label className="label">Nome *</label>
            <input className="input" placeholder="Torneio de sábado" value={nome} onChange={e => setNome(e.target.value)} required maxLength={150} />
          </div>
          <div>
            <label className="label">Descrição</label>
            <textarea className="input resize-y min-h-[3rem]" placeholder="Detalhes do evento" value={descricao} onChange={e => setDescricao(e.target.value)} maxLength={2000} />
          </div>
          <div>
            <label className="label">Data e hora *</label>
            <input type="datetime-local" className="input" value={data} onChange={e => setData(e.target.value)} required />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Preço da entrada (R$)</label>
              <input type="number" min={0} step="0.01" className="input" placeholder="0,00" value={preco} onChange={e => setPreco(e.target.value)} />
            </div>
            <div>
              <label className="label">Capacidade máxima</label>
              <input type="number" min={1} className="input" placeholder="Sem limite" value={capacidade} onChange={e => setCapacidade(e.target.value)} />
            </div>
          </div>
          {evento && (
            <div>
              <label className="label">Status</label>
              <select className="input" value={status} onChange={e => setStatus(e.target.value as EventoStatus)}>
                {STATUS_OPTIONS.map(s => <option key={s} value={s}>{s}</option>)}
              </select>
            </div>
          )}
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
            <button type="submit" disabled={saving} className="btn-primary flex-1 justify-center">
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />}
              {evento ? 'Salvar' : 'Criar'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── Detalhe de um evento: entradas vendidas ───────────────────────────────────

function EventoDetalhe({ evento, onBack, onChanged }: { evento: EventoDto; onBack: () => void; onChanged: () => void }) {
  const [entradas, setEntradas] = useState<EventoEntradaDto[]>([])
  const [loading, setLoading]   = useState(true)
  const [nomeCliente, setNomeCliente] = useState('')
  const [formaPagamento, setFormaPagamento] = useState('Dinheiro')
  const [valorPago, setValorPago] = useState((evento.precoEntradaInCents / 100).toString())
  const [vendendo, setVendendo] = useState(false)
  const [actingId, setActingId] = useState<string | null>(null)

  const fetchEntradas = useCallback(() => {
    setLoading(true)
    eventosApi.listEntradas(evento.id)
      .then(r => setEntradas(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar entradas')))
      .finally(() => setLoading(false))
  }, [evento.id])

  useEffect(() => { fetchEntradas() }, [fetchEntradas])

  async function venderEntrada(e: React.FormEvent) {
    e.preventDefault()
    if (!nomeCliente.trim()) {
      toast.error('Informe o nome de quem está comprando a entrada.')
      return
    }
    setVendendo(true)
    try {
      await eventosApi.venderEntrada(evento.id, {
        nomeCliente: nomeCliente.trim(),
        formaPagamento,
        valorPagoInCents: Math.round(Number(valorPago || 0) * 100),
      })
      toast.success('Entrada vendida!')
      setNomeCliente('')
      fetchEntradas()
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao vender entrada'))
    } finally {
      setVendendo(false)
    }
  }

  async function checkIn(id: string) {
    setActingId(id)
    try {
      await eventosApi.checkIn(evento.id, id)
      fetchEntradas()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao confirmar check-in'))
    } finally {
      setActingId(null)
    }
  }

  async function cancelarEntrada(id: string) {
    if (!confirm('Cancelar esta entrada?')) return
    setActingId(id)
    try {
      await eventosApi.cancelarEntrada(evento.id, id)
      fetchEntradas()
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao cancelar entrada'))
    } finally {
      setActingId(null)
    }
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center gap-2">
        <button onClick={onBack} className="text-gray-500 hover:text-white"><ChevronLeft className="w-5 h-5" /></button>
        <div>
          <h2 className="text-lg font-bold text-white">{evento.nome}</h2>
          <p className="text-xs text-gray-400">{fmtDateTime(evento.dataEvento)}</p>
        </div>
      </div>

      <div className="card p-5">
        <h3 className="font-bold text-white mb-3 flex items-center gap-2"><Ticket className="w-4 h-4 text-brand-400" /> Vender entrada</h3>
        <form onSubmit={venderEntrada} className="grid grid-cols-1 sm:grid-cols-4 gap-2">
          <input className="input sm:col-span-2" placeholder="Nome do cliente" value={nomeCliente} onChange={e => setNomeCliente(e.target.value)} />
          <select className="input" value={formaPagamento} onChange={e => setFormaPagamento(e.target.value)}>
            {ENTRADA_PAYMENT_METHODS.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}
          </select>
          <input type="number" min={0} step="0.01" className="input" placeholder="Valor" value={valorPago} onChange={e => setValorPago(e.target.value)} />
          <button type="submit" disabled={vendendo} className="btn-primary sm:col-span-4 justify-center">
            {vendendo ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />} Vender entrada
          </button>
        </form>
      </div>

      <div className="card p-5">
        <h3 className="font-bold text-white mb-3">Entradas vendidas ({entradas.filter(e => !e.canceladaEm).length})</h3>
        {loading ? (
          <div className="flex justify-center py-8"><Loader2 className="w-5 h-5 animate-spin text-brand-400" /></div>
        ) : entradas.length === 0 ? (
          <p className="text-gray-400 text-sm text-center py-8">Nenhuma entrada vendida ainda.</p>
        ) : (
          <div className="space-y-2">
            {entradas.map(en => (
              <div key={en.id} className={clsx(
                'flex items-center justify-between gap-3 px-3 py-2.5 rounded-xl border',
                en.canceladaEm ? 'border-surface-600 opacity-50' : 'border-surface-600',
              )}>
                <div className="min-w-0">
                  <p className="text-sm text-white truncate">{en.nomeCliente}</p>
                  <p className="text-xs text-gray-500">{en.formaPagamento} · {fmt(en.valorPagoInCents)} · {fmtDateTime(en.createdAt)}</p>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  {en.canceladaEm ? (
                    <span className="text-xs text-red-400">Cancelada</span>
                  ) : en.checkInEm ? (
                    <span className="text-xs text-accent-green flex items-center gap-1"><Check className="w-3.5 h-3.5" /> Check-in ok</span>
                  ) : (
                    <>
                      <button
                        onClick={() => checkIn(en.id)}
                        disabled={actingId === en.id}
                        className="btn-secondary text-xs py-1 px-2.5"
                      >
                        {actingId === en.id ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Check className="w-3.5 h-3.5" />} Check-in
                      </button>
                      <button
                        onClick={() => cancelarEntrada(en.id)}
                        disabled={actingId === en.id}
                        className="p-1.5 rounded-lg text-red-400 hover:bg-red-500/10"
                        title="Cancelar entrada"
                      >
                        <Ban className="w-3.5 h-3.5" />
                      </button>
                    </>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────

export default function EventosPage() {
  const [eventos, setEventos] = useState<EventoDto[]>([])
  const [loading, setLoading] = useState(true)
  const [showForm, setShowForm] = useState(false)
  const [editingEvento, setEditingEvento] = useState<EventoDto | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const fetchEventos = useCallback(() => {
    setLoading(true)
    eventosApi.list()
      .then(r => setEventos(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar eventos')))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => { fetchEventos() }, [fetchEventos])

  const selected = eventos.find(e => e.id === selectedId)

  if (selected) {
    return <div className="p-4 md:p-6 max-w-3xl mx-auto"><EventoDetalhe evento={selected} onBack={() => setSelectedId(null)} onChanged={fetchEventos} /></div>
  }

  return (
    <div className="p-4 md:p-6 max-w-3xl mx-auto space-y-5">
      <PageHeader
        icon={PartyPopper}
        title="Gestão de Eventos"
        description="Torneios, festas e eventos com cobrança de entrada"
        actions={
          <button onClick={() => { setEditingEvento(null); setShowForm(true) }} className="btn-primary">
            <Plus className="w-4 h-4" /> Novo Evento
          </button>
        }
      />

      {loading ? (
        <div className="flex justify-center py-16"><Loader2 className="w-6 h-6 animate-spin text-brand-400" /></div>
      ) : eventos.length === 0 ? (
        <div className="card p-10 text-center text-gray-400">Nenhum evento cadastrado ainda.</div>
      ) : (
        <div className="space-y-2">
          {eventos.map(ev => (
            <div
              key={ev.id}
              onClick={() => setSelectedId(ev.id)}
              className="w-full card p-4 flex items-center justify-between gap-3 text-left hover:border-brand-500/40 transition-colors cursor-pointer"
            >
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="font-bold text-white">{ev.nome}</span>
                  <span className={clsx('text-xs px-2 py-0.5 rounded-full border', STATUS_STYLES[ev.status])}>{ev.status}</span>
                </div>
                <div className="flex items-center gap-3 text-xs text-gray-400 mt-1 flex-wrap">
                  <span className="flex items-center gap-1"><Calendar className="w-3.5 h-3.5" /> {fmtDateTime(ev.dataEvento)}</span>
                  <span className="flex items-center gap-1"><Users className="w-3.5 h-3.5" /> {ev.entradasVendidas}{ev.capacidadeMaxima ? `/${ev.capacidadeMaxima}` : ''} ({ev.entradasCheckIn} check-in)</span>
                  <span className="flex items-center gap-1"><DollarSign className="w-3.5 h-3.5" /> {fmt(ev.faturamentoInCents)}</span>
                </div>
              </div>
              <button
                onClick={e => { e.stopPropagation(); setEditingEvento(ev); setShowForm(true) }}
                className="btn-secondary text-xs py-1 px-2.5 shrink-0"
              >
                Editar
              </button>
            </div>
          ))}
        </div>
      )}

      {showForm && (
        <EventoFormModal evento={editingEvento} onClose={() => setShowForm(false)} onSaved={fetchEventos} />
      )}
    </div>
  )
}
