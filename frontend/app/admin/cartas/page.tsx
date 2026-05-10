'use client'
import { useState } from 'react'
import { tcgApi, productApi, CardCache } from '@/lib/api'
import toast from 'react-hot-toast'
import { Search, Loader2, Zap, Star, DollarSign, Database, PackagePlus, X, Check, PlusCircle } from 'lucide-react'
import Image from 'next/image'
import clsx from 'clsx'

// ── Modal: adicionar carta PRÓPRIA ao estoque (sem busca na API TCG) ──────────

function AddOwnCardModal({ onClose }: { onClose: () => void }) {
  const [name, setName]       = useState('')
  const [game, setGame]       = useState('Pokemon')
  const [set, setSet]         = useState('')
  const [price, setPrice]     = useState('')
  const [qty, setQty]         = useState(1)
  const [minStock, setMin]    = useState(1)
  const [saving, setSaving]   = useState(false)

  async function handleSave() {
    if (!name.trim()) { toast.error('Informe o nome da carta'); return }
    const priceVal = parseFloat(price.replace(',', '.'))
    if (!price || isNaN(priceVal) || priceVal <= 0) { toast.error('Informe um preço válido'); return }
    setSaving(true)
    try {
      await productApi.create({
        name:          name.trim(),
        category:      'Carta Avulsa',
        description:   `${game}${set ? ` — ${set}` : ''}`,
        priceInCents:  Math.round(priceVal * 100),
        stockQuantity: qty,
        minimumStock:  minStock,
        isActive:      true,
        isFeatured:    false,
        imageUrl:      null,
      })
      toast.success(`"${name.trim()}" adicionada ao estoque!`)
      onClose()
    } catch {
      toast.error('Erro ao adicionar ao estoque.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="card w-full max-w-md animate-bounce-in">
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-base font-bold text-white flex items-center gap-2">
            <PlusCircle className="w-5 h-5 text-brand-400" />
            Adicionar Carta Própria
          </h2>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-300">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="space-y-4">
          <div>
            <label className="label">Nome da carta *</label>
            <input className="input" placeholder="Ex: Pikachu VMAX" value={name} onChange={e => setName(e.target.value)} />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Jogo</label>
              <select className="input" value={game} onChange={e => setGame(e.target.value)}>
                {['Pokemon', 'Magic: The Gathering', 'Yu-Gi-Oh!', 'One Piece TCG', 'Dragon Ball Super', 'Outro'].map(g => (
                  <option key={g}>{g}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="label">Coleção / Set</label>
              <input className="input" placeholder="Ex: Obsidian Flames" value={set} onChange={e => setSet(e.target.value)} />
            </div>
          </div>

          <div>
            <label className="label">Preço de venda (R$) *</label>
            <input className="input" type="number" min="0.01" step="0.01" placeholder="Ex: 35,00" value={price} onChange={e => setPrice(e.target.value)} />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Quantidade *</label>
              <input className="input" type="number" min="1" value={qty} onChange={e => setQty(Math.max(1, parseInt(e.target.value) || 1))} />
            </div>
            <div>
              <label className="label">Estoque mínimo</label>
              <input className="input" type="number" min="0" value={minStock} onChange={e => setMin(Math.max(0, parseInt(e.target.value) || 0))} />
            </div>
          </div>
        </div>

        <div className="flex gap-3 mt-5">
          <button onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
          <button onClick={handleSave} disabled={saving} className="btn-primary flex-1 justify-center">
            {saving ? <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</> : <><Check className="w-4 h-4" /> Adicionar</>}
          </button>
        </div>
      </div>
    </div>
  )
}

const GAMES = ['Pokemon', 'Magic: The Gathering', 'Yu-Gi-Oh!', 'One Piece TCG', 'Dragon Ball Super']

// ── Modal: adicionar carta ao estoque ─────────────────────────────────────────

function AddToStockModal({ card, onClose }: { card: CardCache; onClose: () => void }) {
  const usdPrice = card.marketPrices?.market ?? card.marketPrices?.mid
  // Sugere preço em reais: se tiver preço USD, multiplica por 6 (taxa aproximada)
  const [priceReais, setPriceReais] = useState(usdPrice ? (usdPrice * 6).toFixed(2) : '')
  const [qty, setQty]               = useState(1)
  const [minStock, setMinStock]     = useState(1)
  const [saving, setSaving]         = useState(false)

  async function handleSave() {
    const priceVal = parseFloat(priceReais.replace(',', '.'))
    if (!priceReais || isNaN(priceVal) || priceVal <= 0) {
      toast.error('Informe um preço válido')
      return
    }
    setSaving(true)
    try {
      await productApi.create({
        name:          card.name,
        category:      'Carta Avulsa',
        description:   card.setName ? `${card.game} — ${card.setName}${card.number ? ` #${card.number}` : ''}` : card.game,
        priceInCents:  Math.round(priceVal * 100),
        stockQuantity: qty,
        minimumStock:  minStock,
        isActive:      true,
        isFeatured:    false,
        imageUrl:      card.imageUrlSmall ?? null,
      })
      toast.success(`"${card.name}" adicionada ao estoque!`)
      onClose()
    } catch {
      toast.error('Erro ao adicionar ao estoque.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="card w-full max-w-md animate-bounce-in">

        <div className="flex items-center justify-between mb-5">
          <h2 className="text-base font-bold text-white flex items-center gap-2">
            <PackagePlus className="w-5 h-5 text-brand-400" />
            Adicionar ao Estoque
          </h2>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-300">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Preview da carta */}
        <div className="flex items-center gap-3 bg-surface-800 rounded-xl p-3 mb-5">
          {card.imageUrlSmall && (
            <div className="relative w-12 h-16 shrink-0 rounded overflow-hidden">
              <Image src={card.imageUrlSmall} alt={card.name} fill className="object-contain" />
            </div>
          )}
          <div className="min-w-0">
            <p className="font-semibold text-white text-sm truncate">{card.name}</p>
            <p className="text-xs text-gray-400">{card.game}{card.setName && ` · ${card.setName}`}</p>
            {card.rarity && <span className="text-[10px] text-amber-400">{card.rarity}</span>}
            {usdPrice && (
              <p className="text-xs text-gray-500 mt-0.5">Ref. mercado: ${usdPrice.toFixed(2)} USD</p>
            )}
          </div>
        </div>

        <div className="space-y-4">
          <div>
            <label className="label">Preço de venda (R$) *</label>
            <input
              className="input"
              type="number"
              min="0.01"
              step="0.01"
              placeholder="Ex: 29,90"
              value={priceReais}
              onChange={e => setPriceReais(e.target.value)}
            />
            {usdPrice && (
              <p className="text-[11px] text-gray-500 mt-1">
                Sugestão baseada no preço de mercado USD × 6
              </p>
            )}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Quantidade em estoque *</label>
              <input
                className="input"
                type="number"
                min="1"
                value={qty}
                onChange={e => setQty(Math.max(1, parseInt(e.target.value) || 1))}
              />
            </div>
            <div>
              <label className="label">Estoque mínimo</label>
              <input
                className="input"
                type="number"
                min="0"
                value={minStock}
                onChange={e => setMinStock(Math.max(0, parseInt(e.target.value) || 0))}
              />
            </div>
          </div>
        </div>

        <div className="flex gap-3 mt-5">
          <button onClick={onClose} className="btn-secondary flex-1 justify-center">
            Cancelar
          </button>
          <button onClick={handleSave} disabled={saving} className="btn-primary flex-1 justify-center">
            {saving
              ? <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</>
              : <><Check className="w-4 h-4" /> Adicionar</>
            }
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Card item ─────────────────────────────────────────────────────────────────

function CardItem({ card }: { card: CardCache }) {
  const [flipped, setFlipped]     = useState(false)
  const [addModal, setAddModal]   = useState(false)
  const price = card.marketPrices?.market ?? card.marketPrices?.mid
  const isFromCache = new Date(card.cachedAt).getTime() < Date.now() - 60000

  return (
    <>
      {addModal && <AddToStockModal card={card} onClose={() => setAddModal(false)} />}

      <div className="card relative group">
        {/* Botão "Adicionar ao Estoque" — aparece no hover */}
        <button
          onClick={e => { e.stopPropagation(); setAddModal(true) }}
          className="absolute top-2 left-2 z-10 opacity-0 group-hover:opacity-100 transition-opacity bg-brand-600 hover:bg-brand-500 text-white rounded-lg px-2 py-1 text-[10px] font-semibold flex items-center gap-1 shadow-lg"
          title="Adicionar ao estoque de produtos"
        >
          <PackagePlus className="w-3 h-3" /> Estoque
        </button>

        {/* Imagem (clique para virar) */}
        <div
          onClick={() => setFlipped(f => !f)}
          className="relative w-full aspect-[2.5/3.5] rounded-lg overflow-hidden bg-surface-800 mb-3 cursor-pointer hover:scale-[1.02] transition-transform duration-200"
        >
          {card.imageUrlSmall ? (
            <Image
              src={flipped ? (card.imageUrlLarge ?? card.imageUrlSmall) : card.imageUrlSmall}
              alt={card.name}
              fill className="object-contain"
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
    </>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────

export default function CartasPage() {
  const [query, setQuery]       = useState('')
  const [game, setGame]         = useState('Pokemon')
  const [cards, setCards]       = useState<CardCache[]>([])
  const [loading, setLoading]   = useState(false)
  const [searched, setSearched] = useState(false)
  const [totalPages, setTotalPages] = useState(0)
  const [page, setPage]         = useState(1)
  const [ownModal, setOwnModal] = useState(false)

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
      {ownModal && <AddOwnCardModal onClose={() => setOwnModal(false)} />}

      {/* Header */}
      <div className="flex items-start justify-between gap-3 flex-wrap">
        <div>
          <h1 className="text-2xl font-bold text-white">Busca de Cartas TCG</h1>
          <p className="text-gray-400 text-sm mt-0.5">
            Cache-First: MongoDB → API externa · Passe o mouse sobre a carta para adicionar ao estoque
          </p>
        </div>
        <button
          onClick={() => setOwnModal(true)}
          className="btn-primary text-sm shrink-0"
        >
          <PlusCircle className="w-4 h-4" />
          Adicionar Carta Própria
        </button>
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

      {/* Legenda */}
      <div className="flex items-center gap-4 text-xs text-gray-500 flex-wrap">
        <div className="flex items-center gap-1.5">
          <Database className="w-3.5 h-3.5 text-brand-400" />
          <span>Ícone roxo = dado do cache MongoDB</span>
        </div>
        <span>·</span>
        <span>Clique na imagem para ampliar</span>
        <span>·</span>
        <div className="flex items-center gap-1.5">
          <PackagePlus className="w-3.5 h-3.5 text-brand-400" />
          <span>Hover na carta → botão &quot;Estoque&quot; para cadastrar</span>
        </div>
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
          <p className="text-gray-600 text-sm mt-0.5">Passe o mouse sobre a carta para adicionar ao estoque</p>
        </div>
      ) : null}
    </div>
  )
}
