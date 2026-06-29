'use client'

import { useEffect, useState, useCallback } from 'react'
import { marketplaceApi, CardListingDto, CreateListingRequest } from '@/lib/api'
import { getUserId, getUserName } from '@/lib/auth'
import Link from 'next/link'
import toast, { Toaster } from 'react-hot-toast'
import clsx from 'clsx'
import {
  Search, Plus, Heart, HeartOff, Package, Loader2,
  ArrowLeft, X, ChevronLeft, ChevronRight, User as UserIcon,
  Star, ShoppingBag, Pencil, Trash2, Eye, MessageCircle,
} from 'lucide-react'

const CONDITIONS: { value: string; label: string; color: string }[] = [
  { value: 'NM',  label: 'Near Mint',     color: 'text-green-400' },
  { value: 'LP',  label: 'Light Played',  color: 'text-yellow-400' },
  { value: 'MP',  label: 'Moderate Played', color: 'text-orange-400' },
  { value: 'HP',  label: 'Heavy Played',  color: 'text-red-400' },
  { value: 'DMG', label: 'Damaged',       color: 'text-red-600' },
]
const conditionLabel = Object.fromEntries(CONDITIONS.map(c => [c.value, c.label]))
const conditionColor = Object.fromEntries(CONDITIONS.map(c => [c.value, c.color]))

const GAMES = ['Magic', 'Pokémon', 'Yu-Gi-Oh!', 'One Piece', 'Dragon Ball', 'Digimon', 'Outro']

function fmtPrice(cents: number) {
  return `R$ ${(cents / 100).toFixed(2).replace('.', ',')}`
}

