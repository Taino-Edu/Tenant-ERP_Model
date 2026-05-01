'use client'
import { useEffect, useState } from 'react'
import { championshipApi, Championship } from '@/lib/api'
import toast from 'react-hot-toast'
import { Trophy, Plus, Users, CalendarDays, Swords, X, Check, Loader2 } from 'lucide-react'
import clsx from 'clsx'

const STATUS_LABELS: Record<string, string> = {
  Planejado: '📋 Planejado', Inscricoes: '📝 Inscrições',
  EmAndamento: '⚔️ Em Andamento', Finalizado: '🏆 Finalizado', Cancelado: '❌ Cancelado'
}
const STATUS_CLASSES: Record<string, string> = {
  Planejado: 'badge bg-blue-500/10 text-blue-400 border-blue-500/20',
  Inscricoes: 'badge bg-brand-500/10 text-brand-300 border-brand-500/20',
  EmAndamento: 'badge bg-amber-500/10 text-amber-400 border-amber-500/20',
  Finalizado: 'badge bg-emerald-500/10 text-emerald-400 border-emerald-500/20',
  Cancelado: 'badge bg-red-500/10 text-red-400 border-red-500/20',
}
const GAMES = ['Pokemon', 'Magic: The Gathering', 'Yu-Gi-Oh!', 'One Piece TCG', 'Dragon Ball Super']

