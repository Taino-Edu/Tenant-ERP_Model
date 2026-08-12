'use client'

import { FormEvent, useEffect, useState } from 'react'
import {
  CrmActivityDto, CrmActivityType, CrmAssigneeDto, CrmOpportunityStage,
  CrmWorkspaceDto, LeadDto, ReferralPartnerDto, getErrorMessage, platformApi, referralApi,
} from '@/lib/api'
import Modal from '@/components/admin/ui/Modal'
import toast from 'react-hot-toast'
import { CalendarClock, Check, CircleDollarSign, Loader2, MessageSquarePlus, Target } from 'lucide-react'
import clsx from 'clsx'

const STAGES: { value: CrmOpportunityStage; label: string; probability: number }[] = [
  { value: 'Qualificacao', label: 'Qualificação', probability: 20 },
  { value: 'Diagnostico', label: 'Diagnóstico', probability: 35 },
  { value: 'Proposta', label: 'Proposta', probability: 55 },
  { value: 'Negociacao', label: 'Negociação', probability: 75 },
  { value: 'Ganho', label: 'Ganho', probability: 100 },
  { value: 'Perdido', label: 'Perdido', probability: 0 },
]

const ACTIVITY_TYPES: { value: CrmActivityType; label: string }[] = [
  { value: 'Comentario', label: 'Comentário interno' },
  { value: 'Tarefa', label: 'Tarefa' },
  { value: 'Ligacao', label: 'Ligação' },
  { value: 'WhatsApp', label: 'WhatsApp' },
  { value: 'Email', label: 'E-mail' },
  { value: 'Reuniao', label: 'Reunião' },
]

const ACTIVITY_LABEL: Record<CrmActivityType, string> = {
  Comentario: 'Comentário', Tarefa: 'Tarefa', Ligacao: 'Ligação', WhatsApp: 'WhatsApp',
  Email: 'E-mail', Reuniao: 'Reunião', MudancaEtapa: 'Funil', MudancaResponsavel: 'Responsável',
}

function localDateTime(value: string) {
  return new Date(value).toLocaleString('pt-BR', {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
  })
}