// ── Card de listagem ──────────────────────────────────────────────────────────
function ListingCard({
  listing, myId, onInterest, onEdit, onDelete, onViewInterests,
}: {
  listing: CardListingDto
  myId: string | null
  onInterest: (id: string) => void
  onEdit: (l: CardListingDto) => void
  onDelete: (l: CardListingDto) => void
  onViewInterests: (l: CardListingDto) => void
}) {
  const isMine = listing.sellerId === myId

  return (
    <div className={clsx(
      'card flex flex-col gap-3 relative overflow-hidden',
      listing.status === 'Sold'     && 'opacity-60',
      listing.status === 'Reserved' && 'ring-1 ring-amber-500/40',
    )}>
      {/* status badge */}
      {listing.status !== 'Available' && (
        <div className="absolute top-2 right-2 z-10">
          <span className={clsx(
            'text-[10px] font-bold px-2 py-0.5 rounded-full uppercase tracking-wider',
            listing.status === 'Sold'     && 'bg-red-500/20 text-red-400 border border-red-500/30',
            listing.status === 'Reserved' && 'bg-amber-500/20 text-amber-400 border border-amber-500/30',
          )}>
            {listing.status === 'Sold' ? 'Vendido' : 'Reservado'}
          </span>
        </div>
      )}

      {/* imagem ou placeholder */}
      <div className="rounded-xl overflow-hidden h-36 bg-surface-700 flex items-center justify-center">
        {listing.cardImageUrl ? (
          <img src={listing.cardImageUrl} alt={listing.cardName} className="h-full w-full object-contain" />
        ) : (
          <Package className="w-10 h-10 text-surface-500" />
        )}
      </div>

      {/* info */}
      <div className="flex-1 flex flex-col gap-1 min-w-0">
        <p className="font-bold text-white text-sm leading-tight truncate">{listing.cardName}</p>
        {listing.cardGame && <p className="text-xs text-brand-400">{listing.cardGame}</p>}
        <div className="flex items-center gap-2 mt-0.5">
          <span className={clsx('text-xs font-medium', conditionColor[listing.condition] ?? 'text-gray-400')}>
            {conditionLabel[listing.condition] ?? listing.condition}
          </span>
        </div>
        <p className="text-lg font-black text-brand-300 mt-1">{fmtPrice(listing.priceInCents)}</p>
        {listing.description && (
          <p className="text-xs text-gray-400 line-clamp-2">{listing.description}</p>
        )}
      </div>

      {/* vendedor */}
      <Link
        href={`/perfil/${listing.sellerId}`}
        className="flex items-center gap-2 mt-auto pt-2 border-t border-surface-700 hover:opacity-80 transition-opacity"
      >
        {listing.sellerImageUrl ? (
          <img src={listing.sellerImageUrl} alt={listing.sellerName} className="w-6 h-6 rounded-full object-cover" />
        ) : (
          <div className="w-6 h-6 rounded-full bg-surface-600 flex items-center justify-center">
            <UserIcon className="w-3 h-3 text-gray-400" />
          </div>
        )}
        <span className="text-xs text-gray-400 truncate">{listing.sellerName}</span>
      </Link>

      {/* ações */}
      <div className="flex gap-2">
        {isMine ? (
          <>
            <button
              onClick={() => onViewInterests(listing)}
              className="flex-1 flex items-center justify-center gap-1 py-1.5 rounded-lg bg-surface-700 hover:bg-surface-600 text-xs text-gray-300 transition-colors"
            >
              <Eye className="w-3.5 h-3.5" />
              {listing.interestCount > 0 ? `${listing.interestCount} interesse${listing.interestCount > 1 ? 's' : ''}` : 'Ver interesses'}
            </button>
            <button onClick={() => onEdit(listing)} className="p-1.5 rounded-lg bg-surface-700 hover:bg-brand-500/20 text-gray-400 hover:text-brand-400 transition-colors">
              <Pencil className="w-3.5 h-3.5" />
            </button>
            <button onClick={() => onDelete(listing)} className="p-1.5 rounded-lg bg-surface-700 hover:bg-red-500/20 text-gray-400 hover:text-red-400 transition-colors">
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </>
        ) : (
          <button
            onClick={() => onInterest(listing.id)}
            disabled={listing.status === 'Sold'}
            className={clsx(
              'flex-1 flex items-center justify-center gap-1.5 py-1.5 rounded-lg text-xs font-semibold transition-colors',
              listing.myInterest
                ? 'bg-red-500/20 text-red-400 border border-red-500/30 hover:bg-red-500/30'
                : 'bg-brand-500/20 text-brand-400 border border-brand-500/30 hover:bg-brand-500/30',
              listing.status === 'Sold' && 'opacity-40 cursor-not-allowed',
            )}
          >
            {listing.myInterest ? <HeartOff className="w-3.5 h-3.5" /> : <Heart className="w-3.5 h-3.5" />}
            {listing.myInterest ? 'Remover interesse' : `Tenho interesse${listing.interestCount > 0 ? ` (${listing.interestCount})` : ''}`}
          </button>
        )}
      </div>
    </div>
  )
}

