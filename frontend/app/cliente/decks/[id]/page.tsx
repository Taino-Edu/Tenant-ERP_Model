'use client'
import { useEffect, useState, useCallback, useRef } from 'react'
import { useParams, useRouter } from 'next/navigation'
import { deckApi, tcgApi, CardCache, DeckCard } from '@/lib/api'
import {
  Search, Plus, Minus, Trash2, Save, Loader2,
  ArrowLeft, Globe, Lock, ChevronDown, ChevronUp, Zap,
  Upload, X,
} from 'lucide-react'
import toast, { Toaster } from 'react-hot-toast'

const C = { navy: '#0C3D5A', blue: '#3EC2F2', blue2: '#1A9DD4', yellow: '#FFE45E', bg: '#EBF7FD', white: '#FFFFFF', muted: '#4D8FAC', border: 'rgba(62,194,242,0.18)' }

const GAMES = ['Pokemon', 'MTG', 'Yu-Gi-Oh!', 'LoL Riftbound']
const FORMATS: Record<string, string[]> = {
  Pokemon: ['Standard', 'Expanded', 'Unlimited', 'Pocket'],
  MTG: ['Standard', 'Modern', 'Legacy', 'Commander', 'Draft'],
  'Yu-Gi-Oh!': ['Advanced', 'Traditional'],
  'LoL Riftbound': ['Standard'],
}
const MAX_CARDS: Record<string, number> = { Pokemon: 60, MTG: 60, 'Yu-Gi-Oh!': 60, 'LoL Riftbound': 50 }
const MAX_COPIES: Record<string, number> = { Pokemon: 4, MTG: 4, 'Yu-Gi-Oh!': 3, 'LoL Riftbound': 3 }

function cardKey(c: CardCache) { return c.tcgCardId }
function mainPrice(c: CardCache) {
  const p = c.allPrices?.holofoil ?? c.allPrices?.normal ?? c.allPrices?.reverseHolofoil ?? c.marketPrices
  return p?.market ?? p?.mid ?? null
}

// ── Componente de carta no resultado da busca ─────────────────────────────────
function SearchCardRow({ card, onAdd, qty }: { card: CardCache; onAdd: (c: CardCache) => void; qty: number }) {
  const price = mainPrice(card)
  return (
    <div className="flex items-center gap-3 px-4 py-2.5 hover:bg-blue-50 transition-colors cursor-pointer"
      onClick={() => onAdd(card)}>
      {card.imageUrlSmall
        ? <img src={card.imageUrlSmall} alt={card.name} className="w-8 h-11 object-contain rounded shrink-0" />
        : <div className="w-8 h-11 rounded bg-gray-100 shrink-0" />}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-bold truncate" style={{ color: C.navy }}>{card.name}</p>
        <p className="text-[11px]" style={{ color: C.muted }}>
          {card.setName} {card.number ? `#${card.number}` : ''} · {card.rarity ?? '—'}
          {card.hp ? ` · HP ${card.hp}` : ''}
        </p>
      </div>
      <div className="text-right shrink-0">
        {price && <p className="text-[11px] font-bold" style={{ color: C.blue2 }}>${price.toFixed(2)}</p>}
        {qty > 0 && <p className="text-[10px] font-black" style={{ color: C.blue }}>+{qty} no deck</p>}
        <Plus className="w-4 h-4 mt-0.5 ml-auto" style={{ color: C.blue }} />
      </div>
    </div>
  )
}