export default function CrmWorkspaceModal({ lead, onClose, onChanged }: {
  lead: LeadDto; onClose: () => void; onChanged: () => void
}) {
  const [workspace, setWorkspace] = useState<CrmWorkspaceDto | null>(null)
  const [assignees, setAssignees] = useState<CrmAssigneeDto[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [stage, setStage] = useState<CrmOpportunityStage>('Qualificacao')
  const [probability, setProbability] = useState(20)
  const [value, setValue] = useState('')
  const [expectedCloseDate, setExpectedCloseDate] = useState('')
  const [assignedUserId, setAssignedUserId] = useState('')
  const [lostReason, setLostReason] = useState('')
  const [activityType, setActivityType] = useState<CrmActivityType>('Tarefa')
  const [activityTitle, setActivityTitle] = useState('')
  const [activityDescription, setActivityDescription] = useState('')
  const [activityDueAt, setActivityDueAt] = useState('')
  const [partners, setPartners] = useState<ReferralPartnerDto[]>([])
  const [currentLead, setCurrentLead] = useState(lead)
  const [campaign, setCampaign] = useState(lead.campaign ?? '')
  const [utmSource, setUtmSource] = useState(lead.utmSource ?? '')
  const [utmMedium, setUtmMedium] = useState(lead.utmMedium ?? '')
  const [utmCampaign, setUtmCampaign] = useState(lead.utmCampaign ?? '')
  const [referralPartnerId, setReferralPartnerId] = useState(lead.referralPartnerId ?? '')
  const [dataOriginDetails, setDataOriginDetails] = useState(lead.dataOriginDetails)
  const [processingPurpose, setProcessingPurpose] = useState(lead.processingPurpose)
  const [legalBasis, setLegalBasis] = useState<LeadDto['legalBasis']>(lead.legalBasis)

  async function load() {
    setLoading(true)
    try {
      const [workspaceResult, assigneesResult, partnersResult] = await Promise.all([
        platformApi.getCrmWorkspace(lead.id), platformApi.listCrmAssignees(), referralApi.partners(),
      ])
      const data = workspaceResult.data
      setWorkspace(data); setAssignees(assigneesResult.data); setPartners(partnersResult.data.filter(item => item.active))
      if (data.opportunity) {
        setStage(data.opportunity.stage); setProbability(data.opportunity.probability)
        setValue(data.opportunity.value?.toString() ?? '')
        setExpectedCloseDate(data.opportunity.expectedCloseDate?.slice(0, 10) ?? '')
        setAssignedUserId(data.opportunity.assignedUserId ?? '')
        setLostReason(data.opportunity.lostReason ?? '')
      }
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível abrir o CRM deste lead.'))
    } finally { setLoading(false) }
  }

  useEffect(() => { load() }, [lead.id]) // eslint-disable-line react-hooks/exhaustive-deps

  function changeStage(nextStage: CrmOpportunityStage) {
    setStage(nextStage)
    setProbability(STAGES.find(item => item.value === nextStage)?.probability ?? probability)
  }

  async function saveOpportunity(event: FormEvent) {
    event.preventDefault(); setSaving(true)
    try {
      await platformApi.saveCrmOpportunity(lead.id, {
        stage, probability, value: value === '' ? null : Number(value),
        expectedCloseDate: expectedCloseDate ? new Date(`${expectedCloseDate}T12:00:00`).toISOString() : null,
        assignedUserId: assignedUserId || null, lostReason: stage === 'Perdido' ? lostReason : null,
      })
      await load(); onChanged(); toast.success('Oportunidade atualizada.')
    } catch (error) { toast.error(getErrorMessage(error, 'Não foi possível salvar a oportunidade.')) }
    finally { setSaving(false) }
  }

  async function createActivity(event: FormEvent) {
    event.preventDefault(); setSaving(true)
    try {
      await platformApi.createCrmActivity(lead.id, {
        type: activityType, title: activityTitle.trim(), description: activityDescription.trim() || null,
        dueAt: activityDueAt ? new Date(activityDueAt).toISOString() : null,
      })
      setActivityTitle(''); setActivityDescription(''); setActivityDueAt('')
      await load(); toast.success(activityType === 'Tarefa' ? 'Tarefa criada.' : 'Atividade registrada.')
    } catch (error) { toast.error(getErrorMessage(error, 'Não foi possível registrar a atividade.')) }
    finally { setSaving(false) }
  }

  async function saveGovernance(event: FormEvent) {
    event.preventDefault(); setSaving(true)
    try {
      const result = await platformApi.updateLead(lead.id, {
        status: currentLead.status, notas: currentLead.notas,
        campaign: campaign.trim() || null, utmSource: utmSource.trim() || null,
        utmMedium: utmMedium.trim() || null, utmCampaign: utmCampaign.trim() || null,
        referralPartnerId: referralPartnerId || null,
        dataOriginDetails: dataOriginDetails.trim(), processingPurpose: processingPurpose.trim(),
        legalBasis,
      })
      setCurrentLead(result.data); onChanged(); toast.success('Origem e governança atualizadas.')
    } catch (error) { toast.error(getErrorMessage(error, 'Não foi possível salvar a governança.')) }
    finally { setSaving(false) }
  }

  async function validateLegitimateInterest() {
    setSaving(true)
    try {
      const result = await platformApi.validateLeadLegitimateInterest(lead.id)
      setCurrentLead(result.data); onChanged(); toast.success('Avaliação registrada; contato comercial liberado.')
    } catch (error) { toast.error(getErrorMessage(error, 'Não foi possível registrar a avaliação.')) }
    finally { setSaving(false) }
  }

  async function registerOpposition() {
    const reason = window.prompt('Motivo ou canal em que a oposição foi recebida:')?.trim()
    if (!reason) return
    setSaving(true)
    try {
      const result = await platformApi.registerLeadOpposition(lead.id, reason)
      setCurrentLead(result.data); onChanged(); toast.success('Oposição registrada e contatos bloqueados.')
    } catch (error) { toast.error(getErrorMessage(error, 'Não foi possível registrar a oposição.')) }
    finally { setSaving(false) }
  }

  async function completeActivity(activity: CrmActivityDto) {
    const outcome = window.prompt('Resultado da tarefa (opcional):', activity.outcome ?? '')
    if (outcome === null) return
    try {
      await platformApi.completeCrmActivity(activity.id, outcome.trim() || null)
      await load(); toast.success('Tarefa concluída.')
    } catch (error) { toast.error(getErrorMessage(error, 'Não foi possível concluir a tarefa.')) }
  }

  return <Modal onClose={onClose} maxWidth="2xl" title={`CRM · ${lead.nome}`} icon={Target} closeOnBackdrop={false}>
    {loading && !workspace ? <div className="flex justify-center p-12"><Loader2 className="h-6 w-6 animate-spin text-brand-400" /></div> :
      <div className="grid gap-5 p-4 lg:grid-cols-2">
        <div className="space-y-4">
          <form onSubmit={saveGovernance} className="space-y-3 rounded-xl border border-surface-600 p-4">
            <div className="flex items-center justify-between gap-2"><div><h4 className="text-sm font-bold text-white">Origem, indicação e LGPD</h4><p className="text-[11px] text-gray-500">Atribuição comercial e registro da operação de tratamento</p></div><span className={clsx('rounded-full px-2 py-1 text-[10px] font-bold', currentLead.canContact ? 'bg-accent-green/10 text-accent-green' : 'bg-amber-500/10 text-amber-300')}>{currentLead.canContact ? 'Contato liberado' : 'Contato bloqueado'}</span></div>
            <div className="grid grid-cols-2 gap-3">
              <label><span className="label">Campanha</span><input className="input w-full" maxLength={120} value={campaign} onChange={event => setCampaign(event.target.value)} /></label>
              <label><span className="label">Vendedor indicador</span><select className="input w-full" value={referralPartnerId} onChange={event => setReferralPartnerId(event.target.value)}><option value="">Sem indicação</option>{partners.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
              <label><span className="label">UTM source</span><input className="input w-full" maxLength={120} value={utmSource} onChange={event => setUtmSource(event.target.value)} /></label>
              <label><span className="label">UTM medium</span><input className="input w-full" maxLength={120} value={utmMedium} onChange={event => setUtmMedium(event.target.value)} /></label>
              <label className="col-span-2"><span className="label">UTM campaign</span><input className="input w-full" maxLength={120} value={utmCampaign} onChange={event => setUtmCampaign(event.target.value)} /></label>
              <label className="col-span-2"><span className="label">Base legal registrada</span><select className="input w-full" value={legalBasis} onChange={event => setLegalBasis(event.target.value as LeadDto['legalBasis'])}><option value="NaoDefinida">Pendente de definição</option><option value="ProcedimentosPreContratuais">Procedimentos pré-contratuais</option><option value="LegitimoInteresse">Legítimo interesse</option><option value="Consentimento">Consentimento documentado</option><option value="ObrigacaoLegal">Obrigação legal</option></select></label>
              <label className="col-span-2"><span className="label">Origem dos dados</span><textarea required maxLength={500} className="input min-h-16 w-full" value={dataOriginDetails} onChange={event => setDataOriginDetails(event.target.value)} /></label>
              <label className="col-span-2"><span className="label">Finalidade</span><textarea required maxLength={500} className="input min-h-16 w-full" value={processingPurpose} onChange={event => setProcessingPurpose(event.target.value)} /></label>
            </div>
            <div className="rounded-lg bg-surface-700 p-3 text-xs text-gray-400"><strong className="text-gray-200">Base:</strong> {currentLead.legalBasis} · <strong className="text-gray-200">revisão:</strong> {currentLead.retentionReviewAt ? new Date(currentLead.retentionReviewAt).toLocaleDateString('pt-BR') : 'não definida'}{currentLead.opposedAt && <p className="mt-1 text-red-300">Oposição em {localDateTime(currentLead.opposedAt)}: {currentLead.oppositionReason}</p>}</div>
            <button disabled={saving} className="btn-secondary w-full justify-center">Salvar atribuição e governança</button>
            {currentLead.legalBasis === 'LegitimoInteresse' && !currentLead.legitimateInterestAssessedAt && !currentLead.opposedAt && <button type="button" disabled={saving} onClick={validateLegitimateInterest} className="btn-primary w-full justify-center">Registrar teste de legítimo interesse</button>}
            {!currentLead.opposedAt && <button type="button" disabled={saving} onClick={registerOpposition} className="w-full text-xs font-semibold text-red-300 hover:underline">Registrar oposição / não contatar</button>}
          </form>

          <form onSubmit={saveOpportunity} className="space-y-3 rounded-xl border border-surface-600 p-4">
            <div className="flex items-center gap-2"><CircleDollarSign className="h-4 w-4 text-brand-400" /><h4 className="text-sm font-bold text-white">Oportunidade</h4></div>
            <div className="grid grid-cols-2 gap-3">
              <label className="col-span-2"><span className="label">Etapa do funil</span><select className="input w-full" value={stage} onChange={event => changeStage(event.target.value as CrmOpportunityStage)}>{STAGES.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
              <label><span className="label">Probabilidade</span><input className="input w-full" type="number" min={0} max={100} value={probability} onChange={event => setProbability(Number(event.target.value))} /></label>
              <label><span className="label">Valor previsto</span><input className="input w-full" type="number" min={0} step="0.01" placeholder="R$" value={value} onChange={event => setValue(event.target.value)} /></label>
              <label className="col-span-2"><span className="label">Responsável</span><select className="input w-full" value={assignedUserId} onChange={event => setAssignedUserId(event.target.value)}><option value="">Sem responsável</option>{assignees.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
              <label className="col-span-2"><span className="label">Previsão de fechamento</span><input className="input w-full" type="date" value={expectedCloseDate} onChange={event => setExpectedCloseDate(event.target.value)} /></label>
              {stage === 'Perdido' && <label className="col-span-2"><span className="label">Motivo da perda</span><textarea required maxLength={500} className="input min-h-20 w-full" value={lostReason} onChange={event => setLostReason(event.target.value)} /></label>}
            </div>
            <button disabled={saving} className="btn-primary w-full justify-center">{saving && <Loader2 className="h-4 w-4 animate-spin" />} Salvar oportunidade</button>
          </form>

          <form onSubmit={createActivity} className="space-y-3 rounded-xl border border-surface-600 p-4">
            <div className="flex items-center gap-2"><MessageSquarePlus className="h-4 w-4 text-brand-400" /><h4 className="text-sm font-bold text-white">Registrar atividade</h4></div>
            <select className="input w-full" value={activityType} onChange={event => setActivityType(event.target.value as CrmActivityType)}>{ACTIVITY_TYPES.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select>
            <input required maxLength={160} className="input w-full" placeholder="Título ou próxima ação" value={activityTitle} onChange={event => setActivityTitle(event.target.value)} />
            <textarea maxLength={2000} className="input min-h-20 w-full" placeholder="Detalhes e contexto" value={activityDescription} onChange={event => setActivityDescription(event.target.value)} />
            {activityType === 'Tarefa' && <label><span className="label">Vencimento</span><input required className="input w-full" type="datetime-local" value={activityDueAt} onChange={event => setActivityDueAt(event.target.value)} /></label>}
            <button disabled={saving} className="btn-secondary w-full justify-center">Registrar</button>
          </form>
        </div>

        <section className="min-w-0">
          <div className="mb-3 flex items-center justify-between"><div className="flex items-center gap-2"><CalendarClock className="h-4 w-4 text-brand-400" /><h4 className="text-sm font-bold text-white">Linha do tempo</h4></div><span className="text-xs text-gray-500">{workspace?.activities.length ?? 0} eventos</span></div>
          <div className="max-h-[65vh] space-y-2 overflow-y-auto pr-1">
            {workspace?.activities.length === 0 && <p className="rounded-xl border border-dashed border-surface-600 p-8 text-center text-sm text-gray-500">Nenhuma atividade registrada.</p>}
            {workspace?.activities.map(activity => {
              const overdue = activity.type === 'Tarefa' && !activity.completedAt && activity.dueAt && new Date(activity.dueAt) < new Date()
              return <article key={activity.id} className={clsx('rounded-xl border p-3', overdue ? 'border-red-500/40 bg-red-500/5' : 'border-surface-600')}>
                <div className="flex items-start justify-between gap-2"><div><span className="text-[10px] font-bold uppercase tracking-wide text-brand-300">{ACTIVITY_LABEL[activity.type]}</span><h5 className="text-sm font-semibold text-white">{activity.title}</h5></div><span className="shrink-0 text-[10px] text-gray-500">{localDateTime(activity.createdAt)}</span></div>
                {activity.description && <p className="mt-1 text-xs leading-relaxed text-gray-400">{activity.description}</p>}
                {activity.outcome && <p className="mt-2 rounded-lg bg-accent-green/5 p-2 text-xs text-accent-green">Resultado: {activity.outcome}</p>}
                <div className="mt-2 flex items-center justify-between gap-2 text-[11px] text-gray-500"><span>{activity.createdByUserName}{activity.dueAt ? ` · vence ${localDateTime(activity.dueAt)}` : ''}</span>{activity.type === 'Tarefa' && (activity.completedAt ? <span className="flex items-center gap-1 text-accent-green"><Check className="h-3 w-3" /> Concluída</span> : <button onClick={() => completeActivity(activity)} className="text-brand-300 hover:underline">Concluir</button>)}</div>
              </article>
            })}
          </div>
        </section>
      </div>}
  </Modal>
}