// ── Modal criar / editar ──────────────────────────────────────────────────────
function ListingModal({ initial, onClose, onSave }: {
  initial?: CardListingDto
  onClose: () => void
  onSave: (l: CardListingDto) => void
}) {
  const [form, setForm] = useState<CreateListingRequest & { status?: string }>({
    cardName:    initial?.cardName ?? '',
    cardGame:    initial?.cardGame ?? '',
    cardImageUrl:initial?.cardImageUrl ?? '',
    priceInCents:initial?.priceInCents ?? 0,
    condition:   initial?.condition ?? 'NM',
    description: initial?.description ?? '',
    status:      initial?.status ?? 'Available',
  })
  const [saving, setSaving] = useState(false)

  const set = (k: keyof typeof form, v: string | number) => setForm(p => ({ ...p, [k]: v }))

  async function submit() {
    if (!form.cardName.trim()) { toast.error('Informe o nome da carta'); return }
    if (form.priceInCents <= 0) { toast.error('Informe o preço'); return }
    setSaving(true)
    try {
      const req = {
        cardName:     form.cardName.trim(),
        cardGame:     form.cardGame || undefined,
        cardImageUrl: form.cardImageUrl || undefined,
        priceInCents: form.priceInCents,
        condition:    form.condition,
        description:  form.description || undefined,
        status:       form.status,
      }
      const { data } = initial
        ? await marketplaceApi.update(initial.id, req)
        : await marketplaceApi.create(req)
      onSave(data)
      toast.success(initial ? 'Listagem atualizada!' : 'Carta anunciada!')
    } catch { toast.error('Erro ao salvar') }
    finally  { setSaving(false) }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="bg-surface-800 rounded-2xl shadow-2xl w-full max-w-md flex flex-col gap-5 p-6">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-bold text-white">{initial ? 'Editar anúncio' : 'Anunciar carta'}</h2>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-surface-700 text-gray-400"><X className="w-5 h-5" /></button>
        </div>

        <div className="flex flex-col gap-3">
          <div>
            <label className="label">Nome da carta *</label>
            <input className="input" value={form.cardName} onChange={e => set('cardName', e.target.value)} placeholder="Ex: Black Lotus" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Jogo</label>
              <select className="input" value={form.cardGame ?? ''} onChange={e => set('cardGame', e.target.value)}>
                <option value="">Selecionar...</option>
                {GAMES.map(g => <option key={g} value={g}>{g}</option>)}
              </select>
            </div>
            <div>
              <label className="label">Condição</label>
              <select className="input" value={form.condition} onChange={e => set('condition', e.target.value)}>
                {CONDITIONS.map(c => <option key={c.value} value={c.value}>{c.label}</option>)}
              </select>
            </div>
          </div>
          <div>
            <label className="label">Preço (R$) *</label>
            <input
              className="input"
              type="number"
              min={0}
              step={0.01}
              value={(form.priceInCents / 100).toFixed(2)}
              onChange={e => set('priceInCents', Math.round(parseFloat(e.target.value || '0') * 100))}
            />
          </div>
          <div>
            <label className="label">URL da imagem (opcional)</label>
            <input className="input" value={form.cardImageUrl ?? ''} onChange={e => set('cardImageUrl', e.target.value)} placeholder="https://..." />
          </div>
          <div>
            <label className="label">Descrição (opcional)</label>
            <textarea className="input h-20 resize-none" value={form.description ?? ''} onChange={e => set('description', e.target.value)} placeholder="Estado detalhado, idioma, etc." />
          </div>
          {initial && (
            <div>
              <label className="label">Status</label>
              <select className="input" value={form.status} onChange={e => set('status', e.target.value)}>
                <option value="Available">Disponível</option>
                <option value="Reserved">Reservado</option>
                <option value="Sold">Vendido</option>
              </select>
            </div>
          )}
        </div>

        <div className="flex gap-3 justify-end">
          <button onClick={onClose} className="btn-secondary">Cancelar</button>
          <button onClick={submit} disabled={saving} className="btn-primary flex items-center gap-2">
            {saving && <Loader2 className="w-4 h-4 animate-spin" />}
            {initial ? 'Salvar' : 'Publicar'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Modal de interesses ───────────────────────────────────────────────────────
function InterestsModal({ listing, onClose }: { listing: CardListingDto; onClose: () => void }) {
  const [interests, setInterests] = useState<{ userId: string; userName: string; message: string | null; createdAt: string }[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    marketplaceApi.interests(listing.id)
      .then(r => setInterests(r.data))
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [listing.id])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="bg-surface-800 rounded-2xl shadow-2xl w-full max-w-sm flex flex-col gap-4 p-6 max-h-[80vh]">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-bold text-white">Interessados — {listing.cardName}</h2>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-surface-700 text-gray-400"><X className="w-5 h-5" /></button>
        </div>

        {loading ? (
          <div className="flex items-center justify-center py-8"><Loader2 className="w-6 h-6 animate-spin text-brand-400" /></div>
        ) : interests.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-6">Nenhum interesse ainda.</p>
        ) : (
          <div className="flex flex-col gap-2 overflow-auto">
            {interests.map(i => (
              <Link
                key={i.userId}
                href={`/perfil/${i.userId}`}
                className="flex items-start gap-3 p-3 rounded-xl bg-surface-700 hover:bg-surface-600 transition-colors"
              >
                <div className="w-8 h-8 rounded-full bg-surface-500 flex items-center justify-center shrink-0">
                  <UserIcon className="w-4 h-4 text-gray-400" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-semibold text-white">{i.userName}</p>
                  {i.message && <p className="text-xs text-gray-400 mt-0.5">{i.message}</p>}
                  <p className="text-[10px] text-gray-500 mt-1">{new Date(i.createdAt).toLocaleDateString('pt-BR')}</p>
                </div>
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────
export default function MercadoPage() {
  const myId = getUserId() || null

  const [items,        setItems]        = useState<CardListingDto[]>([])
  const [totalPages,   setTotalPages]   = useState(1)
  const [page,         setPage]         = useState(1)
  const [search,       setSearch]       = useState('')
  const [gameFilter,   setGameFilter]   = useState('')
  const [loading,      setLoading]      = useState(true)
  const [tab,          setTab]          = useState<'all' | 'mine'>('all')
  const [editModal,    setEditModal]    = useState<CardListingDto | null | 'new'>('new' as 'new' | null | CardListingDto)
  const [interestModal,setInterestModal]= useState<CardListingDto | null>(null)
  const [showCreate,   setShowCreate]   = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      if (tab === 'mine') {
        const { data } = await marketplaceApi.mine()
        setItems(data); setTotalPages(1)
      } else {
        const { data } = await marketplaceApi.list({ page, pageSize: 20, game: gameFilter || undefined, search: search || undefined })
        setItems(data.items); setTotalPages(data.totalPages)
      }
    } catch { toast.error('Erro ao carregar') }
    finally  { setLoading(false) }
  }, [tab, page, gameFilter, search])

  useEffect(() => { load() }, [load])

  async function handleInterest(id: string) {
    if (!myId) { toast.error('Faça login para marcar interesse'); return }
    try {
      const { data } = await marketplaceApi.toggleInterest(id)
      setItems(prev => prev.map(i => i.id === id
        ? { ...i, myInterest: data.interested, interestCount: data.interestCount }
        : i
      ))
      toast.success(data.interested ? 'Interesse marcado!' : 'Interesse removido')
    } catch { toast.error('Erro') }
  }

  async function handleDelete(l: CardListingDto) {
    if (!confirm(`Remover anúncio de "${l.cardName}"?`)) return
    try {
      await marketplaceApi.remove(l.id)
      setItems(prev => prev.filter(i => i.id !== l.id))
      toast.success('Anúncio removido')
    } catch { toast.error('Erro ao remover') }
  }

  function handleSaved(saved: CardListingDto) {
    setItems(prev => {
      const idx = prev.findIndex(i => i.id === saved.id)
      if (idx >= 0) { const n = [...prev]; n[idx] = saved; return n }
      return [saved, ...prev]
    })
    setShowCreate(false)
    setEditModal(null)
  }

  return (
    <div className="min-h-screen bg-surface-900 p-4 max-w-5xl mx-auto pb-20">
      <Toaster position="top-right" />

      {/* Header */}
      <div className="flex items-center gap-3 mb-6">
        <Link href="/" className="p-2 rounded-xl bg-surface-800 hover:bg-surface-700 transition-colors text-gray-400">
          <ArrowLeft className="w-5 h-5" />
        </Link>
        <div>
          <h1 className="text-2xl font-black text-white">Mercado</h1>
          <p className="text-sm text-gray-400">Compre e venda cartas com outros jogadores</p>
        </div>
        {myId && (
          <button onClick={() => setShowCreate(true)} className="ml-auto btn-primary flex items-center gap-2">
            <Plus className="w-4 h-4" /> Anunciar carta
          </button>
        )}
      </div>

      {/* Tabs */}
      <div className="flex gap-2 mb-4">
        <button
          onClick={() => { setTab('all'); setPage(1) }}
          className={clsx('px-4 py-2 rounded-xl text-sm font-semibold transition-colors', tab === 'all' ? 'bg-brand-500 text-white' : 'bg-surface-800 text-gray-400 hover:bg-surface-700')}
        >
          <ShoppingBag className="w-4 h-4 inline mr-1.5" />Mercado
        </button>
        {myId && (
          <button
            onClick={() => setTab('mine')}
            className={clsx('px-4 py-2 rounded-xl text-sm font-semibold transition-colors', tab === 'mine' ? 'bg-brand-500 text-white' : 'bg-surface-800 text-gray-400 hover:bg-surface-700')}
          >
            Meus anúncios
          </button>
        )}
      </div>

      {/* Filtros (só no tab all) */}
      {tab === 'all' && (
        <div className="flex gap-2 mb-5">
          <div className="relative flex-1 max-w-xs">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input
              className="input pl-9"
              placeholder="Buscar carta..."
              value={search}
              onChange={e => { setSearch(e.target.value); setPage(1) }}
            />
          </div>
          <select
            className="input max-w-[160px]"
            value={gameFilter}
            onChange={e => { setGameFilter(e.target.value); setPage(1) }}
          >
            <option value="">Todos os jogos</option>
            {GAMES.map(g => <option key={g} value={g}>{g}</option>)}
          </select>
        </div>
      )}

      {/* Grid */}
      {loading ? (
        <div className="flex items-center justify-center py-20">
          <Loader2 className="w-8 h-8 animate-spin text-brand-400" />
        </div>
      ) : items.length === 0 ? (
        <div className="text-center py-20 text-gray-500">
          <Package className="w-12 h-12 mx-auto mb-3 opacity-40" />
          <p className="text-lg font-semibold">{tab === 'mine' ? 'Você não tem anúncios' : 'Nenhuma carta encontrada'}</p>
          {tab === 'mine' && myId && (
            <button onClick={() => setShowCreate(true)} className="mt-4 btn-primary flex items-center gap-2 mx-auto">
              <Plus className="w-4 h-4" /> Anunciar minha primeira carta
            </button>
          )}
        </div>
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
          {items.map(l => (
            <ListingCard
              key={l.id}
              listing={l}
              myId={myId}
              onInterest={handleInterest}
              onEdit={l2 => setEditModal(l2)}
              onDelete={handleDelete}
              onViewInterests={l2 => setInterestModal(l2)}
            />
          ))}
        </div>
      )}

      {/* Paginação */}
      {tab === 'all' && totalPages > 1 && (
        <div className="flex items-center justify-center gap-4 mt-8">
          <button disabled={page <= 1} onClick={() => setPage(p => p - 1)} className="p-2 rounded-xl bg-surface-800 disabled:opacity-40 hover:bg-surface-700 transition-colors">
            <ChevronLeft className="w-5 h-5" />
          </button>
          <span className="text-sm text-gray-400">{page} / {totalPages}</span>
          <button disabled={page >= totalPages} onClick={() => setPage(p => p + 1)} className="p-2 rounded-xl bg-surface-800 disabled:opacity-40 hover:bg-surface-700 transition-colors">
            <ChevronRight className="w-5 h-5" />
          </button>
        </div>
      )}

      {/* Modais */}
      {showCreate && (
        <ListingModal onClose={() => setShowCreate(false)} onSave={handleSaved} />
      )}
      {editModal && editModal !== 'new' && (
        <ListingModal initial={editModal} onClose={() => setEditModal(null)} onSave={handleSaved} />
      )}
      {interestModal && (
        <InterestsModal listing={interestModal} onClose={() => setInterestModal(null)} />
      )}
    </div>
  )
}
