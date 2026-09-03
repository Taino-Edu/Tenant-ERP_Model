'use client'
import { useEffect, useState, useCallback, useMemo } from 'react'
import { platformApi, LeadDto, LeadStatus, LeadDigitalPresence, CrmAnalyticsDto, CrmAssigneeDto, CrmOpportunityStage, CrmTaskDto, getErrorMessage } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import CreateTenantModal from '@/components/plataforma/CreateTenantModal'
import CrmWorkspaceModal from '@/components/plataforma/CrmWorkspaceModal'
import StatusPillSelect from '@/components/admin/StatusPillSelect'
import SomenteLeitura from '@/components/plataforma/SomenteLeitura'
import { usePlatformPermissions } from '@/hooks/usePlatformPermissions'
import toast from 'react-hot-toast'
import clsx from 'clsx'
import { UserPlus, Loader2, MessageCircle, MapPin, Search, Sparkles, Target, UserCheck, Users, Workflow, Columns3, List, CalendarCheck, Check, Clock3, AlertTriangle, ChartNoAxesCombined, ShieldCheck } from 'lucide-react'

const STATUS_OPTIONS: LeadStatus[] = ['Novo', 'Contatado', 'Convertido', 'Perdido']

const DIGITAL_PRESENCE_OPTIONS: { value: LeadDigitalPresence; label: string }[] = [
  { value: 'SemSite',    label: 'Sem site' },
  { value: 'SiteLegado', label: 'Site desatualizado' },
  { value: 'ECommerce',  label: 'Já tem e-commerce' },
]

function scoreColor(score: number | null): string {
  if (score === null) return 'text-gray-500 border-gray-600'
  if (score >= 70) return 'text-accent-green border-accent-green/40'
  if (score >= 40) return 'text-amber-400 border-amber-500/40'
  return 'text-gray-400 border-gray-600'
}

const STATUS_STYLES: Record<LeadStatus, string> = {
  Novo:       'bg-brand-500/10 text-brand-300 border-brand-500/30',
  Contatado:  'bg-amber-500/10 text-amber-400 border-amber-500/30',
  Convertido: 'bg-accent-green/10 text-accent-green border-accent-green/30',
  Perdido:    'bg-gray-500/10 text-gray-400 border-gray-500/30',
}

type CrmView = 'list' | 'kanban' | 'tasks' | 'analytics'
type TaskFilter = 'all' | 'overdue' | 'today' | 'future'
type OrigemFilter = '' | 'site' | 'prospeccao'

/** Valor de `origem` que o POST público grava (LeadsController). Os dois
 * formulários do site — institucional e Programa de Afiliados — caem aqui;
 * a Prospecção grava "prospeccao". */
const ORIGEM_SITE = 'landing'

/** Prazo de resposta para quem escreveu no site. Não é SLA contratado: é o
 * ponto em que a página passa a cobrar, porque um contato que a pessoa mandou
 * por vontade própria e ficou um dia inteiro parado em "Novo" já é um cliente
 * perdido — diferente do lead de prospecção, que ninguém pediu. */
const HORAS_PARA_RESPONDER = 24

/** Quem chegou pelo formulário do site (esperando retorno) × quem a Prospecção
 * garimpou. A distinção é o que separa "atrasado" de "fila de trabalho". */
function veioDoSite(lead: LeadDto): boolean {
  return lead.origem === ORIGEM_SITE
}

function horasDesde(iso: string): number {
  return (Date.now() - new Date(iso).getTime()) / 3_600_000
}

/** Contato do site que ninguém respondeu ainda. `canContact` entra na conta
 * porque quem se opôs ao tratamento não pode ser cobrado de volta — cobrar
 * resposta ali seria empurrar o time para uma ligação que a LGPD barra. */
function aguardandoResposta(lead: LeadDto): boolean {
  return veioDoSite(lead) && lead.status === 'Novo' && lead.canContact
}

function esperaHumana(horas: number): string {
  if (horas < 1) return 'menos de 1h'
  if (horas < 24) return `${Math.floor(horas)}h`
  const dias = Math.floor(horas / 24)
  return dias === 1 ? '1 dia' : `${dias} dias`
}

const KANBAN_STAGES: { value: CrmOpportunityStage | null; label: string; probability: number; color: string }[] = [
  { value: null, label: 'Sem oportunidade', probability: 20, color: 'border-gray-600' },
  { value: 'Qualificacao', label: 'Qualificação', probability: 20, color: 'border-sky-500/50' },
  { value: 'Diagnostico', label: 'Diagnóstico', probability: 35, color: 'border-cyan-500/50' },
  { value: 'Proposta', label: 'Proposta', probability: 55, color: 'border-amber-500/50' },
  { value: 'Negociacao', label: 'Negociação', probability: 75, color: 'border-orange-500/50' },
  { value: 'Ganho', label: 'Ganho', probability: 100, color: 'border-accent-green/50' },
  { value: 'Perdido', label: 'Perdido', probability: 0, color: 'border-red-500/50' },
]

