'use client'

import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react'
import {
  prospectingApi, ProspectCandidateDto, ProspectingSearchResultDto,
  ProspectingSearchSummaryDto, ProspectingCampaignDto, getErrorMessage,
} from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import toast from 'react-hot-toast'
import { Ban, Bot, Check, ChevronDown, Clock3, Database, Globe, History, Loader2, Pause, Play, Plus, RefreshCw, Search, Sparkles, UserPlus } from 'lucide-react'
import clsx from 'clsx'

const DIGITAL_PRESENCE_LABEL: Record<string, string> = {
  SemSite: 'Sem site', SiteLegado: 'Site básico/legado', ECommerce: 'Já tem e-commerce',
}
const STATUS_LABEL: Record<string, string> = {
  New: 'Novo', Selected: 'Selecionado', Discarded: 'Descartado', Lead: 'Já é lead',
  Customer: 'Já é cliente', Stale: 'Não apareceu na atualização',
  Suppressed: 'Não prospectar',
}

function fmtDate(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
}

function CandidateCard({ candidate, categoria, onAdded, onSuppressed }: { candidate: ProspectCandidateDto; categoria: string; onAdded: (id: string) => void; onSuppressed: (id: string) => void }) {
  const [data, setData] = useState(candidate)
  const [enriching, setEnriching] = useState(false)
  const [adding, setAdding] = useState(false)
  const [suppressing, setSuppressing] = useState(false)
  const [added, setAdded] = useState(candidate.status === 'Lead' || candidate.status === 'Customer')
  const [abordagem, setAbordagem] = useState<string | null>(candidate.suggestedApproach)
  const unavailable = data.status === 'Stale' || data.status === 'Discarded' || data.status === 'Suppressed'

  async function enrich() {
    setEnriching(true)
    try {
      const { data: result } = await prospectingApi.enrich(data, categoria)
      setData(prev => ({
        ...prev, estimatedRevenueRange: result.estimatedRevenueRange,
        suggestedApproach: result.abordagemSugerida, enrichmentStatus: 'Updated',
        lastEnrichedAt: new Date().toISOString(),
        enrichmentSource: prev.enrichmentSource?.includes('Gemini') ? prev.enrichmentSource : `${prev.enrichmentSource ? `${prev.enrichmentSource};` : ''}Gemini`,
      }))
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

  async function suppress() {
    if (!window.confirm(`Bloquear “${data.nome}” de todas as campanhas futuras?`)) return
    setSuppressing(true)
    try {
      await prospectingApi.suppressCandidate(data.id)
      setData(prev => ({ ...prev, status: 'Suppressed' }))
      onSuppressed(data.id)
      toast.success('Candidato incluído na lista de oposição.')
    } catch (err) { toast.error(getErrorMessage(err, 'Não foi possível bloquear este candidato.')) }
    finally { setSuppressing(false) }
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
      {data.lastEnrichedAt && <p className="text-[11px] text-gray-500">Enriquecido em {fmtDate(data.lastEnrichedAt)} · {data.enrichmentSource || 'fonte não informada'}{data.enrichmentConfidence != null ? ` · confiança ${data.enrichmentConfidence}%` : ''}</p>}
      {(data.recentObservations?.length ?? 0) > 0 && <details className="text-[11px] text-gray-400"><summary className="cursor-pointer text-brand-400">Histórico das informações ({data.recentObservations.length})</summary><div className="mt-2 space-y-1">{data.recentObservations.map((o, index) => <p key={`${o.fieldName}-${o.observedAt}-${index}`}><strong>{o.fieldName}</strong>: {o.previousValue || 'vazio'} → {o.observedValue || 'vazio'} · {o.source} · {o.confidence}%</p>)}</div></details>}
      {abordagem && <p className="text-xs text-gray-300 bg-surface-700 rounded-lg p-3 leading-relaxed">{abordagem}</p>}

      <div className="flex gap-2 pt-1">
        <button onClick={suppress} disabled={suppressing || added || unavailable} className="btn-secondary p-2 justify-center" title="Não prospectar novamente">
          {suppressing ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Ban className="w-3.5 h-3.5" />}
        </button>
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
  const [campaigns, setCampaigns] = useState<ProspectingCampaignDto[]>([])
  const [reviewQueue, setReviewQueue] = useState<ProspectCandidateDto[]>([])
  const [current, setCurrent] = useState<ProspectingSearchResultDto | null>(null)
  const [loading, setLoading] = useState(false)
  const [loadingHistory, setLoadingHistory] = useState(true)
  const [savingCampaign, setSavingCampaign] = useState(false)
  const [manualOpen, setManualOpen] = useState(true)
  const [botOpen, setBotOpen] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(true)
  const [queueOpen, setQueueOpen] = useState(false)
  const [campaignForm, setCampaignForm] = useState({ name: '', categoria: '', cidade: '', intervalHours: 168, maxCandidatesPerRun: 200, dailyRunBudget: 1, maxRetryAttempts: 3 })

  const loadHistory = useCallback(async () => {
    try { setHistory((await prospectingApi.listSearches()).data) }
    catch { /* histórico não bloqueia uma nova busca */ }
    finally { setLoadingHistory(false) }
  }, [])

  const loadAutomation = useCallback(async () => {
    try {
      const [campaignResult, queueResult] = await Promise.all([
        prospectingApi.listCampaigns(), prospectingApi.reviewQueue(60),
      ])
      setCampaigns(campaignResult.data)
      setReviewQueue(queueResult.data)
    } catch { /* automação não bloqueia a pesquisa manual */ }
  }, [])

  useEffect(() => {
    loadHistory()
    loadAutomation()
    prospectingApi.listCategories().then(r => setCategories(['Todos os negócios', ...r.data])).catch(() => {})
    const refresh = window.setInterval(loadAutomation, 30_000)
    return () => window.clearInterval(refresh)
  }, [loadAutomation, loadHistory])

  async function createCampaign(e: FormEvent) {
    e.preventDefault(); setSavingCampaign(true)
    try {
      await prospectingApi.createCampaign(campaignForm)
      setCampaignForm(prev => ({ ...prev, name: '' }))
      await loadAutomation(); toast.success('Campanha criada e colocada na fila do bot.')
    } catch (err) { toast.error(getErrorMessage(err, 'Não foi possível criar a campanha.')) }
    finally { setSavingCampaign(false) }
  }

  async function runCampaign(id: string) {
    try { await prospectingApi.runCampaign(id); await loadAutomation(); toast.success('Execução colocada na fila.') }
    catch (err) { toast.error(getErrorMessage(err, 'Não foi possível iniciar a campanha.')) }
  }

  async function toggleCampaign(campaign: ProspectingCampaignDto) {
    try { await prospectingApi.setCampaignActive(campaign.id, campaign.status !== 'Active'); await loadAutomation() }
    catch (err) { toast.error(getErrorMessage(err, 'Não foi possível alterar a campanha.')) }
  }

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
    setManualOpen(true)
    setLoading(true)
    try {
      const result = (await prospectingApi.getSearch(item.id)).data
      setCategoria(result.categoria); setCidade(result.cidade); setCurrent(result)
    } catch (err) { toast.error(getErrorMessage(err, 'Não foi possível abrir esta pesquisa.')) }
    finally { setLoading(false) }
  }

  function markAdded(candidateId: string) {
    setCurrent(prev => prev ? { ...prev, candidates: prev.candidates.map(c => c.id === candidateId ? { ...c, status: 'Lead' } : c) } : prev)
    setReviewQueue(prev => prev.filter(c => c.id !== candidateId))
  }

  function markSuppressed(candidateId: string) {
    setCurrent(prev => prev ? { ...prev, candidates: prev.candidates.map(c => c.id === candidateId ? { ...c, status: 'Suppressed' } : c) } : prev)
    setReviewQueue(prev => prev.filter(c => c.id !== candidateId))
  }

  const visibleCandidates = useMemo(() => current?.candidates.filter(c => c.status !== 'Stale' && c.status !== 'Suppressed') ?? [], [current])

  return (
    <div className="space-y-5">
      <PageHeader icon={Search} title="Prospecção" description="Pesquise, retome e qualifique estabelecimentos sem perder o trabalho realizado" />

      <section className="card overflow-hidden">
        <button type="button" onClick={() => setManualOpen(value => !value)} aria-expanded={manualOpen} className="flex w-full items-center justify-between gap-4 p-4 text-left">
          <span className="flex items-center gap-3"><Search className="h-5 w-5 text-brand-400" /><span><strong className="block text-sm text-white">Pesquisa manual</strong><span className="text-xs text-gray-500">Nova pesquisa, histórico salvo e resultados</span></span></span>
          <span className="flex items-center gap-2"><span className="rounded-full bg-surface-700 px-2 py-1 text-[11px] text-gray-300">{history.length} salvas</span><ChevronDown className={clsx('h-4 w-4 text-gray-400 transition-transform', manualOpen && 'rotate-180')} /></span>
        </button>
        {manualOpen && <div className="space-y-4 border-t border-surface-700 p-4">
          <form onSubmit={handleSubmit} className="grid gap-3 lg:grid-cols-[1fr_1fr_auto] items-end">
            <label><span className="label">Segmento</span><input list="prospecting-categories" className="input w-full" placeholder="Ex.: restaurante, roupas ou todos" value={categoria} onChange={e => setCategoria(e.target.value)} required /><datalist id="prospecting-categories">{categories.map(c => <option key={c} value={c} />)}</datalist></label>
            <label><span className="label">Cidade e UF</span><input className="input w-full" placeholder="Ex.: Ribeirão Preto, SP" value={cidade} onChange={e => setCidade(e.target.value)} required /></label>
            <button type="submit" disabled={loading} className="btn-primary py-2 px-5 justify-center">{loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Search className="w-4 h-4" />} Pesquisar</button>
          </form>

          <div className="rounded-xl border border-surface-600">
            <button type="button" onClick={() => setHistoryOpen(value => !value)} aria-expanded={historyOpen} className="flex w-full items-center justify-between gap-3 p-3 text-left">
              <span className="flex items-center gap-2"><History className="h-4 w-4 text-brand-400" /><strong className="text-sm text-white">Pesquisas recentes</strong></span>
              <ChevronDown className={clsx('h-4 w-4 text-gray-400 transition-transform', historyOpen && 'rotate-180')} />
            </button>
            {historyOpen && <div className="border-t border-surface-600 p-3">{loadingHistory ? <Loader2 className="w-4 h-4 animate-spin text-brand-400" /> : history.length === 0 ? <p className="text-sm text-gray-500">As pesquisas salvas aparecerão aqui.</p> : (
              <div className="flex gap-2 overflow-x-auto pb-1">{history.map(item => <button key={item.id} onClick={() => openHistory(item)} className={clsx('min-w-[210px] rounded-xl border p-3 text-left transition-colors', current?.id === item.id ? 'border-brand-500 bg-brand-500/10' : 'border-surface-600 hover:border-surface-500')}><span className="block text-sm font-semibold text-white truncate">{item.categoria}</span><span className="block text-xs text-gray-400 truncate">{item.cidade}</span><span className="mt-2 flex items-center gap-1 text-[11px] text-gray-500"><Clock3 className="w-3 h-3" />{fmtDate(item.refreshedAt)} · {item.resultCount}</span></button>)}</div>
            )}</div>}
          </div>

          {current && <section className="space-y-4">
          <div className="card p-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div><div className="flex flex-wrap items-center gap-2"><h2 className="font-bold text-white">{current.categoria} em {current.cidade}</h2><span className="inline-flex items-center gap-1 rounded-full bg-surface-700 px-2 py-0.5 text-[11px] text-gray-300"><Database className="w-3 h-3" />{current.fromCache ? 'Resultado salvo' : 'Fonte atualizada'}</span></div><p className="mt-1 text-xs text-gray-500">{visibleCandidates.length} estabelecimentos · atualizado em {fmtDate(current.refreshedAt)} · dados © <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer" className="text-brand-400 hover:underline">OpenStreetMap</a></p></div>
            <button onClick={() => executeSearch(true)} disabled={loading} className="btn-secondary text-xs py-2 px-3 justify-center"><RefreshCw className={clsx('w-3.5 h-3.5', loading && 'animate-spin')} /> Atualizar fonte</button>
          </div>
          {visibleCandidates.length === 0 ? <div className="card py-14 text-center text-sm text-gray-400">Nenhum candidato ativo nesta pesquisa.</div> : <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">{visibleCandidates.map(c => <CandidateCard key={c.id} candidate={c} categoria={current.categoria} onAdded={markAdded} onSuppressed={markSuppressed} />)}</div>}
          </section>}
        </div>}
      </section>

      <section className="card overflow-hidden">
        <button type="button" onClick={() => setBotOpen(value => !value)} aria-expanded={botOpen} className="flex w-full items-center justify-between gap-4 p-4 text-left">
          <span className="flex items-center gap-3"><Bot className="h-5 w-5 text-brand-400" /><span><strong className="block text-sm text-white">Bot de captação</strong><span className="text-xs text-gray-500">Campanhas automáticas e fila de revisão</span></span></span>
          <span className="flex items-center gap-2"><span className="rounded-full bg-brand-500/10 px-2 py-1 text-[11px] text-brand-300">{campaigns.length} campanhas · {reviewQueue.length} pendentes</span><ChevronDown className={clsx('h-4 w-4 text-gray-400 transition-transform', botOpen && 'rotate-180')} /></span>
        </button>
        {botOpen && <div className="space-y-5 border-t border-surface-700 p-4">
          <p className="text-xs text-gray-500">Pesquisa e atualiza candidatos; nenhum contato ou lead é criado sem sua aprovação.</p>
          <form onSubmit={createCampaign} className="grid gap-3 lg:grid-cols-[1.2fr_1fr_1fr_120px_100px_auto] items-end">
            <label><span className="label">Nome da campanha</span><input className="input w-full" value={campaignForm.name} onChange={e => setCampaignForm(p => ({ ...p, name: e.target.value }))} placeholder="Comércio regional" required /></label>
            <label><span className="label">Segmento</span><input list="prospecting-categories" className="input w-full" value={campaignForm.categoria} onChange={e => setCampaignForm(p => ({ ...p, categoria: e.target.value }))} placeholder="Restaurante" required /></label>
            <label><span className="label">Cidade e UF</span><input className="input w-full" value={campaignForm.cidade} onChange={e => setCampaignForm(p => ({ ...p, cidade: e.target.value }))} placeholder="Ribeirão Preto, SP" required /></label>
            <label><span className="label">Frequência</span><select className="input w-full" value={campaignForm.intervalHours} onChange={e => setCampaignForm(p => ({ ...p, intervalHours: Number(e.target.value) }))}><option value={24}>Diária</option><option value={72}>3 dias</option><option value={168}>Semanal</option><option value={720}>Mensal</option></select></label>
            <label><span className="label">Limite/dia</span><input type="number" min={1} max={24} className="input w-full" value={campaignForm.dailyRunBudget} onChange={e => setCampaignForm(p => ({ ...p, dailyRunBudget: Number(e.target.value) }))} /></label>
            <button disabled={savingCampaign} className="btn-primary py-2 px-4 justify-center">{savingCampaign ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />} Criar</button>
          </form>
          {campaigns.length > 0 && <div className="grid gap-3 lg:grid-cols-2">{campaigns.map(campaign => {
            const latest = campaign.recentRuns[0]
            return <article key={campaign.id} className="rounded-xl border border-surface-600 p-3 flex items-center justify-between gap-3"><div className="min-w-0"><div className="flex items-center gap-2"><strong className="text-sm text-white truncate">{campaign.name}</strong><span className={clsx('text-[10px] rounded-full px-2 py-0.5', campaign.status === 'Active' ? 'bg-accent-green/10 text-accent-green' : 'bg-surface-600 text-gray-400')}>{campaign.status === 'Active' ? 'Ativa' : 'Pausada'}</span></div><p className="text-xs text-gray-400 truncate">{campaign.categoria} · {campaign.cidade}</p><p className={clsx('text-[11px] mt-1 truncate', latest?.status === 'Failed' ? 'text-red-400' : 'text-gray-500')}>{latest ? latest.status === 'Running' ? 'Executando' : latest.status === 'Queued' ? 'Na fila' : latest.status === 'Failed' ? `Falhou: ${latest.error || 'erro não informado'}` : `${latest.discoveredCount} priorizados · ${latest.newCount} novos` : 'Primeira execução aguardando o worker'}</p></div><div className="flex gap-1"><button type="button" onClick={() => runCampaign(campaign.id)} className="btn-secondary p-2" title="Executar agora"><Play className="w-3.5 h-3.5" /></button><button type="button" onClick={() => toggleCampaign(campaign)} className="btn-secondary p-2" title={campaign.status === 'Active' ? 'Pausar' : 'Ativar'}>{campaign.status === 'Active' ? <Pause className="w-3.5 h-3.5" /> : <Play className="w-3.5 h-3.5" />}</button></div></article>
          })}</div>}

          {reviewQueue.length > 0 && <section className="rounded-xl border border-surface-600">
            <button type="button" onClick={() => setQueueOpen(value => !value)} aria-expanded={queueOpen} className="flex w-full items-center justify-between gap-3 p-3 text-left"><span><strong className="text-sm text-white">Fila de captação</strong><span className="ml-2 text-xs text-gray-500">Candidatos aguardando revisão</span></span><span className="flex items-center gap-2"><span className="rounded-full bg-brand-500/10 px-2 py-1 text-xs text-brand-300">{reviewQueue.length}</span><ChevronDown className={clsx('h-4 w-4 text-gray-400 transition-transform', queueOpen && 'rotate-180')} /></span></button>
            {queueOpen && <div className="grid gap-4 border-t border-surface-600 p-3 sm:grid-cols-2 xl:grid-cols-3">{reviewQueue.map(c => <CandidateCard key={`queue-${c.id}`} candidate={c} categoria="prospecção automática" onAdded={markAdded} onSuppressed={markSuppressed} />)}</div>}
          </section>}
        </div>}
      </section>
    </div>
  )
}
