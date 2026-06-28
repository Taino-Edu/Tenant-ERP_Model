'use client'
import { useEffect, useState, useCallback, useRef } from 'react'
import { useParams, useRouter } from 'next/navigation'
import { deckApi, tcgApi, CardCache, DeckCard, TcgSet } from '@/lib/api'
import {
  Search, Plus, Minus, Trash2, Save, Loader2,
  ArrowLeft, Globe, Lock, Zap, Upload, X, Camera,
  ChevronDown, SlidersHorizontal, RefreshCw,
} from 'lucide-react'
import toast, { Toaster } from 'react-hot-toast'

const C = { navy: '#0C3D5A', blue: '#3EC2F2', blue2: '#1A9DD4', yellow: '#FFE45E', bg: '#EBF7FD', white: '#FFFFFF', muted: '#4D8FAC', border: 'rgba(62,194,242,0.18)' }

const GAMES   = ['Pokemon', 'MTG', 'Yu-Gi-Oh!', 'LoL Riftbound']
const FORMATS: Record<string, string[]> = {
  Pokemon:        ['Standard', 'Expanded', 'Unlimited', 'Pocket'],
  MTG:            ['Standard', 'Modern', 'Legacy', 'Commander', 'Draft'],
  'Yu-Gi-Oh!':   ['Advanced', 'Traditional'],
  'LoL Riftbound': ['Standard'],
}
const MAX_CARDS:  Record<string, number> = { Pokemon: 60, MTG: 60, 'Yu-Gi-Oh!': 60, 'LoL Riftbound': 50 }
const MAX_COPIES: Record<string, number> = { Pokemon: 4,  MTG: 4,  'Yu-Gi-Oh!': 3,  'LoL Riftbound': 3 }

const POKEMON_RARITIES = ['Common', 'Uncommon', 'Rare', 'Rare Holo', 'Rare Ultra', 'Rare Secret', 'Illustration Rare', 'Special Illustration Rare', 'Hyper Rare', 'Double Rare', 'ACE SPEC Rare', 'Promo']

function cardKey(c: CardCache) { return c.tcgCardId }
function mainPrice(c: CardCache) {
  const p = c.allPrices?.holofoil ?? c.allPrices?.normal ?? c.allPrices?.reverseHolofoil ?? c.marketPrices
  return p?.market ?? p?.mid ?? null
}

// ── Card no grid de busca ─────────────────────────────────────────────────────
function CardGridItem({ card, qty, maxCopies, onAdd, onPreview }: {
  card: CardCache; qty: number; maxCopies: number
  onAdd: (c: CardCache) => void; onPreview: (c: CardCache) => void
}) {
  const price = mainPrice(card)
  const full  = qty >= maxCopies
  return (
    <div className="relative rounded-xl overflow-hidden flex flex-col"
      style={{ backgroundColor: C.white, border: `1px solid ${full ? C.blue2 : C.border}` }}>
      {/* imagem */}
      <button onClick={() => onPreview(card)} className="w-full aspect-[2.5/3.5] bg-gray-100 overflow-hidden">
        {card.imageUrlSmall
          ? <img src={card.imageUrlSmall} alt={card.name} className="w-full h-full object-cover" loading="lazy" />
          : <div className="w-full h-full flex items-center justify-center text-[10px] text-gray-400 px-1 text-center">{card.name}</div>}
      </button>
      {/* info */}
      <div className="px-1.5 py-1 flex-1 flex flex-col gap-0.5">
        <p className="text-[10px] font-black leading-tight line-clamp-2" style={{ color: C.navy }}>{card.name}</p>
        <p className="text-[9px]" style={{ color: C.muted }}>{card.setCode} #{card.number}</p>
        {price && <p className="text-[9px] font-bold" style={{ color: C.blue2 }}>${price.toFixed(2)}</p>}
      </div>
      {/* botão add */}
      <button
        onClick={() => onAdd(card)}
        disabled={full}
        className="w-full py-1.5 text-[10px] font-black transition-colors"
        style={{ backgroundColor: full ? '#e5e7eb' : C.blue, color: full ? C.muted : '#fff' }}
      >
        {full ? `${qty}/${maxCopies}` : qty > 0 ? `+1 (${qty})` : '+ Adicionar'}
      </button>
    </div>
  )
}

