'use client'
import { useEffect, useState, useCallback, useRef } from 'react'
import { championshipApi, userApi, uploadApi, Championship, ChampionshipParticipant } from '@/lib/api'
import toast from 'react-hot-toast'
import Image from 'next/image'
import {
  Trophy, Plus, Users, Swords, X, Check, Loader2,
  ChevronDown, ChevronUp, UserPlus, Trash2, Medal, Search, ImagePlus,
} from 'lucide-react'
import clsx from 'clsx'

// ── Constantes ────────────────────────────────────────────────────────────────
const STATUS_LABELS: Record<string, string> = {
  Planejado: '📋 Planejado', Inscricoes: '📝 Inscrições',
  EmAndamento: '⚔️ Em Andamento', Finalizado: '🏆 Finalizado', Cancelado: '❌ Cancelado',
}
const STATUS_CLASSES: Record<string, string> = {
  Planejado:   'badge bg-blue-500/10 text-blue-400 border-blue-500/20',
  Inscricoes:  'badge bg-brand-500/10 text-brand-300 border-brand-500/20',
  EmAndamento: 'badge bg-amber-500/10 text-amber-400 border-amber-500/20',
  Finalizado:  'badge bg-emerald-500/10 text-emerald-400 border-emerald-500/20',
  Cancelado:   'badge bg-red-500/10 text-red-400 border-red-500/20',
}
const GAMES = ['Pokemon', 'Magic: The Gathering', 'Yu-Gi-Oh!', 'One Piece TCG', 'Dragon Ball Super']
const NEXT_STATUS: Record<string, string[]> = {
  Planejado: ['Inscricoes', 'Cancelado'],
  Inscricoes: ['EmAndamento', 'Cancelado'],
  EmAndamento: ['Finalizado', 'Cancelado'],
  Finalizado: [], Cancelado: [],
}