function NewChampionshipModal({ onClose, onSave }: { onClose: () => void; onSave: (c: Partial<Championship>) => Promise<void> }) {
  const [form, setForm] = useState<Partial<Championship>>({ game: 'Pokemon', entryFeeInCents: 0 })
  const [saving, setSaving] = useState(false)
  const set = (k: keyof Championship, v: unknown) => setForm(f => ({ ...f, [k]: v }))

  async function submit(e: React.FormEvent) {
    e.preventDefault(); setSaving(true)
    try { await onSave(form) } finally { setSaving(false) }
  }

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="card w-full max-w-lg animate-bounce-in">
        <div className="flex items-center justify-between mb-6">
          <h2 className="text-lg font-bold text-white flex items-center gap-2"><Trophy className="w-5 h-5 text-accent-gold" /> Novo Campeonato</h2>
          <button onClick={onClose}><X className="w-5 h-5 text-gray-500 hover:text-gray-300" /></button>
        </div>
        <form onSubmit={submit} className="space-y-4">
          <div>
            <label className="label">Nome do Campeonato *</label>
            <input className="input" required value={form.name ?? ''} onChange={e => set('name', e.target.value)} placeholder="Ex: Torneio Pokémon — Abril 2025" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Jogo *</label>
              <select className="input" value={form.game ?? ''} onChange={e => set('game', e.target.value)}>
                {GAMES.map(g => <option key={g}>{g}</option>)}
              </select>
            </div>
            <div>
              <label className="label">Taxa de inscrição (R$)</label>
              <input className="input" type="number" min="0" step="0.01"
                value={(form.entryFeeInCents ?? 0) / 100}
                onChange={e => set('entryFeeInCents', Math.round(parseFloat(e.target.value) * 100))}
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Data / Hora *</label>
              <input className="input" type="datetime-local" required
                onChange={e => set('startDate', new Date(e.target.value).toISOString())}
              />
            </div>
            <div>
              <label className="label">Máx. participantes</label>
              <input className="input" type="number" min="2" placeholder="Sem limite"
                onChange={e => set('maxParticipants', e.target.value ? parseInt(e.target.value) : null)}
              />
            </div>
          </div>
          <div>
            <label className="label">Descrição</label>
            <textarea className="input resize-none h-20" placeholder="Regras, formato, premiação..." value={form.description ?? ''}
              onChange={e => set('description', e.target.value)} />
          </div>
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
            <button type="submit" disabled={saving} className="btn-primary flex-1 justify-center">
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
              Criar Campeonato
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export default function CampeonatosPage() {
  const [championships, setChampionships] = useState<Championship[]>([])
  const [loading, setLoading] = useState(true)
  const [showModal, setShowModal] = useState(false)

  const fetch = async () => {
    setLoading(true)
    try { const { data } = await championshipApi.list(); setChampionships(data) }
    catch { toast.error('Erro ao carregar campeonatos') }
    finally { setLoading(false) }
  }
  useEffect(() => { fetch() }, [])

  async function handleSave(form: Partial<Championship>) {
    try { await championshipApi.create(form); toast.success('Campeonato criado!'); setShowModal(false); fetch() }
    catch { toast.error('Erro ao criar campeonato') }
  }

  async function updateStatus(id: string, status: string) {
    try { await championshipApi.setStatus(id, status); fetch() }
    catch { toast.error('Erro ao atualizar status') }
  }

  const nextStatuses: Record<string, string[]> = {
    Planejado: ['Inscricoes', 'Cancelado'],
    Inscricoes: ['EmAndamento', 'Cancelado'],
    EmAndamento: ['Finalizado', 'Cancelado'],
    Finalizado: [], Cancelado: [],
  }

  return (
    <div className="p-6 space-y-6">
      {showModal && <NewChampionshipModal onClose={() => setShowModal(false)} onSave={handleSave} />}

      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white">Campeonatos</h1>
          <p className="text-gray-400 text-sm mt-0.5">{championships.length} torneio{championships.length !== 1 ? 's' : ''} registrado{championships.length !== 1 ? 's' : ''}</p>
        </div>
        <button onClick={() => setShowModal(true)} className="btn-primary">
          <Plus className="w-4 h-4" /> Novo Campeonato
        </button>
      </div>

      {loading ? (
        <div className="flex justify-center py-16"><div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" /></div>
      ) : championships.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <Trophy className="w-12 h-12 text-gray-600 mb-3" />
          <p className="text-gray-400 font-medium">Nenhum campeonato ainda</p>
          <button onClick={() => setShowModal(true)} className="btn-primary mt-4"><Plus className="w-4 h-4" /> Criar primeiro campeonato</button>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {championships.map(c => (
            <div key={c.id} className="card space-y-4">
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <div className="flex items-center gap-2 mb-2">
                    <span className={STATUS_CLASSES[c.status] ?? 'badge'}>{STATUS_LABELS[c.status]}</span>
                  </div>
                  <h3 className="font-bold text-white">{c.name}</h3>
                  <p className="text-sm text-gray-400 flex items-center gap-1.5 mt-1">
                    <Swords className="w-3.5 h-3.5" />{c.game}
                  </p>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3 text-sm">
                <div className="bg-surface-800 rounded-lg p-2.5">
                  <p className="text-xs text-gray-500">Data</p>
                  <p className="text-white font-medium">
                    {new Date(c.startDate).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' })}
                  </p>
                </div>
                <div className="bg-surface-800 rounded-lg p-2.5">
                  <p className="text-xs text-gray-500">Inscrição</p>
                  <p className={clsx('font-medium', c.entryFeeInCents === 0 ? 'text-accent-green' : 'text-accent-gold')}>
                    {c.entryFeeInCents === 0 ? 'Grátis' : `R$ ${(c.entryFeeInCents/100).toFixed(2)}`}
                  </p>
                </div>
              </div>

              {c.maxParticipants && (
                <div className="flex items-center gap-2 text-sm text-gray-400">
                  <Users className="w-4 h-4" />
                  <span>Máx. {c.maxParticipants} participantes</span>
                </div>
              )}

              {/* Botões de transição de status */}
              {nextStatuses[c.status]?.length > 0 && (
                <div className="flex gap-2 pt-1">
                  {nextStatuses[c.status].map(next => (
                    <button
                      key={next}
                      onClick={() => updateStatus(c.id, next)}
                      className={clsx('text-xs px-3 py-1.5 rounded-lg font-medium transition-colors',
                        next === 'Cancelado' ? 'bg-red-600/20 text-red-400 hover:bg-red-600/30 border border-red-500/20'
                                            : 'bg-brand-600/20 text-brand-300 hover:bg-brand-600/30 border border-brand-500/20'
                      )}
                    >
                      → {STATUS_LABELS[next]}
                    </button>
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