function fmtDateTime(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

/** Monta o link do WhatsApp a partir do telefone salvo — assume Brasil (55)
 * quando o número não já vem com código de país (10/11 dígitos = DDD+número). */
function whatsAppLink(telefone: string): string {
  const digits = telefone.replace(/\D/g, '')
  const withCountry = digits.length <= 11 ? `55${digits}` : digits
  return `https://wa.me/${withCountry}`
}

/** O campo "Place ID" guarda dois formatos diferentes dependendo da origem do
 * lead: "node/123"/"way/123"/"relation/123" quando veio da Prospecção (OSM —
 * ver ProspectingService.cs), ou um Place ID do Google (ex:
 * "ChIJN1t_tDeuEmsRUsoyG83frY4") quando digitado manualmente pra um lead
 * antigo/de outra origem. São sistemas de ID de provedores diferentes — usar
 * a URL do Google pra um ID do OSM (ou vice-versa) nunca acha o lugar. */
function mapLink(placeId: string): string {
  const osmMatch = placeId.match(/^(node|way|relation)\/(\d+)$/)
  return osmMatch
    ? `https://www.openstreetmap.org/${osmMatch[1]}/${osmMatch[2]}`
    : `https://www.google.com/maps/place/?q=place_id:${encodeURIComponent(placeId)}`
}

/** Linha de lead — renderiza como `<tr>` (desktop) ou como card (celular).
 *
 * Esta tabela não usa o DataTable como as outras porque ela não EXIBE dados:
 * ela edita. Cada linha tem estado próprio (anotações, placeId, saving,
 * enriching) e seis campos editáveis inline. As funções `cell` do DataTable são
 * chamadas durante a renderização dele, então não podem hospedar hooks — o
 * estado precisa morar num componente por linha, que é este.
 *
 * Os dois layouts saem do MESMO componente, com os blocos montados uma vez só
 * acima do `return`: só a moldura muda. Assim os handlers (updateStatus,
 * saveNotas, enrichLead…) não têm como divergir entre celular e desktop. */
function LeadRow({ lead, onChanged, onConvert, onOpenCrm, layout = 'row', podeEditar = true }: { lead: LeadDto; onChanged: () => void; onConvert: (lead: LeadDto) => void; onOpenCrm: (lead: LeadDto) => void; layout?: 'row' | 'card'; podeEditar?: boolean }) {
  const [notas, setNotas] = useState(lead.notas ?? '')
  const [placeId, setPlaceId] = useState(lead.placeId ?? '')
  const [saving, setSaving] = useState(false)
  const [enriching, setEnriching] = useState(false)

  // Base pra qualquer PATCH parcial — sempre reenvia os campos de oportunidade
  // atuais além do que está sendo alterado, senão a API (que substitui o valor
  // inteiro, não faz merge) apagaria o que já estava salvo.
  function baseUpdate() {
    return {
      status: lead.status, notas: lead.notas,
      digitalPresence: lead.digitalPresence, opportunityScore: lead.opportunityScore, placeId: lead.placeId,
    }
  }

  async function updateStatus(status: LeadStatus) {
    setSaving(true)
    try {
      await platformApi.updateLead(lead.id, { ...baseUpdate(), status })
      toast.success('Lead atualizado.')
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao atualizar lead.'))
    } finally {
      setSaving(false)
    }
  }

  async function saveNotas() {
    if (notas === (lead.notas ?? '')) return
    setSaving(true)
    try {
      await platformApi.updateLead(lead.id, { ...baseUpdate(), notas })
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao salvar anotação.'))
    } finally {
      setSaving(false)
    }
  }

  async function updateDigitalPresence(digitalPresence: LeadDigitalPresence | '') {
    setSaving(true)
    try {
      await platformApi.updateLead(lead.id, { ...baseUpdate(), digitalPresence: digitalPresence || null })
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao atualizar presença digital.'))
    } finally {
      setSaving(false)
    }
  }

  async function updateScore(value: string) {
    const opportunityScore = value === '' ? null : Math.max(0, Math.min(100, Number(value)))
    setSaving(true)
    try {
      await platformApi.updateLead(lead.id, { ...baseUpdate(), opportunityScore })
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao atualizar pontuação.'))
    } finally {
      setSaving(false)
    }
  }

  async function savePlaceId() {
    if (placeId === (lead.placeId ?? '')) return
    setSaving(true)
    try {
      await platformApi.updateLead(lead.id, { ...baseUpdate(), placeId: placeId || null })
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao salvar Place ID.'))
    } finally {
      setSaving(false)
    }
  }

  async function enrichLead() {
    setEnriching(true)
    try {
      await platformApi.enrichLead(lead.id)
      toast.success('Lead enriquecido e abordagem salva no CRM.')
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Não foi possível enriquecer este lead.'))
    } finally {
      setEnriching(false)
    }
  }

  // Larguras fixas (w-36 / w-64) existem pra manter as colunas alinhadas entre
  // as linhas da tabela. No card não há coluna nenhuma pra alinhar, e elas só
  // deixariam metade da tela vazia — ali tudo ocupa a largura disponível.
  const card = layout === 'card'
  const wQualif = card ? 'w-full' : 'w-36'
  const wNotas  = card ? 'w-full' : 'w-64'

  // Espera só existe para quem veio do site: a linha precisa dizer, sem abrir
  // nada, se este contato está pendurado esperando alguém responder.
  const esperando = aguardandoResposta(lead)
  const horasEsperando = esperando ? horasDesde(lead.createdAt) : 0
  const atrasado = horasEsperando >= HORAS_PARA_RESPONDER

  const contato = (
    <>
      <div className="flex flex-wrap items-center gap-1.5">
        <p className="text-white font-medium">{lead.nome}</p>
        <span className={clsx('rounded border px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wide',
          veioDoSite(lead) ? 'border-brand-500/30 bg-brand-500/10 text-brand-300' : 'border-surface-600 bg-surface-700 text-gray-400')}>
          {veioDoSite(lead) ? 'Do site' : 'Prospecção'}
        </span>
        {esperando && (
          <span className={clsx('flex items-center gap-1 rounded border px-1.5 py-0.5 text-[10px] font-bold',
            atrasado ? 'border-red-500/40 bg-red-500/10 text-red-300' : 'border-amber-500/40 bg-amber-500/10 text-amber-300')}>
            <AlertTriangle className="h-3 w-3" />Sem resposta há {esperaHumana(horasEsperando)}
          </span>
        )}
      </div>
      <div className="flex flex-wrap items-center gap-1.5 text-xs text-gray-400 mt-0.5">
        {lead.telefone ? (
          <a
            href={whatsAppLink(lead.telefone)}
            target="_blank"
            rel="noopener noreferrer"
            title="Abrir conversa no WhatsApp"
            className="flex items-center gap-1 text-accent-green hover:underline"
          >
            <MessageCircle className="w-3.5 h-3.5" /> {lead.telefone}
          </a>
        ) : (
          // Leads vindos da Prospecção (OSM) frequentemente não têm telefone
          // cadastrado no mapa — sem esse fallback, sobrava só o ícone do
          // WhatsApp com um link quebrado (wa.me/55) e nenhum número visível.
          <span className="italic text-gray-500">Sem telefone cadastrado</span>
        )}
        {lead.email && <span>· {lead.email}</span>}
      </div>
      {lead.mensagem && <p className={clsx('text-xs text-gray-500 mt-1', !card && 'max-w-xs')}>{lead.mensagem}</p>}
    </>
  )

  const status = (
    <>
      <StatusPillSelect value={lead.status} options={STATUS_OPTIONS} styles={STATUS_STYLES} disabled={saving || !podeEditar} onChange={updateStatus} />
      {lead.opportunity && <div className="mt-2 max-w-36 text-[11px] text-gray-500"><p className="font-semibold text-brand-300">{lead.opportunity.stage}</p><p className="truncate">{lead.opportunity.assignedUserName || 'Sem responsável'} · {lead.opportunity.probability}%</p></div>}
    </>
  )

  const qualificacao = (
    <div className={clsx('flex flex-col gap-1.5', wQualif)}>
      <select
        className="input text-xs py-1"
        value={lead.digitalPresence ?? ''}
        disabled={saving || !podeEditar}
        aria-label="Presença digital"
        onChange={e => updateDigitalPresence(e.target.value as LeadDigitalPresence | '')}
      >
        <option value="">Presença digital…</option>
        {DIGITAL_PRESENCE_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
      <div className="flex items-center gap-1.5">
        <span className={`text-xs font-bold px-1.5 py-0.5 rounded border shrink-0 ${scoreColor(lead.opportunityScore)}`}>
          {lead.opportunityScore ?? '—'}
        </span>
        <input
          type="number" min={0} max={100}
          className={clsx('input text-xs py-1', card ? 'flex-1 min-w-0' : 'w-16')}
          placeholder="Score"
          aria-label="Score de oportunidade"
          defaultValue={lead.opportunityScore ?? ''}
          disabled={saving || !podeEditar}
          onBlur={e => updateScore(e.target.value)}
        />
      </div>
      <div className="flex items-center gap-1">
        <input
          className="input text-xs py-1 flex-1 min-w-0"
          placeholder="Place ID"
          aria-label="Place ID"
          value={placeId}
          disabled={saving || !podeEditar}
          onChange={e => setPlaceId(e.target.value)}
          onBlur={savePlaceId}
        />
        {lead.placeId && (
          <a
            href={mapLink(lead.placeId)}
            target="_blank" rel="noopener noreferrer"
            title="Abrir no mapa"
            className="text-brand-400 hover:text-brand-300 shrink-0"
          >
            <MapPin className="w-3.5 h-3.5" />
          </a>
        )}
      </div>
      {lead.estimatedRevenueRange && <p className="text-[11px] text-gray-500">{lead.estimatedRevenueRange}</p>}
    </div>
  )

  const anotacoes = (
    <>
      {lead.abordagemSugerida && (
        <p className={clsx('mb-2 rounded-lg bg-brand-500/10 p-2 text-[11px] leading-relaxed text-brand-200', wNotas)}>
          <Sparkles className="mr-1 inline h-3 w-3" />{lead.abordagemSugerida}
        </p>
      )}
      <textarea
        className={clsx('input text-xs py-1.5 resize-y min-h-[3.5rem]', wNotas)}
        placeholder="Anotações"
        aria-label="Anotações do lead"
        value={notas}
        onChange={e => setNotas(e.target.value)}
        onBlur={saveNotas}
        disabled={saving || !podeEditar}
      />
    </>
  )

  const acoes = (
    <>
      {/* "Abrir CRM" continua valendo em consulta: o modal mostra oportunidade,
          base legal e linha do tempo — informação, não ação. Ele recebe a mesma
          flag e desabilita os próprios campos. */}
      <button onClick={() => onOpenCrm(lead)} disabled={saving || enriching} className={clsx('btn-primary text-xs py-1 px-2.5', card && 'flex-1 justify-center')}>
        <Workflow className="h-3.5 w-3.5" /> Abrir CRM
      </button>
      {/* Enriquecer gasta cota de serviço externo; converter cria tenant. */}
      {podeEditar && <button onClick={enrichLead} disabled={saving || enriching} className={clsx('btn-secondary text-xs py-1 px-2.5', card && 'flex-1 justify-center')}>
        {enriching ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Sparkles className="h-3.5 w-3.5" />}
        Enriquecer
      </button>}
      {podeEditar && lead.status !== 'Convertido' && (
        <button onClick={() => onConvert(lead)} disabled={saving || enriching} className={clsx('btn-secondary text-xs py-1 px-2.5', card && 'w-full justify-center')}>
          Converter em tenant
        </button>
      )}
    </>
  )

  if (card) {
    return (
      <li className="card space-y-3 !p-3">
        {/* Nome + status no topo: são o par que o operador lê pra decidir se
            essa linha merece atenção agora. */}
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0 flex-1">{contato}</div>
          <div className="shrink-0">{status}</div>
        </div>
        <p className="text-[11px] text-gray-500">Recebido em {fmtDateTime(lead.createdAt)}</p>
        {qualificacao}
        <div>{anotacoes}</div>
        <div className="flex flex-wrap gap-2 border-t border-surface-600 pt-3">{acoes}</div>
      </li>
    )
  }

  return (
    <tr className="border-b border-surface-700 last:border-0 align-top">
      <td className="py-3">{contato}</td>
      <td className="py-3">{status}</td>
      <td className="py-3">{qualificacao}</td>
      <td className="py-3">{anotacoes}</td>
      <td className="py-3 text-gray-400">{fmtDateTime(lead.createdAt)}</td>
      <td className="py-3 text-right">
        <div className="flex flex-col items-end gap-2">{acoes}</div>
      </td>
    </tr>
  )
}

export default function PlataformaLeadsPage() {
  // `platform.leads.read` abre a tela (Auditoria); `platform.leads` é que grava.
  const can = usePlatformPermissions()
  const podeEditar = can('platform.leads')
  const [leads, setLeads] = useState<LeadDto[]>([])
  const [loading, setLoading] = useState(true)
  const [statusFilter, setStatusFilter] = useState<LeadStatus | ''>('')
  const [origemFilter, setOrigemFilter] = useState<OrigemFilter>('')
  const [search, setSearch] = useState('')
  const [convertingLead, setConvertingLead] = useState<LeadDto | null>(null)
  const [crmLead, setCrmLead] = useState<LeadDto | null>(null)
  const [view, setView] = useState<CrmView>('list')
  const [assignees, setAssignees] = useState<CrmAssigneeDto[]>([])
  const [tasks, setTasks] = useState<CrmTaskDto[]>([])
  const [ownerFilter, setOwnerFilter] = useState('')
  const [taskFilter, setTaskFilter] = useState<TaskFilter>('all')
  const [movingLeadId, setMovingLeadId] = useState<string | null>(null)
  const [analytics, setAnalytics] = useState<CrmAnalyticsDto | null>(null)
  const [retentionDueIds, setRetentionDueIds] = useState<string[]>([])

  const fetchLeads = useCallback(() => {
    setLoading(true)
    platformApi.listLeads()
      .then(r => setLeads(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar leads')))
      .finally(() => setLoading(false))
  }, [])

  const fetchCrmMeta = useCallback(() => {
    Promise.all([platformApi.listCrmAssignees(), platformApi.listCrmTasks(), platformApi.getCrmAnalytics(), platformApi.listRetentionDue()])
      .then(([owners, taskResult, analyticsResult, retentionResult]) => { setAssignees(owners.data); setTasks(taskResult.data); setAnalytics(analyticsResult.data); setRetentionDueIds(retentionResult.data) })
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar tarefas e responsáveis.')))
  }, [])

  useEffect(() => { fetchLeads(); fetchCrmMeta() }, [fetchCrmMeta, fetchLeads])

  const refreshAll = useCallback(() => { fetchLeads(); fetchCrmMeta() }, [fetchCrmMeta, fetchLeads])

  // A origem filtra ANTES dos contadores por status: com "Do site" ligado, o
  // "Novo 21" tem que contar leads do site, senão o número da aba discorda da
  // lista que ele abre.
  const leadsPorOrigem = useMemo(
    () => leads.filter(lead => !origemFilter || veioDoSite(lead) === (origemFilter === 'site')),
    [leads, origemFilter],
  )

  const filteredLeads = useMemo(() => {
    const term = search.trim().toLocaleLowerCase('pt-BR')
    return leadsPorOrigem.filter(lead => {
      const matchesStatus = !statusFilter || lead.status === statusFilter
      const matchesOwner = !ownerFilter || (ownerFilter === 'unassigned'
        ? !lead.opportunity?.assignedUserId
        : lead.opportunity?.assignedUserId === ownerFilter)
      const matchesSearch = !term || [lead.nome, lead.telefone, lead.email, lead.origem, lead.mensagem]
        .some(value => value?.toLocaleLowerCase('pt-BR').includes(term))
      return matchesStatus && matchesOwner && matchesSearch
    })
  }, [leadsPorOrigem, ownerFilter, search, statusFilter])

  const stageCounts = useMemo(() => Object.fromEntries(
    STATUS_OPTIONS.map(status => [status, leadsPorOrigem.filter(lead => lead.status === status).length])
  ) as Record<LeadStatus, number>, [leadsPorOrigem])

  // O aviso olha a base inteira, não a filtrada: quem está com um filtro
  // qualquer ligado é justamente quem corre o risco de não ver o contato novo.
  const semResposta = useMemo(() => leads.filter(aguardandoResposta), [leads])
  const esperaMaisLonga = useMemo(
    () => semResposta.reduce((maior, lead) => Math.max(maior, horasDesde(lead.createdAt)), 0),
    [semResposta],
  )
  const respostasAtrasadas = esperaMaisLonga >= HORAS_PARA_RESPONDER

  function mostrarSemResposta() {
    setView('list')
    setOrigemFilter('site')
    setStatusFilter('Novo')
    setOwnerFilter('')
    setSearch('')
  }

  const todayStart = useMemo(() => { const date = new Date(); date.setHours(0, 0, 0, 0); return date }, [])
  const tomorrowStart = useMemo(() => new Date(todayStart.getTime() + 86_400_000), [todayStart])
  const overdueTasks = useMemo(() => tasks.filter(task => task.dueAt && new Date(task.dueAt) < todayStart), [tasks, todayStart])
  const filteredTasks = useMemo(() => tasks.filter(task => {
    const matchesOwner = !ownerFilter || (ownerFilter === 'unassigned' ? !task.assignedUserId : task.assignedUserId === ownerFilter)
    if (!matchesOwner) return false
    const due = task.dueAt ? new Date(task.dueAt) : null
    if (taskFilter === 'overdue') return !!due && due < todayStart
    if (taskFilter === 'today') return !!due && due >= todayStart && due < tomorrowStart
    if (taskFilter === 'future') return !due || due >= tomorrowStart
    return true
  }), [ownerFilter, taskFilter, tasks, todayStart, tomorrowStart])
  const openOpportunities = leads.filter(lead => lead.opportunity && !['Ganho', 'Perdido'].includes(lead.opportunity.stage))
  const weightedPipeline = openOpportunities.reduce((sum, lead) => sum + (lead.opportunity?.value ?? 0) * (lead.opportunity?.probability ?? 0) / 100, 0)

  async function moveLead(lead: LeadDto, stage: CrmOpportunityStage) {
    if (lead.opportunity?.stage === stage) return
    const lostReason = stage === 'Perdido' ? window.prompt('Informe o motivo da perda:') : null
    if (stage === 'Perdido' && !lostReason?.trim()) return
    const config = KANBAN_STAGES.find(item => item.value === stage)!
    setMovingLeadId(lead.id)
    try {
      await platformApi.saveCrmOpportunity(lead.id, {
        stage, probability: config.probability, value: lead.opportunity?.value,
        expectedCloseDate: lead.opportunity?.expectedCloseDate,
        assignedUserId: lead.opportunity?.assignedUserId, lostReason: lostReason?.trim() || null,
      })
      refreshAll(); toast.success(`“${lead.nome}” movido para ${config.label}.`)
    } catch (err) { toast.error(getErrorMessage(err, 'Não foi possível mover a oportunidade.')) }
    finally { setMovingLeadId(null) }
  }

  async function completeTask(task: CrmTaskDto) {
    const outcome = window.prompt('Resultado da tarefa (opcional):', task.outcome ?? '')
    if (outcome === null) return
    try { await platformApi.completeCrmActivity(task.id, outcome.trim() || null); fetchCrmMeta(); toast.success('Tarefa concluída.') }
    catch (err) { toast.error(getErrorMessage(err, 'Não foi possível concluir a tarefa.')) }
  }

  async function handleTenantCreated(tenantId: string) {
    if (!convertingLead) return
    try {
      await platformApi.updateLead(convertingLead.id, { status: 'Convertido', notas: convertingLead.notas, convertedTenantId: tenantId })
      fetchLeads()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Tenant criado, mas não deu pra marcar o lead como convertido.'))
    }
  }

  async function reviewRetention(lead: LeadDto, action: 'Extend' | 'Anonymize') {
    const reason = window.prompt(action === 'Extend' ? 'Justifique por que os dados ainda são necessários:' : 'Justifique a anonimização:')?.trim()
    if (!reason) return
    if (action === 'Anonymize' && !window.confirm(`Anonimizar os dados pessoais de “${lead.nome}”? Esta ação não pode ser desfeita.`)) return
    try {
      await platformApi.reviewLeadRetention(lead.id, { action, reason, extensionDays: action === 'Extend' ? 180 : null })
      refreshAll(); toast.success(action === 'Extend' ? 'Retenção revisada por mais 180 dias.' : 'Lead anonimizado.')
    } catch (err) { toast.error(getErrorMessage(err, 'Não foi possível concluir a revisão.')) }
  }

  return (
    <div className="space-y-5">
      <PageHeader
        icon={UserPlus}
        title="CRM · Leads"
        description="Captação, qualificação, contato e conversão de futuros clientes"
      />

      {!podeEditar && <SomenteLeitura>Você acompanha o funil, a base legal de cada lead e a linha do tempo. Editar, enriquecer, converter e as ações de LGPD ficam com o comercial.</SomenteLeitura>}

      {/* Aviso, não métrica: o contato que a pessoa mandou pelo site é o único
          da tela em que o silêncio é falha nossa. Fica acima dos cartões e leva
          direto para a lista filtrada — ler o número sem conseguir agir nele
          seria só mais um contador. */}
      {semResposta.length > 0 && (
        <button
          type="button"
          onClick={mostrarSemResposta}
          className={clsx('card flex w-full items-center gap-3 border p-4 text-left transition hover:brightness-110',
            respostasAtrasadas ? 'border-red-500/40 bg-red-500/5' : 'border-amber-500/40 bg-amber-500/5')}
        >
          <AlertTriangle className={clsx('h-5 w-5 shrink-0', respostasAtrasadas ? 'text-red-400' : 'text-amber-400')} />
          <div className="min-w-0 flex-1">
            <p className="text-sm font-bold text-white">
              {semResposta.length === 1
                ? '1 contato do site esperando resposta'
                : `${semResposta.length} contatos do site esperando resposta`}
            </p>
            <p className="text-xs text-gray-400">
              O mais antigo há {esperaHumana(esperaMaisLonga)}
              {respostasAtrasadas && ` · acima do prazo de ${HORAS_PARA_RESPONDER}h`}
              {' '}· essas pessoas pediram contato pelo formulário.
            </p>
          </div>
          <span className="shrink-0 text-xs font-bold text-brand-300">Ver só esses</span>
        </button>
      )}

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        {[
          { label: 'Leads captados', value: leads.length, icon: Users, color: 'text-brand-300' },
          { label: 'Oportunidades abertas', value: openOpportunities.length, icon: Target, color: 'text-amber-400' },
          { label: 'Pipeline ponderado', value: weightedPipeline.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }), icon: UserCheck, color: 'text-accent-green' },
          { label: 'Tarefas atrasadas', value: overdueTasks.length, icon: AlertTriangle, color: overdueTasks.length ? 'text-red-400' : 'text-gray-400' },
        ].map(metric => (
          <div key={metric.label} className="card p-4 flex items-center gap-3">
            <metric.icon className={`w-5 h-5 ${metric.color}`} />
            <div>
              <p className="text-xl font-black text-white">{metric.value}</p>
              <p className="text-xs text-gray-500">{metric.label}</p>
            </div>
          </div>
        ))}
      </div>

      <div className="card flex gap-1 overflow-x-auto p-1.5">
        {([
          { value: 'list', label: 'Lista', icon: List },
          { value: 'kanban', label: 'Kanban', icon: Columns3 },
          { value: 'tasks', label: 'Tarefas', icon: CalendarCheck },
          { value: 'analytics', label: 'Análises', icon: ChartNoAxesCombined },
        ] as const).map(item => <button key={item.value} type="button" onClick={() => { setView(item.value); if (item.value !== 'list') setStatusFilter('') }} className={clsx('flex items-center gap-2 rounded-lg px-4 py-2 text-xs font-bold', view === item.value ? 'bg-brand-500/20 text-brand-300' : 'text-gray-500 hover:text-gray-300')}><item.icon className="h-4 w-4" />{item.label}{item.value === 'tasks' && tasks.length > 0 && <span className="rounded-full bg-surface-700 px-1.5 py-0.5 text-[10px]">{tasks.length}</span>}</button>)}
      </div>

      <div className="card p-3 flex flex-col sm:flex-row gap-3">
        <label className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
          <input
            className="input text-sm pl-9 w-full"
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Buscar por nome, telefone, e-mail ou origem"
          />
        </label>
        <select className="input min-w-48 text-sm" value={ownerFilter} onChange={event => setOwnerFilter(event.target.value)}>
          <option value="">Todos os responsáveis</option><option value="unassigned">Sem responsável</option>
          {assignees.map(owner => <option key={owner.id} value={owner.id}>{owner.name}</option>)}
        </select>
        <select className="input min-w-48 text-sm" aria-label="Origem do lead" value={origemFilter} onChange={event => setOrigemFilter(event.target.value as OrigemFilter)}>
          <option value="">Todas as origens</option>
          <option value="site">Do site (formulário)</option>
          <option value="prospeccao">Prospecção</option>
        </select>
        {view === 'list' && <div className="flex gap-1 overflow-x-auto">
          <button type="button" onClick={() => setStatusFilter('')} className={`px-3 py-2 rounded-lg text-xs font-bold whitespace-nowrap ${!statusFilter ? 'bg-brand-500/20 text-brand-300' : 'text-gray-500 hover:text-gray-300'}`}>
            Todos {leadsPorOrigem.length}
          </button>
          {STATUS_OPTIONS.map(status => (
            <button key={status} type="button" onClick={() => setStatusFilter(status)} className={`px-3 py-2 rounded-lg text-xs font-bold whitespace-nowrap ${statusFilter === status ? STATUS_STYLES[status] : 'text-gray-500 hover:text-gray-300'}`}>
              {status} {stageCounts[status]}
            </button>
          ))}
        </div>}
      </div>

      {loading ? (
        <div className="card flex items-center justify-center py-16">
          <Loader2 className="w-6 h-6 animate-spin text-brand-400" />
        </div>
      ) : view === 'list' ? (
        filteredLeads.length === 0 ? (
          <div className="card"><p className="text-gray-400 text-center py-16">Nenhum lead encontrado com esses filtros.</p></div>
        ) : (
          <>
            {/* Desktop: tabela de 900px. Celular: um card por lead — a mesma
                linha, com os seis campos editáveis empilhados. */}
            <div className="card table-scroll hidden sm:block">
              <table className="w-full min-w-[900px] text-sm">
                <thead>
                  <tr className="text-left text-gray-500 border-b border-surface-600">
                    <th className="py-2 font-medium">Contato</th>
                    <th className="py-2 font-medium">Status</th>
                    <th className="py-2 font-medium">Oportunidade</th>
                    <th className="py-2 font-medium">Anotações</th>
                    <th className="py-2 font-medium">Recebido em</th>
                    <th className="py-2 font-medium text-right">Ações</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredLeads.map(l => (
                    <LeadRow key={l.id} lead={l} onChanged={refreshAll} onConvert={setConvertingLead} onOpenCrm={setCrmLead} podeEditar={podeEditar} />
                  ))}
                </tbody>
              </table>
            </div>
            <ul className="space-y-2 sm:hidden">
              {filteredLeads.map(l => (
                <LeadRow key={l.id} layout="card" lead={l} onChanged={refreshAll} onConvert={setConvertingLead} onOpenCrm={setCrmLead} podeEditar={podeEditar} />
              ))}
            </ul>
          </>
        )
      ) : view === 'kanban' ? <div className="table-scroll pb-2" style={{ scrollSnapType: 'x mandatory' }}>
        {/* Kanban rola de lado em qualquer tela — são 7 etapas. O que muda no
            celular é a largura da coluna: 85vw faz UMA etapa ocupar a tela com
            uma fresta da seguinte (indicando que há mais), e o snap para o
            gesto exatamente no início de cada coluna em vez de deixar o
            usuário no meio de duas. Antes eram 7 colunas de 300px num trilho
            fixo de 2100px, o que no celular mostrava um terço de coluna. */}
        <div className="flex gap-3">{KANBAN_STAGES.map(column => {
          const columnLeads = filteredLeads.filter(lead => column.value === null ? !lead.opportunity : lead.opportunity?.stage === column.value)
          const columnValue = columnLeads.reduce((sum, lead) => sum + (lead.opportunity?.value ?? 0), 0)
          return <section key={column.value ?? 'none'} onDragOver={event => event.preventDefault()} onDrop={event => { const lead = leads.find(item => item.id === event.dataTransfer.getData('text/lead-id')); if (lead && column.value) moveLead(lead, column.value) }} style={{ scrollSnapAlign: 'start' }} className={clsx('min-h-[420px] w-[85vw] max-w-[300px] shrink-0 rounded-xl border-t-2 bg-surface-800 p-3 sm:w-[300px]', column.color)}>
            <div className="mb-3 flex items-start justify-between gap-2"><div><h3 className="text-sm font-bold text-white">{column.label}</h3><p className="text-[11px] text-gray-500">{columnLeads.length} · {columnValue.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}</p></div><span className="rounded-full bg-surface-700 px-2 py-1 text-xs text-gray-300">{columnLeads.length}</span></div>
            <div className="space-y-2">{columnLeads.map(lead => <article key={lead.id} draggable={movingLeadId !== lead.id} onDragStart={event => event.dataTransfer.setData('text/lead-id', lead.id)} onClick={() => setCrmLead(lead)} className={clsx('cursor-grab rounded-xl border border-surface-600 bg-surface-700 p-3 transition hover:border-brand-500/50', movingLeadId === lead.id && 'opacity-50')}><div className="flex items-start justify-between gap-2"><h4 className="text-sm font-semibold text-white">{lead.nome}</h4>{movingLeadId === lead.id && <Loader2 className="h-3.5 w-3.5 animate-spin text-brand-400" />}</div><p className="mt-1 text-[11px] text-gray-500">{lead.opportunity?.assignedUserName || 'Sem responsável'}</p><div className="mt-2 flex items-center justify-between text-xs"><span className="text-gray-400">{lead.opportunity?.probability ?? 0}%</span><strong className="text-brand-300">{lead.opportunity?.value?.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }) ?? 'Sem valor'}</strong></div></article>)}</div>
          </section>
        })}</div>
      </div> : view === 'tasks' ? <section className="space-y-3">
        <div className="card flex gap-1 overflow-x-auto p-2">{([
          ['all', 'Todas', tasks.length], ['overdue', 'Atrasadas', overdueTasks.length],
          ['today', 'Hoje', tasks.filter(task => task.dueAt && new Date(task.dueAt) >= todayStart && new Date(task.dueAt) < tomorrowStart).length],
          ['future', 'Futuras', tasks.filter(task => !task.dueAt || new Date(task.dueAt) >= tomorrowStart).length],
        ] as [TaskFilter, string, number][]).map(([value, label, count]) => <button key={value} onClick={() => setTaskFilter(value)} className={clsx('rounded-lg px-3 py-2 text-xs font-bold', taskFilter === value ? 'bg-brand-500/20 text-brand-300' : 'text-gray-500 hover:text-gray-300')}>{label} {count}</button>)}</div>
        {filteredTasks.length === 0 ? <div className="card py-16 text-center text-sm text-gray-400">Nenhuma tarefa neste filtro.</div> : <div className="grid gap-3 lg:grid-cols-2 xl:grid-cols-3">{filteredTasks.map(task => {
          const overdue = task.dueAt && new Date(task.dueAt) < todayStart
          const lead = leads.find(item => item.id === task.leadId)
          return <article key={task.id} className={clsx('card border p-4', overdue ? 'border-red-500/40' : 'border-surface-600')}><div className="flex items-start justify-between gap-3"><div><span className={clsx('text-[10px] font-bold uppercase', overdue ? 'text-red-400' : 'text-brand-300')}>{overdue ? 'Atrasada' : 'Tarefa'}</span><h3 className="text-sm font-semibold text-white">{task.title}</h3><button onClick={() => lead && setCrmLead(lead)} className="text-xs text-brand-400 hover:underline">{task.leadName}</button></div>{podeEditar && <button onClick={() => completeTask(task)} className="btn-secondary p-2" title="Concluir"><Check className="h-3.5 w-3.5" /></button>}</div>{task.description && <p className="mt-2 text-xs text-gray-400">{task.description}</p>}<div className="mt-3 flex items-center justify-between text-[11px] text-gray-500"><span>{task.assignedUserName || 'Sem responsável'}</span><span className="flex items-center gap-1"><Clock3 className="h-3 w-3" />{task.dueAt ? fmtDateTime(task.dueAt) : 'Sem prazo'}</span></div></article>
        })}</div>}
      </section> : <section className="space-y-4">
        {!analytics ? <div className="card flex justify-center py-16"><Loader2 className="h-6 w-6 animate-spin text-brand-400" /></div> : <>
          <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">{[
            ['Conversão', `${analytics.conversionRate.toLocaleString('pt-BR')}%`, `${analytics.convertedLeads} de ${analytics.totalLeads}`],
            ['Pipeline aberto', analytics.openPipeline.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }), `${analytics.openOpportunities} oportunidades`],
            ['Forecast ponderado', analytics.weightedPipeline.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }), 'valor × probabilidade'],
            ['Ciclo médio', `${analytics.averageSalesCycleDays.toLocaleString('pt-BR')} dias`, 'captação até conversão'],
          ].map(([label, value, hint]) => <div key={label} className="card p-4"><p className="text-xs text-gray-500">{label}</p><p className="mt-1 text-xl font-black text-white">{value}</p><p className="text-[11px] text-gray-600">{hint}</p></div>)}</div>
          <div className="grid gap-4 xl:grid-cols-2"><AnalyticsTable title="Funil e aging" rows={analytics.byStage} showAge /><AnalyticsTable title="Origem dos leads" rows={analytics.bySource} /></div>
          <div className="grid gap-4 xl:grid-cols-2"><AnalyticsTable title="Carteira por responsável" rows={analytics.byOwner} /><AnalyticsTable title="Motivos de perda" rows={analytics.lostReasons} hideValue /></div>
          <div className="card p-4"><h3 className="font-bold text-white">Movimento nos últimos 6 meses</h3><div className="mt-4 grid grid-cols-6 gap-2">{analytics.monthlyTrend.map(item => { const max = Math.max(1, ...analytics.monthlyTrend.map(point => point.created)); return <div key={item.month} className="text-center"><div className="flex h-32 items-end justify-center gap-1"><div className="w-4 rounded-t bg-brand-500" style={{ height: `${Math.max(4, item.created / max * 100)}%` }} title={`${item.created} captados`} /><div className="w-4 rounded-t bg-accent-green" style={{ height: `${Math.max(4, item.converted / max * 100)}%` }} title={`${item.converted} convertidos`} /></div><p className="mt-2 text-[10px] text-gray-500">{item.month.slice(5)}/{item.month.slice(2, 4)}</p><p className="text-[10px] text-gray-600">{item.created}/{item.converted}</p></div>})}</div><p className="mt-3 text-[11px] text-gray-500"><span className="text-brand-400">■ captados</span> · <span className="text-accent-green">■ convertidos</span></p></div>
          <div className="card p-4"><div className="flex items-center justify-between"><div><h3 className="flex items-center gap-2 font-bold text-white"><ShieldCheck className="h-4 w-4 text-brand-400" />Revisão de retenção</h3><p className="text-xs text-gray-500">{analytics.retentionReviewsDue} vencidas · {analytics.contactBlocked} contatos bloqueados</p></div></div><div className="mt-3 space-y-2">{retentionDueIds.length === 0 ? <p className="rounded-lg border border-dashed border-surface-600 p-6 text-center text-sm text-gray-500">Nenhuma revisão pendente.</p> : retentionDueIds.map(id => { const lead = leads.find(item => item.id === id); return lead ? <div key={id} className="flex items-center justify-between gap-3 rounded-lg bg-surface-700 p-3"><div><p className="text-sm font-semibold text-white">{lead.nome}</p><p className="text-xs text-gray-500">Revisão vencida em {lead.retentionReviewAt ? new Date(lead.retentionReviewAt).toLocaleDateString('pt-BR') : '-'}</p></div>{podeEditar && <div className="flex gap-2"><button onClick={() => reviewRetention(lead, 'Extend')} className="btn-secondary text-xs">Prorrogar 180 dias</button><button onClick={() => reviewRetention(lead, 'Anonymize')} className="text-xs font-bold text-red-300">Anonimizar</button></div>}</div> : null })}</div></div>
        </>}
      </section>}

      {convertingLead && (
        <CreateTenantModal
          initialEmail={convertingLead.email ?? ''}
          onClose={() => setConvertingLead(null)}
          onCreated={handleTenantCreated}
        />
      )}

      {crmLead && <CrmWorkspaceModal lead={crmLead} onClose={() => setCrmLead(null)} onChanged={fetchLeads} podeEditar={podeEditar} />}
    </div>
  )
}

function AnalyticsTable({ title, rows, showAge = false, hideValue = false }: { title: string; rows: CrmAnalyticsDto['byStage']; showAge?: boolean; hideValue?: boolean }) {
  return <div className="card p-4"><h3 className="font-bold text-white">{title}</h3><div className="mt-3 space-y-2">{rows.length === 0 ? <p className="py-5 text-center text-xs text-gray-500">Sem dados ainda.</p> : rows.map(row => <div key={row.label} className="grid grid-cols-[1fr_auto_auto] items-center gap-3 rounded-lg bg-surface-700 px-3 py-2 text-xs"><span className="truncate text-gray-300">{row.label}</span><strong className="text-white">{row.count}</strong>{showAge ? <span className="min-w-16 text-right text-gray-500">{row.averageAgeDays.toLocaleString('pt-BR')} dias</span> : !hideValue ? <span className="min-w-24 text-right text-brand-300">{row.value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}</span> : <span />}</div>)}</div></div>
}