// ── Modal: Novo Campeonato ────────────────────────────────────────────────────
function NewChampionshipModal({ onClose, onSave }: {
  onClose: () => void
  onSave: (c: Partial<Championship>) => Promise<void>
}) {
  const [form, setForm]       = useState<Partial<Championship>>({ game: 'Pokemon', entryFeeInCents: 0 })
  const [saving, setSaving]   = useState(false)
  const [imgPreview, setImgPreview] = useState<string | null>(null)
  const [uploading, setUploading]   = useState(false)
  const fileRef = useRef<HTMLInputElement>(null)
  const set = (k: keyof Championship, v: unknown) => setForm(f => ({ ...f, [k]: v }))

  async function handleImage(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setUploading(true)
    try {
      const { data } = await uploadApi.image(file)
      set('imageUrl', data.url)
      setImgPreview(data.url)
      toast.success('Imagem carregada!')
    } catch {
      toast.error('Erro ao fazer upload da imagem')
    } finally {
      setUploading(false)
    }
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault(); setSaving(true)
    try { await onSave(form) } finally { setSaving(false) }
  }

  const BASE = process.env.NEXT_PUBLIC_API_URL || ''

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="card w-full max-w-lg animate-bounce-in max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between mb-6">
          <h2 className="text-lg font-bold text-white flex items-center gap-2">
            <Trophy className="w-5 h-5 text-accent-gold" /> Novo Campeonato
          </h2>
          <button onClick={onClose}><X className="w-5 h-5 text-gray-500 hover:text-gray-300" /></button>
        </div>
        <form onSubmit={submit} className="space-y-4">

          {/* Imagem de capa */}
          <div>
            <label className="label">Imagem de capa</label>
            <input ref={fileRef} type="file" accept="image/*" className="hidden" onChange={handleImage} />
            {imgPreview ? (
              <div className="relative w-full h-36 rounded-xl overflow-hidden group">
                <Image src={`${BASE}${imgPreview}`} alt="Capa" fill className="object-cover" />
                <button
                  type="button"
                  onClick={() => { setImgPreview(null); set('imageUrl', null); if (fileRef.current) fileRef.current.value = '' }}
                  className="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity"
                >
                  <X className="w-6 h-6 text-white" />
                </button>
              </div>
            ) : (
              <button
                type="button"
                onClick={() => fileRef.current?.click()}
                disabled={uploading}
                className="w-full h-36 rounded-xl border-2 border-dashed border-surface-500 flex flex-col items-center justify-center gap-2 text-gray-500 hover:border-brand-500 hover:text-brand-400 transition-colors"
              >
                {uploading
                  ? <Loader2 className="w-6 h-6 animate-spin" />
                  : <><ImagePlus className="w-6 h-6" /><span className="text-sm">Clique para adicionar imagem</span></>
                }
              </button>
            )}
          </div>

          <div>
            <label className="label">Nome do Campeonato *</label>
            <input className="input" required value={form.name ?? ''}
              onChange={e => set('name', e.target.value)}
              placeholder="Ex: Torneio Pokémon — Junho 2025" />
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
                onChange={e => set('entryFeeInCents', Math.round(parseFloat(e.target.value) * 100))} />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Data / Hora *</label>
              <input className="input" type="datetime-local" required
                onChange={e => set('startDate', new Date(e.target.value).toISOString())} />
            </div>
            <div>
              <label className="label">Máx. participantes</label>
              <input className="input" type="number" min="2" placeholder="Sem limite"
                onChange={e => set('maxParticipants', e.target.value ? parseInt(e.target.value) : null)} />
            </div>
          </div>
          <div>
            <label className="label">Descrição / Regras</label>
            <textarea className="input resize-none h-20"
              placeholder="Formato, premiação, regras especiais..."
              value={form.description ?? ''} onChange={e => set('description', e.target.value)} />
          </div>
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
            <button type="submit" disabled={saving || uploading} className="btn-primary flex-1 justify-center">
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
              Criar Campeonato
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── Modal: Adicionar Participante ─────────────────────────────────────────────
function AddParticipantModal({ championshipId, onClose, onAdded }: {
  championshipId: string
  onClose: () => void
  onAdded: () => void
}) {
  const [search, setSearch]     = useState('')
  const [results, setResults]   = useState<{ id: string; name: string; cpf?: string }[]>([])
  const [selected, setSelected] = useState<{ id: string; name: string } | null>(null)
  const [deckName, setDeckName] = useState('')
  const [saving, setSaving]     = useState(false)
  const [searching, setSearching] = useState(false)

  async function handleSearch(q: string) {
    setSearch(q)
    setSelected(null)
    if (q.length < 2) { setResults([]); return }
    setSearching(true)
    try {
      const { data } = await userApi.list()
      const filtered = data
        .filter(u => u.name.toLowerCase().includes(q.toLowerCase()) || u.cpf?.includes(q))
        .slice(0, 8)
        .map(u => ({ id: u.id, name: u.name, cpf: u.cpf ?? undefined }))
      setResults(filtered)
    } catch {
      toast.error('Erro ao buscar clientes')
    } finally {
      setSearching(false)
    }
  }

  async function handleAdd() {
    if (!selected) return
    setSaving(true)
    try {
      await championshipApi.adminRegister(championshipId, selected.id, deckName || undefined)
      toast.success(`${selected.name} inscrito com sucesso!`)
      onAdded()
      onClose()
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao inscrever participante')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="card w-full max-w-md animate-bounce-in">
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-lg font-bold text-white flex items-center gap-2">
            <UserPlus className="w-5 h-5 text-brand-400" /> Adicionar Participante
          </h2>
          <button onClick={onClose}><X className="w-5 h-5 text-gray-500 hover:text-gray-300" /></button>
        </div>

        <div className="space-y-4">
          {/* Busca de cliente */}
          <div>
            <label className="label">Buscar cliente</label>
            <input
              className="input"
              placeholder="Nome ou CPF..."
              value={search}
              onChange={e => handleSearch(e.target.value)}
              autoFocus
            />
          </div>

          {/* Resultados */}
          {searching && (
            <div className="flex justify-center py-2">
              <Loader2 className="w-5 h-5 animate-spin text-brand-400" />
            </div>
          )}
          {results.length > 0 && !selected && (
            <div className="border border-surface-500 rounded-xl overflow-hidden divide-y divide-surface-500">
              {results.map(u => (
                <button
                  key={u.id}
                  onClick={() => { setSelected(u); setSearch(u.name); setResults([]) }}
                  className="w-full flex items-center gap-3 px-4 py-2.5 text-sm hover:bg-surface-700 transition-colors text-left"
                >
                  <div className="w-7 h-7 rounded-full bg-brand-500/20 flex items-center justify-center shrink-0">
                    <span className="text-brand-400 text-xs font-bold">{u.name[0]}</span>
                  </div>
                  <div>
                    <p className="text-white font-medium">{u.name}</p>
                    {u.cpf && <p className="text-gray-500 text-xs">{u.cpf}</p>}
                  </div>
                </button>
              ))}
            </div>
          )}

          {/* Cliente selecionado */}
          {selected && (
            <div className="flex items-center gap-3 bg-brand-500/10 border border-brand-500/30 rounded-xl px-4 py-3">
              <div className="w-8 h-8 rounded-full bg-brand-500/30 flex items-center justify-center shrink-0">
                <span className="text-brand-300 text-sm font-bold">{selected.name[0]}</span>
              </div>
              <p className="text-white font-medium flex-1">{selected.name}</p>
              <button onClick={() => { setSelected(null); setSearch('') }} className="text-gray-500 hover:text-gray-300">
                <X className="w-4 h-4" />
              </button>
            </div>
          )}

          {/* Nome do deck (opcional) */}
          <div>
            <label className="label">Nome do Deck <span className="text-gray-600">(opcional)</span></label>
            <input className="input" placeholder="Ex: Charizard ex, Mewtwo..."
              value={deckName} onChange={e => setDeckName(e.target.value)} />
          </div>

          <div className="flex gap-3 pt-1">
            <button onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
            <button
              onClick={handleAdd}
              disabled={!selected || saving}
              className="btn-primary flex-1 justify-center"
            >
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <UserPlus className="w-4 h-4" />}
              Inscrever
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

// ── Card do Campeonato ────────────────────────────────────────────────────────
function ChampionshipCard({
  c, onStatusChange, onParticipantChange, onDelete,
}: {
  c: Championship
  onStatusChange: (id: string, status: string) => void
  onParticipantChange: () => void
  onDelete?: (id: string) => void
}) {
  const [expanded, setExpanded]         = useState(false)
  const [participants, setParticipants] = useState<ChampionshipParticipant[]>([])
  const [loadingP, setLoadingP]         = useState(false)
  const [showAdd, setShowAdd]           = useState(false)

  const loadParticipants = useCallback(async () => {
    setLoadingP(true)
    try {
      const { data } = await championshipApi.participants(c.id)
      setParticipants(data)
    } catch {
      toast.error('Erro ao carregar participantes')
    } finally {
      setLoadingP(false)
    }
  }, [c.id])

  async function toggleExpand() {
    if (!expanded) await loadParticipants()
    setExpanded(v => !v)
  }

  async function handleRemove(p: ChampionshipParticipant) {
    if (!confirm(`Remover ${p.userName} do campeonato?`)) return
    try {
      await championshipApi.removeParticipant(c.id, p.id)
      toast.success(`${p.userName} removido`)
      loadParticipants()
      onParticipantChange()
    } catch {
      toast.error('Erro ao remover participante')
    }
  }

  const canAddParticipants  = c.status === 'Inscricoes' || c.status === 'EmAndamento'
  const canDelete           = c.status === 'Finalizado' || c.status === 'Cancelado'

  return (
    <>
      {showAdd && (
        <AddParticipantModal
          championshipId={c.id}
          onClose={() => setShowAdd(false)}
          onAdded={() => { loadParticipants(); onParticipantChange() }}
        />
      )}

      <div className="card space-y-4 overflow-hidden !p-0">
        {/* Banner de imagem */}
        {c.imageUrl && (
          <div className="relative w-full h-32">
            <Image
              src={`${process.env.NEXT_PUBLIC_API_URL || ''}${c.imageUrl}`}
              alt={c.name}
              fill
              className="object-cover"
            />
            <div className="absolute inset-0 bg-gradient-to-t from-surface-900/80 to-transparent" />
            <span className={clsx('absolute bottom-2 left-3', STATUS_CLASSES[c.status] ?? 'badge')}>
              {STATUS_LABELS[c.status]}
            </span>
          </div>
        )}

        <div className="px-4 pb-4 pt-2 space-y-4">
        {/* Cabeçalho */}
        <div className="flex items-start justify-between">
          <div className="flex-1">
            {!c.imageUrl && (
              <div className="flex items-center gap-2 mb-2">
                <span className={STATUS_CLASSES[c.status] ?? 'badge'}>{STATUS_LABELS[c.status]}</span>
              </div>
            )}
            <h3 className="font-bold text-white">{c.name}</h3>
            <p className="text-sm text-gray-400 flex items-center gap-1.5 mt-1">
              <Swords className="w-3.5 h-3.5" />{c.game}
            </p>
          </div>
        </div>

        {/* Infos */}
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
              {c.entryFeeInCents === 0 ? 'Grátis' : `R$ ${(c.entryFeeInCents / 100).toFixed(2)}`}
            </p>
          </div>
        </div>

        {/* Participantes header */}
        <div className="flex items-center justify-between pt-1 border-t border-surface-500">
          <button
            onClick={toggleExpand}
            className="flex items-center gap-2 text-sm text-gray-400 hover:text-white transition-colors"
          >
            <Users className="w-4 h-4" />
            <span className="font-medium">
              {c.participantCount} participante{c.participantCount !== 1 ? 's' : ''}
              {c.maxParticipants ? ` / ${c.maxParticipants}` : ''}
            </span>
            {expanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
          </button>
          {canAddParticipants && (
            <button
              onClick={() => setShowAdd(true)}
              className="flex items-center gap-1.5 text-xs font-medium text-brand-400 hover:text-brand-300 transition-colors"
            >
              <UserPlus className="w-3.5 h-3.5" /> Adicionar
            </button>
          )}
        </div>

        {/* Lista de participantes */}
        {expanded && (
          <div className="space-y-1.5">
            {loadingP ? (
              <div className="flex justify-center py-4">
                <Loader2 className="w-5 h-5 animate-spin text-brand-400" />
              </div>
            ) : participants.length === 0 ? (
              <p className="text-center text-sm text-gray-600 py-4">Nenhum inscrito ainda</p>
            ) : (
              participants.map(p => (
                <div key={p.id} className="flex items-center gap-3 bg-surface-800 rounded-lg px-3 py-2">
                  <span className="text-xs font-mono text-gray-500 w-5 text-right shrink-0">#{p.playerNumber}</span>
                  <div className="w-7 h-7 rounded-full bg-brand-500/20 flex items-center justify-center shrink-0">
                    <span className="text-brand-400 text-xs font-bold">{p.userName?.[0] ?? '?'}</span>
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-white truncate">{p.userName}</p>
                    {p.deckName && <p className="text-xs text-gray-500 truncate">{p.deckName}</p>}
                  </div>
                  {p.placement && (
                    <span className="text-xs font-bold text-accent-gold flex items-center gap-1">
                      <Medal className="w-3.5 h-3.5" />{p.placement}º
                    </span>
                  )}
                  {(c.status === 'Inscricoes' || c.status === 'EmAndamento') && (
                    <button
                      onClick={() => handleRemove(p)}
                      className="text-gray-600 hover:text-red-400 transition-colors ml-1 shrink-0"
                      title="Remover"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  )}
                </div>
              ))
            )}
          </div>
        )}

        {/* Botões de status + delete */}
        <div className="flex items-center gap-2 pt-1 flex-wrap">
          {NEXT_STATUS[c.status]?.map(next => (
            <button
              key={next}
              onClick={() => onStatusChange(c.id, next)}
              className={clsx(
                'text-xs px-3 py-1.5 rounded-lg font-medium transition-colors',
                next === 'Cancelado'
                  ? 'bg-red-600/20 text-red-400 hover:bg-red-600/30 border border-red-500/20'
                  : 'bg-brand-600/20 text-brand-300 hover:bg-brand-600/30 border border-brand-500/20'
              )}
            >
              → {STATUS_LABELS[next]}
            </button>
          ))}
          {canDelete && onDelete && (
            <button
              onClick={() => onDelete(c.id)}
              className="ml-auto text-xs px-3 py-1.5 rounded-lg font-medium bg-red-600/20 text-red-400 hover:bg-red-600/40 border border-red-500/20 transition-colors flex items-center gap-1.5"
            >
              <Trash2 className="w-3.5 h-3.5" /> Apagar
            </button>
          )}
        </div>
        </div>{/* fim padding */}
      </div>
    </>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────
export default function CampeonatosPage() {
  const [championships, setChampionships] = useState<Championship[]>([])
  const [loading, setLoading]             = useState(true)
  const [showModal, setShowModal]         = useState(false)
  const [search, setSearch]               = useState('')

  const load = useCallback(async (q?: string) => {
    setLoading(true)
    try { const { data } = await championshipApi.listAll(q); setChampionships(data) }
    catch { toast.error('Erro ao carregar campeonatos') }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  // Debounce na busca
  useEffect(() => {
    const t = setTimeout(() => load(search || undefined), 350)
    return () => clearTimeout(t)
  }, [search, load])

  async function handleSave(form: Partial<Championship>) {
    try {
      await championshipApi.create(form)
      toast.success('Campeonato criado!')
      setShowModal(false)
      load(search || undefined)
    } catch {
      toast.error('Erro ao criar campeonato')
    }
  }

  async function handleStatusChange(id: string, status: string) {
    try { await championshipApi.setStatus(id, status); load(search || undefined) }
    catch { toast.error('Erro ao atualizar status') }
  }

  async function handleDelete(id: string) {
    const c = championships.find(x => x.id === id)
    if (!confirm(`Apagar "${c?.name}" permanentemente? Esta ação não pode ser desfeita.`)) return
    try {
      await championshipApi.delete(id)
      toast.success('Campeonato apagado')
      load(search || undefined)
    } catch {
      toast.error('Erro ao apagar campeonato')
    }
  }

  // Agrupa por status
  const ativos     = championships.filter(c => ['Inscricoes', 'EmAndamento'].includes(c.status))
  const planejados = championships.filter(c => c.status === 'Planejado')
  const historico  = championships.filter(c => ['Finalizado', 'Cancelado'].includes(c.status))

  return (
    <div className="p-4 sm:p-6 space-y-4 sm:space-y-6">
      {showModal && <NewChampionshipModal onClose={() => setShowModal(false)} onSave={handleSave} />}

      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-bold text-white">Campeonatos</h1>
          <p className="text-gray-400 text-sm mt-0.5">
            {championships.length} torneio{championships.length !== 1 ? 's' : ''} encontrado{championships.length !== 1 ? 's' : ''}
          </p>
        </div>
        <button onClick={() => setShowModal(true)} className="btn-primary">
          <Plus className="w-4 h-4" /> Novo Campeonato
        </button>
      </div>

      {/* Barra de busca */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
        <input
          className="input pl-9 w-full max-w-sm"
          placeholder="Buscar por nome ou jogo..."
          value={search}
          onChange={e => setSearch(e.target.value)}
        />
      </div>

      {loading ? (
        <div className="flex justify-center py-16">
          <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>
      ) : championships.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <Trophy className="w-12 h-12 text-gray-600 mb-3" />
          <p className="text-gray-400 font-medium">
            {search ? 'Nenhum campeonato encontrado para essa busca' : 'Nenhum campeonato ainda'}
          </p>
          {!search && (
            <button onClick={() => setShowModal(true)} className="btn-primary mt-4">
              <Plus className="w-4 h-4" /> Criar primeiro campeonato
            </button>
          )}
        </div>
      ) : (
        <div className="space-y-8">
          {ativos.length > 0 && (
            <section>
              <h2 className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">⚔️ Ativos</h2>
              <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
                {ativos.map(c => (
                  <ChampionshipCard key={c.id} c={c}
                    onStatusChange={handleStatusChange}
                    onParticipantChange={() => load(search || undefined)}
                  />
                ))}
              </div>
            </section>
          )}

          {planejados.length > 0 && (
            <section>
              <h2 className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">📋 Planejados</h2>
              <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
                {planejados.map(c => (
                  <ChampionshipCard key={c.id} c={c}
                    onStatusChange={handleStatusChange}
                    onParticipantChange={() => load(search || undefined)}
                  />
                ))}
              </div>
            </section>
          )}

          {historico.length > 0 && (
            <section>
              <h2 className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">🏆 Histórico</h2>
              <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
                {historico.map(c => (
                  <ChampionshipCard key={c.id} c={c}
                    onStatusChange={handleStatusChange}
                    onParticipantChange={() => load(search || undefined)}
                    onDelete={handleDelete}
                  />
                ))}
              </div>
            </section>
          )}
        </div>
      )}
    </div>
  )
}