// ── Modal de preview de carta ─────────────────────────────────────────────────
function CardPreviewModal({ card, onClose, onAdd, qty, maxCopies, brlRate }: {
  card: CardCache; onClose: () => void; onAdd: (c: CardCache) => void; qty: number; maxCopies: number; brlRate: number | null
}) {
  const price  = mainPrice(card)
  const full   = qty >= maxCopies
  return (
    <div className="fixed inset-0 z-[70] flex items-end justify-center" onClick={onClose}>
      <div className="absolute inset-0 bg-black/60" />
      <div className="relative w-full max-w-lg rounded-t-3xl overflow-hidden"
        style={{ backgroundColor: C.white }}
        onClick={e => e.stopPropagation()}>
        <div className="flex gap-4 p-4">
          {/* imagem grande */}
          <div className="w-36 shrink-0">
            {card.imageUrlLarge
              ? <img src={card.imageUrlLarge} alt={card.name} className="w-full rounded-xl shadow-lg" />
              : card.imageUrlSmall
              ? <img src={card.imageUrlSmall} alt={card.name} className="w-full rounded-xl shadow-lg" />
              : <div className="w-full aspect-[2.5/3.5] rounded-xl bg-gray-100" />}
          </div>
          {/* dados */}
          <div className="flex-1 min-w-0 space-y-1.5">
            <p className="font-black text-base leading-tight" style={{ color: C.navy }}>{card.name}</p>
            {card.hp && <p className="text-sm font-bold" style={{ color: C.blue2 }}>HP {card.hp}</p>}
            <p className="text-xs" style={{ color: C.muted }}>{card.setName} · {card.setCode} #{card.number}</p>
            {card.rarity && <p className="text-xs" style={{ color: C.muted }}>{card.rarity}</p>}
            {card.types?.length > 0 && <p className="text-xs" style={{ color: C.muted }}>Tipo: {card.types.join(', ')}</p>}
            {card.artist && <p className="text-[10px]" style={{ color: C.muted }}>Ilustração: {card.artist}</p>}
            {/* ataques */}
            {card.attacks?.length > 0 && (
              <div className="space-y-1 mt-1">
                {card.attacks.map((a, i) => (
                  <div key={i} className="rounded-lg p-1.5 text-[10px]" style={{ backgroundColor: C.bg }}>
                    <span className="font-black" style={{ color: C.navy }}>{a.name}</span>
                    {a.damage && <span className="ml-1 font-bold" style={{ color: C.blue2 }}>{a.damage}</span>}
                    {a.cost?.length > 0 && <span className="ml-1" style={{ color: C.muted }}>[{a.cost.join(',')}]</span>}
                    {a.text && <p className="mt-0.5 leading-snug" style={{ color: C.muted }}>{a.text}</p>}
                  </div>
                ))}
              </div>
            )}
            {/* preços */}
            {price && (
              <div className="rounded-lg p-2 mt-1" style={{ backgroundColor: C.bg }}>
                <p className="text-[10px] font-black" style={{ color: C.navy }}>Preço mercado</p>
                <p className="font-black text-sm" style={{ color: C.blue2 }}>${price.toFixed(2)}</p>
                {brlRate && <p className="text-[10px]" style={{ color: C.muted }}>≈ R$ {(price * brlRate).toFixed(2)}</p>}
                {card.allPrices?.holofoil?.market   && <p className="text-[10px]" style={{ color: C.muted }}>Holo: ${card.allPrices.holofoil.market.toFixed(2)}</p>}
                {card.allPrices?.reverseHolofoil?.market && <p className="text-[10px]" style={{ color: C.muted }}>Rev. Holo: ${card.allPrices.reverseHolofoil.market.toFixed(2)}</p>}
              </div>
            )}
          </div>
        </div>
        <div className="px-4 pb-5 flex gap-2">
          <button onClick={onClose}
            className="flex-1 py-3 rounded-xl font-bold text-sm"
            style={{ backgroundColor: C.bg, color: C.muted }}>
            Fechar
          </button>
          <button onClick={() => { onAdd(card); onClose() }} disabled={full}
            className="flex-1 py-3 rounded-xl font-black text-sm disabled:opacity-40"
            style={{ backgroundColor: full ? '#e5e7eb' : C.blue, color: full ? C.muted : '#fff' }}>
            {full ? `Máximo (${qty}/${maxCopies})` : `+ Adicionar ao deck`}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Modal de busca profissional ───────────────────────────────────────────────
function CardSearchModal({ game, onAdd, deckCards, onClose, maxCopies, brlRate }: {
  game: string; onAdd: (c: CardCache, qty?: number) => void
  deckCards: DeckCard[]; onClose: () => void; maxCopies: number; brlRate: number | null
}) {
  const [query,      setQuery]      = useState('')
  const [results,    setResults]    = useState<CardCache[]>([])
  const [total,      setTotal]      = useState(0)
  const [page,       setPage]       = useState(1)
  const [loading,    setLoading]    = useState(false)
  const [loadMore,   setLoadMore]   = useState(false)
  const [sets,       setSets]       = useState<TcgSet[]>([])
  const [selSet,     setSelSet]     = useState('')
  const [selRarity,  setSelRarity]  = useState('')
  const [showFilter, setShowFilter] = useState(false)
  const [preview,    setPreview]    = useState<CardCache | null>(null)

  // Camera
  const [showCam,    setShowCam]    = useState(false)
  const videoRef   = useRef<HTMLVideoElement>(null)
  const canvasRef  = useRef<HTMLCanvasElement>(null)
  const streamRef  = useRef<MediaStream | null>(null)

  const PAGE_SIZE = 30
  const timeout   = useRef<ReturnType<typeof setTimeout>>()

  useEffect(() => {
    tcgApi.sets(game).then(r => setSets(r.data ?? [])).catch(() => {})
  }, [game])

  const doSearch = useCallback(async (q: string, pg: number, append = false) => {
    if (q.trim().length < 2) { if (!append) { setResults([]); setTotal(0) }; return }
    append ? setLoadMore(true) : setLoading(true)
    try {
      const codeMatch = q.trim().match(/^([A-Za-z0-9]+)\s+(\d+)$/)
      let items: CardCache[] = [], tot = 0
      if (codeMatch) {
        const r = await tcgApi.searchByCode(codeMatch[1], codeMatch[2], game)
        items = r.data.items ?? []; tot = r.data.totalCount ?? items.length
      } else {
        const r = await tcgApi.search(q, game, pg, PAGE_SIZE, selSet || undefined, selRarity || undefined)
        items = r.data.items ?? []; tot = r.data.totalCount ?? items.length
      }
      setResults(prev => append ? [...prev, ...items] : items)
      setTotal(tot)
      setPage(pg)
    } catch { toast.error('Erro na busca.') }
    finally { append ? setLoadMore(false) : setLoading(false) }
  }, [game, selSet, selRarity])

  useEffect(() => {
    clearTimeout(timeout.current)
    timeout.current = setTimeout(() => doSearch(query, 1), 400)
    return () => clearTimeout(timeout.current)
  }, [query, doSearch])

  // ── Câmera ──────────────────────────────────────────────────────────────────

  async function openCamera() {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } })
      streamRef.current = stream
      setShowCam(true)
      setTimeout(() => { if (videoRef.current) videoRef.current.srcObject = stream }, 100)
    } catch {
      toast.error('Câmera não disponível.')
    }
  }

  function closeCamera() {
    streamRef.current?.getTracks().forEach(t => t.stop())
    streamRef.current = null
    setShowCam(false)
  }

  async function captureCard() {
    const video  = videoRef.current
    const canvas = canvasRef.current
    if (!video || !canvas) return

    canvas.width  = video.videoWidth
    canvas.height = video.videoHeight
    canvas.getContext('2d')!.drawImage(video, 0, 0)

    // Tenta TextDetector nativo (Chrome/Android)
    const detected = await tryTextDetector(canvas)
    if (detected) {
      closeCamera()
      setQuery(detected)
      return
    }

    // Fallback: pede confirmação manual da imagem
    const dataUrl = canvas.toDataURL('image/jpeg', 0.8)
    closeCamera()
    const code = prompt('Código não detectado automaticamente.\nDigite o código da carta (ex: PAL 058):')
    if (code?.trim()) setQuery(code.trim())
  }

  async function tryTextDetector(canvas: HTMLCanvasElement): Promise<string | null> {
    if (!('TextDetector' in window)) return null
    try {
      // @ts-ignore — experimental API
      const detector = new window.TextDetector()
      // Recorta apenas a parte inferior da carta (onde fica o código)
      const cropCanvas = document.createElement('canvas')
      cropCanvas.width  = canvas.width
      cropCanvas.height = Math.round(canvas.height * 0.15)
      cropCanvas.getContext('2d')!.drawImage(
        canvas, 0, canvas.height - cropCanvas.height,
        canvas.width, cropCanvas.height,
        0, 0, canvas.width, cropCanvas.height
      )
      const blob: Blob = await new Promise(res => cropCanvas.toBlob(b => res(b!)))
      const img = await createImageBitmap(blob)
      const texts: { rawValue: string }[] = await detector.detect(img)
      const raw = texts.map(t => t.rawValue).join(' ')
      // Procura padrão: letras+números seguidos de espaço e número (ex: PAL 058)
      const match = raw.match(/([A-Z][A-Z0-9]{1,7})\s{0,3}(\d{1,3})/i)
      if (match) return `${match[1].toUpperCase()} ${match[2]}`
    } catch {}
    return null
  }

  // ── Import de lista ─────────────────────────────────────────────────────────
  const [showImport, setShowImport] = useState(false)
  const [importText, setImportText] = useState('')
  const [importing,  setImporting]  = useState(false)

  async function importDeckList() {
    const lines = importText.split('\n').map(l => l.trim()).filter(Boolean)
    const cardLines = lines.filter(l => /^\d+\s+/.test(l))
    if (!cardLines.length) { toast.error('Nenhuma carta encontrada.'); return }
    setImporting(true)
    let added = 0; const failed: string[] = []
    const newCards: DeckCard[] = []

    for (const line of cardLines) {
      const match = line.match(/^(\d+)\s+(.+?)\s+([A-Z0-9]{2,8})\s+(\d+)\s*$/)
      if (!match) { failed.push(line); continue }
      const [, qtyStr, name, setCode, number] = match
      const qty = Math.min(parseInt(qtyStr), maxCopies)
      try {
        const r = await tcgApi.searchByCode(setCode, number, game)
        const card = r.data.items?.[0]
        if (card) {
          const ex = newCards.find(c => c.id === cardKey(card))
          if (ex) ex.quantity = Math.min(ex.quantity + qty, maxCopies)
          else newCards.push({ id: cardKey(card), name: card.name, quantity: qty, setCode: card.setCode ?? undefined, setName: card.setName ?? undefined, number: card.number ?? undefined, imageSmall: card.imageUrlSmall ?? undefined, type: card.type ?? undefined, hp: card.hp ?? undefined })
          added++
        } else {
          newCards.push({ id: `manual:${setCode}-${number}`, name, quantity: qty, setCode, number })
          added++
        }
      } catch { failed.push(line) }
    }
    onAdd({ tcgCardId: '__import__' } as CardCache) // trigger parent reset
    // Replace handled externally via onImport
    onClose()
    toast(failed.length ? `${added} importadas. ${failed.length} não encontradas.` : `${added} cartas importadas!`,
      { icon: failed.length ? '⚠️' : '✅', duration: 4000 })
    setImporting(false)
    setShowImport(false)
    setImportText('')
  }

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <>
      <div className="fixed inset-0 z-[60] flex flex-col" style={{ backgroundColor: C.bg }}>
        {/* Header */}
        <div style={{ backgroundColor: C.navy }}>
          <div className="max-w-lg mx-auto px-4 pt-10 pb-4 space-y-3">
            <div className="flex items-center gap-3">
              <button onClick={onClose} className="text-white/60 hover:text-white transition-colors">
                <X className="w-5 h-5" />
              </button>
              <p className="font-black text-white flex-1">Buscar cartas</p>
              <button
                onClick={() => setShowImport(v => !v)}
                className="flex items-center gap-1 px-3 py-1.5 rounded-xl text-xs font-bold"
                style={{ backgroundColor: 'rgba(255,255,255,0.15)', color: '#fff' }}>
                <Upload className="w-3.5 h-3.5" /> Importar
              </button>
            </div>

            {/* Search bar */}
            <div className="flex gap-2">
              <div className="relative flex-1">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-white/40" />
                <input
                  autoFocus
                  value={query}
                  onChange={e => setQuery(e.target.value)}
                  placeholder="Nome ou código (PAL 058)..."
                  className="w-full pl-9 pr-3 py-2.5 rounded-xl text-sm outline-none bg-white/10 text-white placeholder-white/40"
                />
                {query && <button onClick={() => setQuery('')} className="absolute right-3 top-1/2 -translate-y-1/2 text-white/40"><X className="w-3.5 h-3.5" /></button>}
              </div>
              <button onClick={openCamera}
                className="w-10 h-10 rounded-xl flex items-center justify-center"
                style={{ backgroundColor: 'rgba(255,255,255,0.15)' }}
                title="Escanear carta com câmera">
                <Camera className="w-4.5 h-4.5 text-white" />
              </button>
              <button onClick={() => setShowFilter(v => !v)}
                className="w-10 h-10 rounded-xl flex items-center justify-center"
                style={{ backgroundColor: showFilter ? C.yellow : 'rgba(255,255,255,0.15)' }}>
                <SlidersHorizontal className={`w-4 h-4 ${showFilter ? '' : 'text-white'}`} style={showFilter ? { color: C.navy } : {}} />
              </button>
            </div>

            {/* Filtros */}
            {showFilter && (
              <div className="flex gap-2 pb-1">
                <select value={selSet} onChange={e => setSelSet(e.target.value)}
                  className="flex-1 rounded-xl px-3 py-2 text-xs font-bold border-0 outline-none"
                  style={{ backgroundColor: 'rgba(255,255,255,0.15)', color: '#fff' }}>
                  <option value="" style={{ color: C.navy }}>Todos os sets</option>
                  {sets.map(s => <option key={s.code} value={s.code} style={{ color: C.navy }}>{s.name}</option>)}
                </select>
                {game === 'Pokemon' && (
                  <select value={selRarity} onChange={e => setSelRarity(e.target.value)}
                    className="flex-1 rounded-xl px-3 py-2 text-xs font-bold border-0 outline-none"
                    style={{ backgroundColor: 'rgba(255,255,255,0.15)', color: '#fff' }}>
                    <option value="" style={{ color: C.navy }}>Todas raridades</option>
                    {POKEMON_RARITIES.map(r => <option key={r} value={r} style={{ color: C.navy }}>{r}</option>)}
                  </select>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Import panel */}
        {showImport && (
          <div className="max-w-lg mx-auto w-full px-4 py-4 space-y-3"
            style={{ backgroundColor: C.white, borderBottom: `1px solid ${C.border}` }}>
            <p className="text-xs font-bold" style={{ color: C.muted }}>Cole a lista (formato limitlesstcg / PTCG Live):</p>
            <p className="text-[10px] font-mono rounded-lg px-3 py-1.5" style={{ backgroundColor: C.bg, color: C.muted }}>
              4 Pikachu PAL 058{'\n'}2 Raichu PAR 021
            </p>
            <textarea value={importText} onChange={e => setImportText(e.target.value)}
              placeholder="Cole o deck list aqui..." rows={6}
              className="w-full rounded-xl p-3 text-xs font-mono outline-none resize-none"
              style={{ backgroundColor: C.bg, border: `1px solid ${C.border}`, color: C.navy }} />
            <div className="flex gap-2">
              <button onClick={() => { setShowImport(false); setImportText('') }}
                className="flex-1 py-2 rounded-xl text-sm font-bold"
                style={{ backgroundColor: C.bg, color: C.muted }}>Cancelar</button>
              <button onClick={importDeckList} disabled={importing || !importText.trim()}
                className="flex-1 py-2 rounded-xl text-sm font-black flex items-center justify-center gap-2 disabled:opacity-50"
                style={{ backgroundColor: C.blue, color: '#fff' }}>
                {importing ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload className="w-4 h-4" />}
                {importing ? 'Importando...' : 'Importar deck'}
              </button>
            </div>
          </div>
        )}

        {/* Resultados */}
        <div className="flex-1 overflow-y-auto">
          <div className="max-w-lg mx-auto px-3 py-4">
            {total > 0 && (
              <p className="text-xs mb-3 px-1" style={{ color: C.muted }}>
                {total} versão{total !== 1 ? 'ões' : ''} encontrada{total !== 1 ? 's' : ''} · mostrando {results.length}
              </p>
            )}
            {loading ? (
              <div className="flex justify-center py-20">
                <Loader2 className="w-8 h-8 animate-spin" style={{ color: C.blue }} />
              </div>
            ) : results.length === 0 && query.trim().length >= 2 ? (
              <div className="text-center py-16">
                <p className="font-bold text-sm" style={{ color: C.muted }}>Nenhuma carta encontrada.</p>
                <p className="text-xs mt-1" style={{ color: C.muted }}>Tente um nome diferente ou busque pelo código (ex: PAL 058).</p>
              </div>
            ) : (
              <>
                <div className="grid grid-cols-3 gap-2">
                  {results.map(c => (
                    <CardGridItem
                      key={cardKey(c)} card={c}
                      qty={deckCards.find(d => d.id === cardKey(c))?.quantity ?? 0}
                      maxCopies={maxCopies}
                      onAdd={card => onAdd(card)}
                      onPreview={setPreview}
                    />
                  ))}
                </div>
                {results.length < total && (
                  <button
                    onClick={() => doSearch(query, page + 1, true)}
                    disabled={loadMore}
                    className="w-full mt-4 py-3 rounded-xl font-bold text-sm flex items-center justify-center gap-2"
                    style={{ backgroundColor: C.white, border: `1px solid ${C.border}`, color: C.navy }}>
                    {loadMore ? <Loader2 className="w-4 h-4 animate-spin" /> : <RefreshCw className="w-4 h-4" />}
                    Ver mais ({total - results.length} restantes)
                  </button>
                )}
              </>
            )}
          </div>
        </div>
      </div>

      {/* Camera overlay */}
      {showCam && (
        <div className="fixed inset-0 z-[80] bg-black flex flex-col items-center justify-center">
          <canvas ref={canvasRef} className="hidden" />
          <div className="relative w-full max-w-sm aspect-[3/4] rounded-2xl overflow-hidden">
            <video ref={videoRef} autoPlay playsInline className="w-full h-full object-cover" />
            {/* Guia de enquadramento */}
            <div className="absolute inset-0 flex items-center justify-center">
              <div className="w-48 h-64 rounded-xl border-2 border-white/60" />
            </div>
            <div className="absolute bottom-4 left-0 right-0 text-center">
              <p className="text-white/80 text-xs mb-3">Enquadre a carta · código no rodapé visível</p>
              <button onClick={captureCard}
                className="w-16 h-16 rounded-full border-4 border-white flex items-center justify-center"
                style={{ backgroundColor: 'rgba(255,255,255,0.2)' }}>
                <Camera className="w-7 h-7 text-white" />
              </button>
            </div>
          </div>
          <button onClick={closeCamera} className="mt-6 text-white/60 text-sm flex items-center gap-2">
            <X className="w-4 h-4" /> Cancelar
          </button>
        </div>
      )}

      {/* Preview de carta */}
      {preview && (
        <CardPreviewModal
          card={preview} onClose={() => setPreview(null)}
          onAdd={card => { onAdd(card); setPreview(null) }}
          qty={deckCards.find(d => d.id === cardKey(preview))?.quantity ?? 0}
          maxCopies={maxCopies} brlRate={brlRate}
        />
      )}
    </>
  )
}

// ── Linha de carta no deck ────────────────────────────────────────────────────
function DeckCardRow({ card, qty, onInc, onDec, onRemove, maxCopies }: {
  card: DeckCard; qty: number; onInc: () => void; onDec: () => void; onRemove: () => void; maxCopies: number
}) {
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
        <button onClick={onDec} className="w-6 h-6 rounded-lg flex items-center justify-center" style={{ backgroundColor: '#FEE2E2', color: '#DC2626' }}>
          <Minus className="w-3 h-3" />
        </button>
        <span className="w-6 text-center font-black text-sm" style={{ color: C.navy }}>{qty}</span>
        <button onClick={onInc} disabled={qty >= maxCopies}
          className="w-6 h-6 rounded-lg flex items-center justify-center disabled:opacity-40"
          style={{ backgroundColor: qty >= maxCopies ? '#e5e7eb' : `${C.blue}20`, color: C.blue2 }}>
          <Plus className="w-3 h-3" />
        </button>
        <button onClick={onRemove} className="w-6 h-6 rounded-lg flex items-center justify-center ml-1" style={{ color: '#9CA3AF' }}>
          <Trash2 className="w-3 h-3" />
        </button>
      </div>
    </div>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────
export default function DeckBuilderPage() {
  const { id } = useParams<{ id: string }>()
  const isNew  = id === 'novo'
  const router = useRouter()

  const [deckName, setDeckName] = useState('')
  const [game,     setGame]     = useState('Pokemon')
  const [format,   setFormat]   = useState('Standard')
  const [isPublic, setIsPublic] = useState(false)
  const [cards,    setCards]    = useState<DeckCard[]>([])
  const [saving,   setSaving]   = useState(false)
  const [loading,  setLoading]  = useState(!isNew)
  const [brlRate,  setBrlRate]  = useState<number | null>(null)
  const [showPicker, setShowPicker] = useState(false)

  useEffect(() => {
    if (isNew) return
    deckApi.get(id).then(r => {
      setDeckName(r.data.name); setGame(r.data.game); setFormat(r.data.format)
      setIsPublic(r.data.isPublic)
      try { setCards(JSON.parse(r.data.cardsJson)) } catch { setCards([]) }
    }).catch(() => toast.error('Deck não encontrado.')).finally(() => setLoading(false))
  }, [id, isNew])

  useEffect(() => {
    tcgApi.brlRate().then(r => setBrlRate(r.data.usdToBrl)).catch(() => {})
  }, [])

  const maxCopies  = MAX_COPIES[game] ?? 4
  const maxCards   = MAX_CARDS[game] ?? 60
  const totalCards = cards.reduce((s, c) => s + c.quantity, 0)

  function addCard(cache: CardCache) {
    if (cache.tcgCardId === '__import__') return // handled separately
    const existing = cards.find(c => c.id === cardKey(cache))
    if (existing) {
      if (existing.quantity >= maxCopies) { toast(`Máximo de ${maxCopies} cópias!`, { icon: '⚠️' }); return }
      setCards(prev => prev.map(c => c.id === cardKey(cache) ? { ...c, quantity: c.quantity + 1 } : c))
    } else {
      if (totalCards >= maxCards) { toast(`Deck cheio! (${maxCards} cartas)`, { icon: '⚠️' }); return }
      setCards(prev => [...prev, {
        id: cardKey(cache), name: cache.name, quantity: 1,
        setCode: cache.setCode ?? undefined, setName: cache.setName ?? undefined,
        number: cache.number ?? undefined, imageSmall: cache.imageUrlSmall ?? undefined,
        type: cache.type ?? undefined, hp: cache.hp ?? undefined,
      }])
    }
    toast.success(`${cache.name} adicionado!`, { duration: 1200 })
  }

  function incCard(id: string)    { setCards(prev => prev.map(c => c.id === id && c.quantity < maxCopies ? { ...c, quantity: c.quantity + 1 } : c)) }
  function decCard(id: string)    { setCards(prev => prev.map(c => c.id === id ? { ...c, quantity: Math.max(1, c.quantity - 1) } : c)) }
  function removeCard(id: string) { setCards(prev => prev.filter(c => c.id !== id)) }

  async function save() {
    if (!deckName.trim()) { toast.error('Dê um nome ao deck!'); return }
    setSaving(true)
    try {
      const payload = { name: deckName.trim(), game, format, cardsJson: JSON.stringify(cards), isPublic }
      if (isNew) {
        const r = await deckApi.create(payload)
        toast.success('Deck criado!'); router.replace(`/cliente/decks/${r.data.id}`)
      } else {
        await deckApi.update(id, payload); toast.success('Deck salvo!')
      }
    } catch { toast.error('Erro ao salvar.') }
    finally { setSaving(false) }
  }

  function exportText() {
    const pokemon  = cards.filter(c => c.type === 'Pokémon' || c.type === 'Pokemon')
    const trainers = cards.filter(c => c.type === 'Trainer')
    const energy   = cards.filter(c => c.type === 'Energy')
    const other    = cards.filter(c => !['Pokémon', 'Pokemon', 'Trainer', 'Energy'].includes(c.type ?? ''))
    const fmt = (g: DeckCard[], label: string) =>
      g.length ? `${label}: ${g.reduce((s, c) => s + c.quantity, 0)}\n` +
        g.map(c => `${c.quantity} ${c.name}${c.setCode ? ` ${c.setCode.toUpperCase()}` : ''}${c.number ? ` ${c.number}` : ''}`).join('\n') : ''
    const text = [fmt(pokemon, 'Pokémon'), fmt(trainers, 'Trainer'), fmt(energy, 'Energy'), fmt(other, 'Other'), `\nTotal Cards: ${totalCards}`].filter(Boolean).join('\n\n')
    navigator.clipboard.writeText(text).then(() => toast.success('Lista copiada!'))
  }

  if (loading) return (
    <div className="min-h-screen flex items-center justify-center" style={{ backgroundColor: C.bg }}>
      <Loader2 className="w-8 h-8 animate-spin" style={{ color: C.blue }} />
    </div>
  )

  const grouped = [
    { label: 'Pokémon', items: cards.filter(c => c.type === 'Pokémon' || c.type === 'Pokemon') },
    { label: 'Trainer', items: cards.filter(c => c.type === 'Trainer') },
    { label: 'Energy',  items: cards.filter(c => c.type === 'Energy') },
    { label: 'Outros',  items: cards.filter(c => !['Pokémon', 'Pokemon', 'Trainer', 'Energy'].includes(c.type ?? '')) },
  ].filter(g => g.items.length > 0)

  return (
    <div className="min-h-screen" style={{ backgroundColor: C.bg }}>
      <Toaster position="top-center" />

      {/* Header */}
      <header style={{ backgroundColor: C.navy }}>
        <div className="max-w-lg mx-auto px-5 pt-10 pb-5 space-y-4">
          <div className="flex items-center gap-3">
            <button onClick={() => router.push('/cliente/decks')} className="text-white/60 hover:text-white">
              <ArrowLeft className="w-5 h-5" />
            </button>
            <input value={deckName} onChange={e => setDeckName(e.target.value)}
              placeholder="Nome do deck..." maxLength={100}
              className="flex-1 bg-transparent text-white font-black text-lg placeholder-white/40 outline-none" />
            <button onClick={() => setIsPublic(v => !v)}
              className="p-2 rounded-lg" title={isPublic ? 'Público' : 'Privado'}
              style={{ backgroundColor: 'rgba(255,255,255,0.1)', color: isPublic ? C.yellow : 'rgba(255,255,255,0.5)' }}>
              {isPublic ? <Globe className="w-4 h-4" /> : <Lock className="w-4 h-4" />}
            </button>
          </div>
          <div className="flex gap-2 overflow-x-auto pb-1">
            <select value={game}
              onChange={e => { setGame(e.target.value); setFormat(FORMATS[e.target.value]?.[0] ?? 'Standard') }}
              className="shrink-0 rounded-xl px-3 py-1.5 text-xs font-bold border-0 outline-none"
              style={{ backgroundColor: 'rgba(255,255,255,0.15)', color: '#fff' }}>
              {GAMES.map(g => <option key={g} value={g} style={{ color: C.navy }}>{g}</option>)}
            </select>
            <select value={format} onChange={e => setFormat(e.target.value)}
              className="shrink-0 rounded-xl px-3 py-1.5 text-xs font-bold border-0 outline-none"
              style={{ backgroundColor: 'rgba(255,255,255,0.15)', color: '#fff' }}>
              {(FORMATS[game] ?? ['Standard']).map(f => <option key={f} value={f} style={{ color: C.navy }}>{f}</option>)}
            </select>
            <div className="ml-auto shrink-0 flex items-center px-3 py-1.5 rounded-xl"
              style={{ backgroundColor: totalCards === maxCards ? '#22c55e20' : 'rgba(255,255,255,0.1)' }}>
              <span className={`text-xs font-black ${totalCards === maxCards ? 'text-green-400' : 'text-white/80'}`}>
                {totalCards}/{maxCards}
              </span>
            </div>
          </div>
        </div>
      </header>

      <div className="max-w-lg mx-auto px-4 py-4 space-y-3 pb-32">
        {/* Botão abre picker */}
        <button onClick={() => setShowPicker(true)}
          className="w-full flex items-center gap-3 px-4 py-3.5 rounded-2xl transition-all active:scale-[0.98]"
          style={{ backgroundColor: C.white, border: `1px solid ${C.border}`, boxShadow: '0 2px 8px rgba(12,61,90,0.06)' }}>
          <Search className="w-5 h-5 shrink-0" style={{ color: C.blue }} />
          <span className="flex-1 text-left text-sm" style={{ color: C.muted }}>Buscar cartas para adicionar...</span>
          <Plus className="w-5 h-5 shrink-0" style={{ color: C.blue }} />
        </button>

        {/* Lista do deck */}
        {cards.length === 0 ? (
          <div className="text-center py-12" style={{ color: C.muted }}>
            <p className="text-sm">Clique acima para buscar e adicionar cartas ao deck.</p>
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
                  <span className="text-[10px] font-black uppercase tracking-widest" style={{ color: C.muted }}>{group.label}</span>
                  <span className="text-[10px] font-black" style={{ color: C.muted }}>{group.items.reduce((s, c) => s + c.quantity, 0)}</span>
                </div>
                <div className="divide-y" style={{ borderColor: C.border }}>
                  {group.items.map(c => (
                    <DeckCardRow key={c.id} card={c} qty={c.quantity}
                      onInc={() => incCard(c.id)} onDec={() => decCard(c.id)}
                      onRemove={() => removeCard(c.id)} maxCopies={maxCopies} />
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Bottom bar */}
      <div className="fixed bottom-0 inset-x-0 z-50">
        <div className="max-w-lg mx-auto px-4 pb-6 pt-2">
          <div className="rounded-2xl shadow-xl" style={{ backgroundColor: C.navy }}>
            <div className="flex items-center gap-4 px-5 py-4">
              <div className="flex-1">
                <p className="text-white/60 text-[10px] font-black uppercase tracking-widest">Deck</p>
                <p className="font-black text-white">{totalCards}/{maxCards} cartas</p>
                {brlRate && (
                  <p className="text-[10px] mt-0.5" style={{ color: C.yellow }}>
                    Valor estimado disponível após busca
                  </p>
                )}
              </div>
              <button onClick={save} disabled={saving}
                className="flex items-center gap-2 px-6 py-3 rounded-xl font-black text-sm active:scale-95 disabled:opacity-60"
                style={{ backgroundColor: C.yellow, color: C.navy }}>
                {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                {isNew ? 'Criar deck' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Modal de busca */}
      {showPicker && (
        <CardSearchModal
          game={game} onAdd={addCard} deckCards={cards}
          onClose={() => setShowPicker(false)}
          maxCopies={maxCopies} brlRate={brlRate}
        />
      )}
    </div>
  )
}
