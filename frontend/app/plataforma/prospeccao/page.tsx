'use client'

import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react'
import {
  prospectingApi, ProspectCandidateDto, ProspectingSearchResultDto,
  ProspectingSearchSummaryDto, getErrorMessage,
} from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import toast from 'react-hot-toast'
import { Check, Clock3, Database, Globe, History, Loader2, RefreshCw, Search, Sparkles, UserPlus } from 'lucide-react'
import clsx from 'clsx'

const DIGITAL_PRESENCE_LABEL: Record<string, string> = {
  SemSite: 'Sem site', SiteLegado: 'Site básico/legado', ECommerce: 'Já tem e-commerce',
}
const STATUS_LABEL: Record<string, string> = {
  New: 'Novo', Selected: 'Selecionado', Discarded: 'Descartado', Lead: 'Já é lead',
  Customer: 'Já é cliente', Stale: 'Não apareceu na atualização',
}

function fmtDate(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
}

function CandidateCard({ candidate, categoria, onAdded }: { candidate: ProspectCandidateDto; categoria: string; onAdded: (id: string) => void }) {
  const [data, setData] = useState(candidate)
  const [enriching, setEnriching] = useState(false)
  const [adding, setAdding] = useState(false)
  const [added, setAdded] = useState(candidate.status === 'Lead' || candidate.status === 'Customer')
  const [abordagem, setAbordagem] = useState<string | null>(null)
  const unavailable = data.status === 'Stale' || data.status === 'Discarded'

  async function enrich() {
    setEnriching(true)
    try {
      const { data: result } = await prospectingApi.enrich(data, categoria)
      setData(prev => ({ ...prev, estimatedRevenueRange: result.estimatedRevenueRange }))
      setAbordagem(result.abordagemSugerida)
      toast.success('Análise gerada.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não foi possível enriquecer este candidato.'))
    } finally { setEnriching(false) }
  }

  async function addAsLead() {
    setAdding(true)
    try {
      await prospectingApi.createLead({
        prospectCandidateId: data.id, nome: data.nome, telefone: data.telefone ?? undefined,
        placeId: data.placeId, digitalPresence: data.digitalPresence,
        opportunityScore: data.opportunityScore, estimatedRevenueRange: data.estimatedRevenueRange,
        abordagemSugerida: abordagem ?? undefined,
      })
      setAdded(true)
      onAdded(data.id)
      toast.success(`“${data.nome}” agora é lead.`)
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status
      if (status === 409) {
        setAdded(true); onAdded(data.id); toast('Este estabelecimento já estava nos leads.')
      } else toast.error(getErrorMessage(err, 'Erro ao adicionar como lead.'))
    } finally { setAdding(false) }
  }

  return (
    <article className={clsx('card p-4 space-y-3', unavailable && 'opacity-60')}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="text-white font-semibold truncate">{data.nome}</h3>
          <p className="text-xs text-gray-400 mt-0.5">{data.endereco || 'Endereço não informado na fonte'}</p>
        </div>
        <span className={clsx('text-xs font-bold px-2 py-1 rounded border shrink-0', data.opportunityScore >= 70 ? 'text-accent-green border-accent-green/40' : data.opportunityScore >= 40 ? 'text-amber-400 border-amber-500/40' : 'text-gray-400 border-gray-600')}>
          {data.opportunityScore}
        </span>
      </div>

      <div className="flex flex-wrap items-center gap-2 text-xs">
        <span className="px-2 py-0.5 rounded-full border border-surface-500 text-gray-300">{DIGITAL_PRESENCE_LABEL[data.digitalPresence] ?? data.digitalPresence}</span>
        <span className="px-2 py-0.5 rounded-full border border-surface-500 text-gray-300">{data.estimatedRevenueRange}</span>
        {data.status !== 'New' && <span className="px-2 py-0.5 rounded-full bg-brand-500/10 text-brand-300">{STATUS_LABEL[data.status]}</span>}
      </div>

      <div className="flex flex-wrap gap-3 text-xs text-gray-400">
        {data.telefone && <span>{data.telefone}</span>}
        {data.website && <a href={data.website} target="_blank" rel="noopener noreferrer" className="flex items-center gap-1 text-brand-400 hover:underline"><Globe className="w-3.5 h-3.5" /> Abrir site</a>}
      </div>
      {abordagem && <p className="text-xs text-gray-300 bg-surface-700 rounded-lg p-3 leading-relaxed">{abordagem}</p>}

      <div className="flex gap-2 pt-1">
        <button onClick={enrich} disabled={enriching || added || unavailable} className="btn-secondary text-xs py-1.5 px-3 flex-1 justify-center">
          {enriching ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Sparkles className="w-3.5 h-3.5" />} Analisar
        </button>
        <button onClick={addAsLead} disabled={adding || added || unavailable} className="btn-primary text-xs py-1.5 px-3 flex-1 justify-center disabled:opacity-60">
          {added ? <><Check className="w-3.5 h-3.5" /> Nos leads</> : adding ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <><UserPlus className="w-3.5 h-3.5" /> Criar lead</>}
        </button>
      </div>
    </article>
  )
}

