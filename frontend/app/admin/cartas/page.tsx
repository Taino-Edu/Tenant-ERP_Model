'use client'
import { useState } from 'react'
import { tcgApi, CardCache } from '@/lib/api'
import toast from 'react-hot-toast'
import { Search, Loader2, Zap, Star, DollarSign, Database } from 'lucide-react'
import Image from 'next/image'
import clsx from 'clsx'

const GAMES = ['Pokemon', 'Magic: The Gathering', 'Yu-Gi-Oh!', 'One Piece TCG', 'Dragon Ball Super']

function CardItem({ card }: { card: CardCache }) {
  const [flipped, setFlipped] = useState(false)
  const price = card.marketPrices?.market ?? card.marketPrices?.mid
  const isFromCache = new Date(card.cachedAt).getTime() < Date.now() - 60000

  return (
    <div
      onClick={() => setFlipped(f => !f)}
      className="card cursor-pointer hover:border-brand-500/50 hover:shadow-brand-500/10 hover:shadow-xl transition-all duration-200 group"
    >
      {/* Imagem */}
      <div className="relative w-full aspect-[2.5/3.5] rounded-lg overflow-hidden bg-surface-800 mb-3">
        {card.imageUrlSmall ? (
          <Image
            src={flipped ? (card.imageUrlLarge ?? card.imageUrlSmall) : card.imageUrlSmall}
            alt={card.name}
            fill className="object-contain group-hover:scale-105 transition-transform duration-300"
          />
        ) : (
          <div className="absolute inset-0 flex items-center justify-center">
            <span className="text-4xl">🃏</span>
          </div>
        )}
        {isFromCache && (
          <div className="absolute top-1.5 right-1.5 bg-black/60 rounded-full p-1" title="Dados do cache">
            <Database className="w-3 h-3 text-brand-400" />
          </div>
        )}
      </div>

      {/* Info */}
      <div className="space-y-1.5">
        <p className="font-semibold text-white text-sm leading-tight">{card.name}</p>

        <div className="flex items-center gap-2 flex-wrap">
          {card.rarity && (
            <span className="badge bg-amber-500/10 text-amber-400 border-amber-500/20 text-[10px]">
              <Star className="w-2.5 h-2.5 mr-0.5" />{card.rarity}
            </span>
          )}
          {card.type && (
            <span className="badge bg-blue-500/10 text-blue-400 border-blue-500/20 text-[10px]">
              <Zap className="w-2.5 h-2.5 mr-0.5" />{card.type}
            </span>
          )}
        </div>

        {card.setName && <p className="text-xs text-gray-500 truncate">{card.setName} {card.number && `· #${card.number}`}</p>}

        {price && (
          <div className="flex items-center gap-1 text-accent-gold">
            <DollarSign className="w-3.5 h-3.5" />
            <span className="font-bold text-sm">${price.toFixed(2)}</span>
            <span className="text-xs text-gray-500">USD</span>
          </div>
        )}
      </div>
    </div>
  )
}

export default function CartasPage() {
  const [query, setQuery]   = useState('')
  const [game, setGame]     = useState('Pokemon')
  const [cards, setCards]   = useState<CardCache[]>([])
  const [loading, setLoading] = useState(false)
  const [searched, setSearched] = useState(false)
  const [totalPages, setTotalPages] = useState(0)
  const [page, setPage]     = useState(1)

  async function handleSearch(p = 1) {
    if (!query.trim()) { toast.error('Digite o nome da carta'); return }
    setLoading(true)
    try {
      const { data } = await tcgApi.search(query, game || undefined, p, 20)
      setCards(data.items ?? [])
      setTotalPages(data.totalPages ?? 0)
      setPage(p)
      setSearched(true)
      if (!data.items?.length) toast('Nenhuma carta encontrada. Verifique o nome ou o jogo.', { icon: '🔍' })
    } catch { toast.error('Erro ao buscar cartas. API TCG indisponível ou cache vazio.') }
    finally { setLoading(false) }
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-white">Busca de Cartas TCG</h1>
        <p className="text-gray-400 text-sm mt-0.5">Cache-First: MongoDB → API externa</p>
      </div>

      {/* Busca */}
      <div className="card flex flex-col sm:flex-row gap-3">
        <select className="input sm:w-56" value={game} onChange={e => setGame(e.target.value)}>
          <option value="">Todos os jogos</option>
          {GAMES.map(g => <option key={g}>{g}</option>)}
        </select>
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
          <input
            className="input pl-9"
            placeholder="Ex: Pikachu, Black Lotus, Blue-Eyes..."
            value={query}
            onChange={e => setQuery(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleSearch()}
          />
        </div>
        <button onClick={() => handleSearch()} disabled={loading} className="btn-primary px-6">
          {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Search className="w-4 h-4" />}
          Buscar
        </button>
      </div>

      {/* Legenda do cache */}
      <div className="flex items-center gap-4 text-xs text-gray-500">
        <div className="flex items-center gap-1.5">
          <Database className="w-3.5 h-3.5 text-brand-400" />
          <span>Ícone roxo = dado do cache MongoDB</span>
        </div>
        <span>·</span>
        <span>Clique na carta para ver a imagem maior</span>
      </div>

      {/* Grid de resultados */}
      {loading ? (
        <div className="flex items-center justify-center py-24">
          <div className="flex flex-col items-center gap-3">
            <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
            <p className="text-gray-400 text-sm">Buscando no cache e na API TCG...</p>
          </div>
        </div>
      ) : searched && cards.length > 0 ? (
        <>
          <p className="text-gray-400 text-sm">{cards.length} carta{cards.length !== 1 ? 's' : ''} encontrada{cards.length !== 1 ? 's' : ''}</p>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
            {cards.map(c => <CardItem key={c.tcgCardId} card={c} />)}
          </div>
          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-2">
              <button onClick={() => handleSearch(page - 1)} disabled={page === 1} className="btn-secondary px-3 py-1.5 text-sm">← Anterior</button>
              <span className="text-gray-400 text-sm">Página {page} de {totalPages}</span>
              <button onClick={() => handleSearch(page + 1)} disabled={page === totalPages} className="btn-secondary px-3 py-1.5 text-sm">Próxima →</button>
            </div>
          )}
        </>
      ) : !searched ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <div className="text-6xl mb-4">🃏</div>
          <p className="text-gray-400 font-medium">Busque cartas de qualquer TCG</p>
          <p className="text-gray-600 text-sm mt-1">Pokémon, Magic, Yu-Gi-Oh, One Piece e mais</p>
        </div>
      ) : null}
    </div>
  )
}