// ── Linha de carta no deck ────────────────────────────────────────────────────
function DeckCardRow({
  card, qty, onInc, onDec, onRemove, maxCopies,
}: { card: DeckCard; qty: number; onInc: () => void; onDec: () => void; onRemove: () => void; maxCopies: number }) {
  return (
    <div className="flex items-center gap-2 px-4 py-2">
      {card.imageSmall
        ? <img src={card.imageSmall} alt={card.name} className="w-6 h-8 object-contain rounded shrink-0" />
        : <div className="w-6 h-8 rounded bg-gray-100 shrink-0" />}
      <div className="flex-1 min-w-0">
        <p className="text-xs font-bold truncate" style={{ color: C.navy }}>{card.name}</p>
        <p className="text-[10px]" style={{ color: C.muted }}>{card.setCode} {card.number}</p>
      </div>
      <div className="flex items-center gap-1 shrink-0">
        <button onClick={onDec}
          className="w-6 h-6 rounded-lg flex items-center justify-center text-sm font-bold transition-colors"
          style={{ backgroundColor: '#FEE2E2', color: '#DC2626' }}>
          <Minus className="w-3 h-3" />
        </button>
        <span className="w-6 text-center font-black text-sm" style={{ color: C.navy }}>{qty}</span>
        <button onClick={onInc} disabled={qty >= maxCopies}
          className="w-6 h-6 rounded-lg flex items-center justify-center transition-colors disabled:opacity-40"
          style={{ backgroundColor: qty >= maxCopies ? '#e5e7eb' : `${C.blue}20`, color: C.blue2 }}>
          <Plus className="w-3 h-3" />
        </button>
        <button onClick={onRemove} className="w-6 h-6 rounded-lg flex items-center justify-center ml-1"
          style={{ color: '#9CA3AF' }}>
          <Trash2 className="w-3 h-3" />
        </button>
      </div>
    </div>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────
export default function DeckBuilderPage() {
  const { id } = useParams<{ id: string }>()
  const isNew   = id === 'novo'
  const router  = useRouter()

  const [deckName, setDeckName] = useState('')
  const [game,     setGame]     = useState('Pokemon')
  const [format,   setFormat]   = useState('Standard')
  const [isPublic, setIsPublic] = useState(false)
  const [cards,    setCards]    = useState<DeckCard[]>([])

  const [query,       setQuery]       = useState('')
  const [searchRes,   setSearchRes]   = useState<CardCache[]>([])
  const [searching,   setSearching]   = useState(false)
  const [saving,      setSaving]      = useState(false)
  const [loading,     setLoading]     = useState(!isNew)
  const [showSearch,  setShowSearch]  = useState(true)
  const [showImport,  setShowImport]  = useState(false)
  const [importText,  setImportText]  = useState('')
  const [importing,   setImporting]   = useState(false)
  const [brlRate,     setBrlRate]     = useState<number | null>(null)
  const searchTimeout = useRef<ReturnType<typeof setTimeout>>()

  // Carrega deck existente
  useEffect(() => {
    if (isNew) return
    deckApi.get(id).then(r => {
      const d = r.data
      setDeckName(d.name)
      setGame(d.game)
      setFormat(d.format)
      setIsPublic(d.isPublic)
      try { setCards(JSON.parse(d.cardsJson)) } catch { setCards([]) }
    }).catch(() => toast.error('Deck não encontrado.')).finally(() => setLoading(false))
  }, [id, isNew])

  // Busca cotação BRL
  useEffect(() => {
    tcgApi.brlRate().then(r => setBrlRate(r.data.usdToBrl)).catch(() => {})
  }, [])

  // Busca de cartas com debounce
  const search = useCallback(async (q: string) => {
    if (q.trim().length < 2) { setSearchRes([]); return }
    setSearching(true)
    try {
      // Detecta código: "PAL 058", "SV8PT5-1", etc.
      const codeMatch = q.trim().match(/^([A-Za-z0-9]+)\s+(\d+)$/)
      let res
      if (codeMatch) {
        res = await tcgApi.searchByCode(codeMatch[1], codeMatch[2], game)
      } else {
        res = await tcgApi.search(q, game, 1, 12)
      }
      setSearchRes(res.data.items ?? [])
    } catch {
      setSearchRes([])
    } finally { setSearching(false) }
  }, [game])

  useEffect(() => {
    clearTimeout(searchTimeout.current)
    searchTimeout.current = setTimeout(() => search(query), 400)
    return () => clearTimeout(searchTimeout.current)
  }, [query, search])

  // ── Manipulação do deck ────────────────────────────────────────────────────

  const maxCopies = MAX_COPIES[game] ?? 4
  const maxCards  = MAX_CARDS[game] ?? 60
  const totalCards = cards.reduce((s, c) => s + c.quantity, 0)

  function addCard(cache: CardCache) {
    const existing = cards.find(c => c.id === cardKey(cache))
    if (existing) {
      if (existing.quantity >= maxCopies) {
        toast(`Máximo de ${maxCopies} cópias por carta!`, { icon: '⚠️' })
        return
      }
      setCards(prev => prev.map(c => c.id === cardKey(cache) ? { ...c, quantity: c.quantity + 1 } : c))
    } else {
      if (totalCards >= maxCards) {
        toast(`Deck já tem ${maxCards} cartas!`, { icon: '⚠️' })
        return
      }
      setCards(prev => [...prev, {
        id: cardKey(cache), name: cache.name, quantity: 1,
        setCode: cache.setCode ?? undefined, setName: cache.setName ?? undefined,
        number: cache.number ?? undefined, imageSmall: cache.imageUrlSmall ?? undefined,
        type: cache.type ?? undefined, hp: cache.hp ?? undefined,
      }])
    }
    toast.success(`${cache.name} adicionado!`, { duration: 1500 })
  }

  function incCard(id: string) {
    setCards(prev => prev.map(c => c.id === id && c.quantity < maxCopies ? { ...c, quantity: c.quantity + 1 } : c))
  }
  function decCard(id: string) {
    setCards(prev => prev.map(c => c.id === id ? { ...c, quantity: Math.max(1, c.quantity - 1) } : c))
  }
  function removeCard(id: string) {
    setCards(prev => prev.filter(c => c.id !== id))
  }

  // ── Salvar ─────────────────────────────────────────────────────────────────

  async function save() {
    if (!deckName.trim()) { toast.error('Dê um nome ao deck!'); return }
    setSaving(true)
    try {
      const payload = { name: deckName.trim(), game, format, cardsJson: JSON.stringify(cards), isPublic }
      if (isNew) {
        const r = await deckApi.create(payload)
        toast.success('Deck criado!')
        router.replace(`/cliente/decks/${r.data.id}`)
      } else {
        await deckApi.update(id, payload)
        toast.success('Deck salvo!')
      }
    } catch {
      toast.error('Erro ao salvar.')
    } finally { setSaving(false) }
  }

  // ── Importar lista de texto (formato limitlesstcg / PTCG Live) ───────────

  async function importDeckList() {
    const lines = importText.split('\n').map(l => l.trim()).filter(Boolean)
    // Linhas que começam com número = carta: "4 Pikachu PAL 058"
    const cardLines = lines.filter(l => /^\d+\s+/.test(l))
    if (cardLines.length === 0) { toast.error('Nenhuma carta encontrada no texto.'); return }

    setImporting(true)
    let added = 0, failed: string[] = []

    // Substitui deck atual ou acumula — aqui vamos SUBSTITUIR
    const newCards: DeckCard[] = []

    for (const line of cardLines) {
      // Formato: "4 Nome da Carta SET 123"  (set = letras+números maiúsculos, numero = só dígitos no final)
      const match = line.match(/^(\d+)\s+(.+?)\s+([A-Z0-9]{2,8})\s+(\d+)\s*$/)
      if (!match) { failed.push(line); continue }
      const [, qtyStr, name, setCode, number] = match
      const qty = Math.min(parseInt(qtyStr), maxCopies)

      try {
        const res = await tcgApi.searchByCode(setCode, number, game)
        const card = res.data.items?.[0]
        if (card) {
          const existing = newCards.find(c => c.id === cardKey(card))
          if (existing) { existing.quantity = Math.min(existing.quantity + qty, maxCopies) }
          else newCards.push({
            id: cardKey(card), name: card.name, quantity: qty,
            setCode: card.setCode ?? undefined, setName: card.setName ?? undefined,
            number: card.number ?? undefined, imageSmall: card.imageUrlSmall ?? undefined,
            type: card.type ?? undefined, hp: card.hp ?? undefined,
          })
          added++
        } else {
          // Fallback sem imagem — adiciona só com o nome
          newCards.push({ id: `manual:${setCode}-${number}`, name, quantity: qty, setCode, number })
          added++
        }
      } catch {
        failed.push(line)
      }
    }

    setCards(newCards)
    setImporting(false)
    setShowImport(false)
    setImportText('')

    if (failed.length > 0) {
      toast(`${added} cartas importadas. ${failed.length} não encontrada(s).`, { icon: '⚠️', duration: 4000 })
    } else {
      toast.success(`${added} cartas importadas!`)
    }
  }

  // ── Exportar lista de texto (formato padrão Pokémon) ──────────────────────

  function exportText() {
    const pokemon  = cards.filter(c => c.type === 'Pokémon' || c.type === 'Pokemon')
    const trainers = cards.filter(c => c.type === 'Trainer')
    const energy   = cards.filter(c => c.type === 'Energy')
    const other    = cards.filter(c => !['Pokémon', 'Pokemon', 'Trainer', 'Energy'].includes(c.type ?? ''))

    const fmt = (group: DeckCard[], label: string) =>
      group.length ? `${label}: ${group.reduce((s, c) => s + c.quantity, 0)}\n` +
        group.map(c => `${c.quantity} ${c.name}${c.setCode ? ` ${c.setCode.toUpperCase()}` : ''}${c.number ? ` ${c.number}` : ''}`).join('\n')
      : ''

    const text = [
      fmt(pokemon, 'Pokémon'), fmt(trainers, 'Trainer'), fmt(energy, 'Energy'), fmt(other, 'Other'),
      `\nTotal Cards: ${totalCards}`,
    ].filter(Boolean).join('\n\n')

    navigator.clipboard.writeText(text).then(() => toast.success('Lista copiada!'))
  }

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center" style={{ backgroundColor: C.bg }}>
        <Loader2 className="w-8 h-8 animate-spin" style={{ color: C.blue }} />
      </div>
    )
  }

  // Agrupa por tipo para o painel do deck
  const grouped = [
    { label: 'Pokémon', items: cards.filter(c => c.type === 'Pokémon' || c.type === 'Pokemon') },
    { label: 'Trainer', items: cards.filter(c => c.type === 'Trainer') },
    { label: 'Energy',  items: cards.filter(c => c.type === 'Energy') },
    { label: 'Outros',  items: cards.filter(c => !['Pokémon', 'Pokemon', 'Trainer', 'Energy'].includes(c.type ?? '')) },
  ].filter(g => g.items.length > 0)

  const totalUsd = cards.reduce((s, c) => {
    const cache = searchRes.find(r => cardKey(r) === c.id)
    const p = cache ? mainPrice(cache) : null
    return s + (p ? p * c.quantity : 0)
  }, 0)

  return (
    <div className="min-h-screen" style={{ backgroundColor: C.bg }}>
      <Toaster position="top-center" />

      {/* Header */}
      <header style={{ backgroundColor: C.navy }}>
        <div className="max-w-lg mx-auto px-5 pt-10 pb-5 space-y-4">
          <div className="flex items-center gap-3">
            <button onClick={() => router.push('/cliente/decks')}
              className="text-white/60 hover:text-white transition-colors">
              <ArrowLeft className="w-5 h-5" />
            </button>
            <input
              value={deckName}
              onChange={e => setDeckName(e.target.value)}
              placeholder="Nome do deck..."
              maxLength={100}
              className="flex-1 bg-transparent text-white font-black text-lg placeholder-white/40 outline-none"
            />
            <button
              onClick={() => setIsPublic(v => !v)}
              className="p-2 rounded-lg transition-colors"
              style={{ backgroundColor: 'rgba(255,255,255,0.1)', color: isPublic ? C.yellow : 'rgba(255,255,255,0.5)' }}
              title={isPublic ? 'Público' : 'Privado'}
            >
              {isPublic ? <Globe className="w-4 h-4" /> : <Lock className="w-4 h-4" />}
            </button>
          </div>

          <div className="flex gap-2 overflow-x-auto pb-1">
            {/* Jogo */}
            <select
              value={game}
              onChange={e => { setGame(e.target.value); setFormat(FORMATS[e.target.value]?.[0] ?? 'Standard') }}
              className="shrink-0 rounded-xl px-3 py-1.5 text-xs font-bold border-0 outline-none"
              style={{ backgroundColor: 'rgba(255,255,255,0.15)', color: '#fff' }}
            >
              {GAMES.map(g => <option key={g} value={g} style={{ color: C.navy }}>{g}</option>)}
            </select>
            {/* Formato */}
            <select
              value={format}
              onChange={e => setFormat(e.target.value)}
              className="shrink-0 rounded-xl px-3 py-1.5 text-xs font-bold border-0 outline-none"
              style={{ backgroundColor: 'rgba(255,255,255,0.15)', color: '#fff' }}
            >
              {(FORMATS[game] ?? ['Standard']).map(f => <option key={f} value={f} style={{ color: C.navy }}>{f}</option>)}
            </select>
            {/* Contagem */}
            <div className="ml-auto shrink-0 flex items-center gap-1 px-3 py-1.5 rounded-xl"
              style={{ backgroundColor: totalCards === maxCards ? '#22c55e20' : 'rgba(255,255,255,0.1)' }}>
              <span className={`text-xs font-black ${totalCards === maxCards ? 'text-green-400' : 'text-white/80'}`}>
                {totalCards}/{maxCards}
              </span>
            </div>
          </div>
        </div>
      </header>

      <div className="max-w-lg mx-auto px-4 py-4 space-y-4 pb-32">

        {/* Busca de cartas */}
        <div className="rounded-2xl overflow-hidden"
          style={{ backgroundColor: C.white, border: `1px solid ${C.border}`, boxShadow: '0 2px 8px rgba(12,61,90,0.06)' }}>
          <div className="flex items-center border-b" style={{ borderColor: C.border }}>
            <button
              onClick={() => setShowSearch(v => !v)}
              className="flex-1 flex items-center justify-between px-4 py-3"
            >
              <span className="flex items-center gap-2 font-bold text-sm" style={{ color: C.navy }}>
                <Search className="w-4 h-4" style={{ color: C.blue }} />
                Buscar cartas
              </span>
              {showSearch ? <ChevronUp className="w-4 h-4" style={{ color: C.muted }} /> : <ChevronDown className="w-4 h-4" style={{ color: C.muted }} />}
            </button>
            <button
              onClick={() => { setShowImport(v => !v); setShowSearch(false) }}
              className="px-3 py-3 text-xs font-bold flex items-center gap-1 border-l"
              style={{ borderColor: C.border, color: C.blue2 }}
              title="Importar lista de deck"
            >
              <Upload className="w-3.5 h-3.5" /> Importar
            </button>
          </div>

          {showSearch && (
            <>
              <div className="relative px-4 py-3 border-b" style={{ borderColor: C.border }}>
                <input
                  type="text"
                  value={query}
                  onChange={e => setQuery(e.target.value)}
                  placeholder={`Buscar por nome ou código (ex: PAL 058)...`}
                  className="w-full pr-8 py-2 pl-4 rounded-xl text-sm outline-none"
                  style={{ backgroundColor: C.bg, border: `1px solid ${C.border}`, color: C.navy }}
                />
                {searching && <Loader2 className="absolute right-7 top-1/2 -translate-y-1/2 w-4 h-4 animate-spin" style={{ color: C.muted }} />}
              </div>
              <div className="max-h-72 overflow-y-auto divide-y" style={{ borderColor: C.border }}>
                {searchRes.length === 0 && query.trim().length >= 2 && !searching && (
                  <p className="text-center py-4 text-sm" style={{ color: C.muted }}>Nenhuma carta encontrada.</p>
                )}
                {searchRes.map(c => (
                  <SearchCardRow
                    key={cardKey(c)} card={c} onAdd={addCard}
                    qty={cards.find(d => d.id === cardKey(c))?.quantity ?? 0}
                  />
                ))}
              </div>
            </>
          )}

          {/* Painel de import */}
          {showImport && (
            <div className="px-4 py-4 space-y-3">
              <p className="text-xs font-bold" style={{ color: C.muted }}>
                Cole a lista de deck (formato limitlesstcg / PTCG Live):
              </p>
              <p className="text-[10px] font-mono rounded-lg px-3 py-2" style={{ backgroundColor: C.bg, color: C.muted }}>
                {'4 Pikachu PAL 058\n2 Raichu PAR 021\n4 Professor\'s Research SVI 189'}
              </p>
              <textarea
                value={importText}
                onChange={e => setImportText(e.target.value)}
                placeholder="Cole o deck list aqui..."
                rows={8}
                className="w-full rounded-xl p-3 text-xs font-mono outline-none resize-none"
                style={{ backgroundColor: C.bg, border: `1px solid ${C.border}`, color: C.navy }}
              />
              <p className="text-[10px]" style={{ color: C.muted }}>
                ⚠️ Isso irá <strong>substituir</strong> as cartas do deck atual.
              </p>
              <div className="flex gap-2">
                <button
                  onClick={() => { setShowImport(false); setImportText('') }}
                  className="flex-1 py-2 rounded-xl text-sm font-bold"
                  style={{ backgroundColor: C.bg, color: C.muted }}
                >
                  Cancelar
                </button>
                <button
                  onClick={importDeckList}
                  disabled={importing || importText.trim().length === 0}
                  className="flex-1 py-2 rounded-xl text-sm font-bold flex items-center justify-center gap-2 disabled:opacity-50"
                  style={{ backgroundColor: C.blue, color: '#fff' }}
                >
                  {importing ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload className="w-4 h-4" />}
                  {importing ? 'Importando...' : 'Importar deck'}
                </button>
              </div>
            </div>
          )}
        </div>

        {/* Deck list */}
        {cards.length === 0 ? (
          <div className="text-center py-8" style={{ color: C.muted }}>
            <p className="text-sm">Busque cartas acima e adicione ao deck.</p>
          </div>
        ) : (
          <div className="rounded-2xl overflow-hidden"
            style={{ backgroundColor: C.white, border: `1px solid ${C.border}`, boxShadow: '0 2px 8px rgba(12,61,90,0.06)' }}>
            <div className="px-4 py-3 border-b flex items-center justify-between" style={{ borderColor: C.border }}>
              <span className="font-bold text-sm" style={{ color: C.navy }}>Lista do deck</span>
              <button onClick={exportText} className="text-xs font-bold flex items-center gap-1" style={{ color: C.blue2 }}>
                <Zap className="w-3 h-3" /> Copiar lista
              </button>
            </div>
            {grouped.map(group => (
              <div key={group.label}>
                <div className="px-4 py-1.5 flex items-center justify-between"
                  style={{ backgroundColor: C.bg, borderBottom: `1px solid ${C.border}` }}>
                  <span className="text-[10px] font-black uppercase tracking-widest" style={{ color: C.muted }}>
                    {group.label}
                  </span>
                  <span className="text-[10px] font-black" style={{ color: C.muted }}>
                    {group.items.reduce((s, c) => s + c.quantity, 0)}
                  </span>
                </div>
                <div className="divide-y" style={{ borderColor: C.border }}>
                  {group.items.map(c => (
                    <DeckCardRow
                      key={c.id} card={c} qty={c.quantity}
                      onInc={() => incCard(c.id)} onDec={() => decCard(c.id)}
                      onRemove={() => removeCard(c.id)} maxCopies={maxCopies}
                    />
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Bottom bar fixa */}
      <div className="fixed bottom-0 inset-x-0 z-50">
        <div className="max-w-lg mx-auto px-4 pb-6 pt-2">
          <div className="rounded-2xl shadow-xl overflow-hidden"
            style={{ backgroundColor: C.navy }}>
            <div className="flex items-center gap-4 px-5 py-4">
              <div className="flex-1">
                <p className="text-white/60 text-[10px] font-black uppercase tracking-widest">Deck</p>
                <p className="font-black text-white leading-tight">{totalCards}/{maxCards} cartas</p>
                {brlRate && totalUsd > 0 && (
                  <p className="text-[10px] mt-0.5" style={{ color: C.yellow }}>
                    ~R$ {(totalUsd * brlRate).toFixed(0)} est.
                  </p>
                )}
              </div>
              <button
                onClick={save}
                disabled={saving}
                className="flex items-center gap-2 px-6 py-3 rounded-xl font-black text-sm transition-all active:scale-95 disabled:opacity-60"
                style={{ backgroundColor: C.yellow, color: C.navy }}
              >
                {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                {isNew ? 'Criar deck' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
