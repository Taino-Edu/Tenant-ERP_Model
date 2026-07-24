'use client'
import { useState } from 'react'
import { prospectingApi, ProspectCandidateDto, getErrorMessage } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import toast from 'react-hot-toast'
import { Search, Loader2, Globe, Sparkles, UserPlus, Check } from 'lucide-react'
import clsx from 'clsx'

function scoreColor(score: number): string {
  if (score >= 70) return 'text-accent-green border-accent-green/40'
  if (score >= 40) return 'text-amber-400 border-amber-500/40'
  return 'text-gray-400 border-gray-600'
}

const DIGITAL_PRESENCE_LABEL: Record<string, string> = {
  SemSite:    'Sem site',
  SiteLegado: 'Site desatualizado',
  ECommerce:  'Já tem e-commerce',
}

function CandidateCard({ candidate, categoria, onAdded }: { candidate: ProspectCandidateDto; categoria: string; onAdded: () => void }) {
  const [data, setData]           = useState(candidate)
  const [enriching, setEnriching] = useState(false)
  const [adding, setAdding]       = useState(false)
  const [added, setAdded]         = useState(false)

  async function enrich() {
    setEnriching(true)
    try {
      const { data: result } = await prospectingApi.enrich(data, categoria)
      setData(prev => ({ ...prev, estimatedRevenueRange: result.estimatedRevenueRange }))
      setAbordagem(result.abordagemSugerida)
      toast.success('Enriquecido com IA.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao enriquecer com IA — confira se a chave de prospecção está configurada.'))
    } finally {
      setEnriching(false)
    }
  }

  const [abordagem, setAbordagem] = useState<string | null>(null)

  async function addAsLead() {
    setAdding(true)
    try {
      await prospectingApi.createLead({
        nome: data.nome,
        telefone: data.telefone ?? undefined,
        placeId: data.placeId,
        digitalPresence: data.digitalPresence,
        opportunityScore: data.opportunityScore,
        estimatedRevenueRange: data.estimatedRevenueRange,
        abordagemSugerida: abordagem ?? undefined,
      })
      setAdded(true)
      toast.success(`"${data.nome}" adicionado como lead.`)
      onAdded()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao adicionar como lead.'))
    } finally {
      setAdding(false)
    }
  }

  return (
    <div className="card p-4 space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-white font-semibold truncate">{data.nome}</p>
          {data.endereco && <p className="text-xs text-gray-400 mt-0.5">{data.endereco}</p>}
          <div className="flex items-center gap-3 mt-1.5 text-xs text-gray-400">
            {data.website && (
              <a href={data.website} target="_blank" rel="noopener noreferrer"
                className="flex items-center gap-1 text-brand-400 hover:underline">
                <Globe className="w-3.5 h-3.5" /> Site
              </a>
            )}
          </div>
        </div>
        <span className={clsx('text-xs font-bold px-2 py-1 rounded border shrink-0', scoreColor(data.opportunityScore))}>
          {data.opportunityScore}
        </span>
      </div>

      <div className="flex flex-wrap items-center gap-2 text-xs">
        <span className="px-2 py-0.5 rounded-full border border-surface-500 text-gray-300">
          {DIGITAL_PRESENCE_LABEL[data.digitalPresence] ?? data.digitalPresence}
        </span>
        <span className="px-2 py-0.5 rounded-full border border-surface-500 text-gray-300">
          {data.estimatedRevenueRange}
        </span>
      </div>

      {abordagem && (
        <p className="text-xs text-gray-400 bg-surface-700 rounded-lg p-3">{abordagem}</p>
      )}

      <div className="flex gap-2 pt-1">
        <button onClick={enrich} disabled={enriching || added} className="btn-secondary text-xs py-1.5 px-3 flex-1 justify-center">
          {enriching ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Sparkles className="w-3.5 h-3.5" />}
          Enriquecer com IA
        </button>
        <button
          onClick={addAsLead} disabled={adding || added}
          className="btn-primary text-xs py-1.5 px-3 flex-1 justify-center disabled:opacity-60"
        >
          {added
            ? <><Check className="w-3.5 h-3.5" /> Adicionado</>
            : adding
              ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
              : <><UserPlus className="w-3.5 h-3.5" /> Adicionar como Lead</>}
        </button>
      </div>
    </div>
  )
}

export default function ProspeccaoPage() {
  const [categoria, setCategoria] = useState('')
  const [cidade, setCidade]       = useState('')
  const [loading, setLoading]     = useState(false)
  const [results, setResults]     = useState<ProspectCandidateDto[] | null>(null)
  const [addedCount, setAddedCount] = useState(0)

  async function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    setResults(null)
    try {
      const { data } = await prospectingApi.search(categoria.trim(), cidade.trim())
      setResults(data)
      if (data.length === 0) toast('Nenhum resultado encontrado — tenta outra categoria/cidade.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao buscar — tenta de novo em instantes.'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="space-y-5">
      <PageHeader
        icon={Search}
        title="Prospecção"
        description="Busca possíveis clientes por categoria e cidade — não gasta IA sozinho, só quando você pedir"
      />

      <form onSubmit={handleSearch} className="card p-4 flex flex-wrap gap-3 items-end">
        <div className="flex-1 min-w-[200px]">
          <label className="label">Categoria</label>
          <input
            className="input" placeholder="Ex: loja de roupas" value={categoria}
            onChange={e => setCategoria(e.target.value)} required
          />
        </div>
        <div className="flex-1 min-w-[200px]">
          <label className="label">Cidade</label>
          <input
            className="input" placeholder="Ex: Ribeirão Preto, SP" value={cidade}
            onChange={e => setCidade(e.target.value)} required
          />
        </div>
        <button type="submit" disabled={loading} className="btn-primary py-2 px-4">
          {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Search className="w-4 h-4" />}
          Buscar
        </button>
      </form>

      {results && results.length > 0 && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {results.map(c => (
            <CandidateCard key={c.placeId} candidate={c} categoria={categoria} onAdded={() => setAddedCount(n => n + 1)} />
          ))}
        </div>
      )}

      {addedCount > 0 && (
        <p className="text-sm text-gray-400">
          {addedCount} lead{addedCount > 1 ? 's' : ''} adicionado{addedCount > 1 ? 's' : ''} nessa sessão —
          confira na aba <span className="text-brand-400">Leads</span>.
        </p>
      )}
    </div>
  )
}