export default function ProspeccaoPage() {
  const [categoria, setCategoria] = useState('')
  const [cidade, setCidade] = useState('')
  const [categories, setCategories] = useState<string[]>([])
  const [history, setHistory] = useState<ProspectingSearchSummaryDto[]>([])
  const [current, setCurrent] = useState<ProspectingSearchResultDto | null>(null)
  const [loading, setLoading] = useState(false)
  const [loadingHistory, setLoadingHistory] = useState(true)

  const loadHistory = useCallback(async () => {
    try { setHistory((await prospectingApi.listSearches()).data) }
    catch { /* histórico não bloqueia uma nova busca */ }
    finally { setLoadingHistory(false) }
  }, [])

  useEffect(() => {
    loadHistory()
    prospectingApi.listCategories().then(r => setCategories(['Todos os negócios', ...r.data])).catch(() => {})
  }, [loadHistory])

  async function executeSearch(forceRefresh: boolean) {
    if (!categoria.trim() || !cidade.trim()) return
    setLoading(true)
    try {
      const result = (await prospectingApi.search(categoria.trim(), cidade.trim(), forceRefresh)).data
      setCurrent(result)
      if (result.candidates.length === 0) toast('Nenhum estabelecimento encontrado com estes filtros.')
      await loadHistory()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao pesquisar estabelecimentos.'))
    } finally { setLoading(false) }
  }

  async function handleSubmit(e: FormEvent) { e.preventDefault(); await executeSearch(false) }

  async function openHistory(item: ProspectingSearchSummaryDto) {
    setLoading(true)
    try {
      const result = (await prospectingApi.getSearch(item.id)).data
      setCategoria(result.categoria); setCidade(result.cidade); setCurrent(result)
    } catch (err) { toast.error(getErrorMessage(err, 'Não foi possível abrir esta pesquisa.')) }
    finally { setLoading(false) }
  }

  function markAdded(candidateId: string) {
    setCurrent(prev => prev ? { ...prev, candidates: prev.candidates.map(c => c.id === candidateId ? { ...c, status: 'Lead' } : c) } : prev)
  }

  const visibleCandidates = useMemo(() => current?.candidates.filter(c => c.status !== 'Stale') ?? [], [current])

  return (
    <div className="space-y-5">
      <PageHeader icon={Search} title="Prospecção" description="Pesquise, retome e qualifique estabelecimentos sem perder o trabalho realizado" />

      <form onSubmit={handleSubmit} className="card p-4 grid gap-3 lg:grid-cols-[1fr_1fr_auto] items-end">
        <label><span className="label">Segmento</span><input list="prospecting-categories" className="input w-full" placeholder="Ex.: restaurante, roupas ou todos" value={categoria} onChange={e => setCategoria(e.target.value)} required /><datalist id="prospecting-categories">{categories.map(c => <option key={c} value={c} />)}</datalist></label>
        <label><span className="label">Cidade e UF</span><input className="input w-full" placeholder="Ex.: Ribeirão Preto, SP" value={cidade} onChange={e => setCidade(e.target.value)} required /></label>
        <button type="submit" disabled={loading} className="btn-primary py-2 px-5 justify-center">{loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Search className="w-4 h-4" />} Pesquisar</button>
      </form>

      <section className="card p-4">
        <div className="flex items-center gap-2 mb-3"><History className="w-4 h-4 text-brand-400" /><h2 className="text-sm font-bold text-white">Pesquisas recentes</h2></div>
        {loadingHistory ? <Loader2 className="w-4 h-4 animate-spin text-brand-400" /> : history.length === 0 ? <p className="text-sm text-gray-500">As pesquisas salvas aparecerão aqui.</p> : (
          <div className="flex gap-2 overflow-x-auto pb-1">{history.map(item => <button key={item.id} onClick={() => openHistory(item)} className={clsx('min-w-[210px] rounded-xl border p-3 text-left transition-colors', current?.id === item.id ? 'border-brand-500 bg-brand-500/10' : 'border-surface-600 hover:border-surface-500')}><span className="block text-sm font-semibold text-white truncate">{item.categoria}</span><span className="block text-xs text-gray-400 truncate">{item.cidade}</span><span className="mt-2 flex items-center gap-1 text-[11px] text-gray-500"><Clock3 className="w-3 h-3" />{fmtDate(item.refreshedAt)} · {item.resultCount}</span></button>)}</div>
        )}
      </section>

      {current && (
        <section className="space-y-4">
          <div className="card p-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div><div className="flex flex-wrap items-center gap-2"><h2 className="font-bold text-white">{current.categoria} em {current.cidade}</h2><span className="inline-flex items-center gap-1 rounded-full bg-surface-700 px-2 py-0.5 text-[11px] text-gray-300"><Database className="w-3 h-3" />{current.fromCache ? 'Resultado salvo' : 'Fonte atualizada'}</span></div><p className="mt-1 text-xs text-gray-500">{visibleCandidates.length} estabelecimentos · atualizado em {fmtDate(current.refreshedAt)} · dados © <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer" className="text-brand-400 hover:underline">OpenStreetMap</a></p></div>
            <button onClick={() => executeSearch(true)} disabled={loading} className="btn-secondary text-xs py-2 px-3 justify-center"><RefreshCw className={clsx('w-3.5 h-3.5', loading && 'animate-spin')} /> Atualizar fonte</button>
          </div>
          {visibleCandidates.length === 0 ? <div className="card py-14 text-center text-sm text-gray-400">Nenhum candidato ativo nesta pesquisa.</div> : <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">{visibleCandidates.map(c => <CandidateCard key={c.id} candidate={c} categoria={current.categoria} onAdded={markAdded} />)}</div>}
        </section>
      )}
    </div>
  )
}
