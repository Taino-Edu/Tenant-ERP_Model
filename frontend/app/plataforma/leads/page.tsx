'use client'
import { useEffect, useState, useCallback } from 'react'
import { leadsApi, platformApi, LeadDto, LeadStatus, LeadDigitalPresence, getErrorMessage } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import CreateTenantModal from '@/components/plataforma/CreateTenantModal'
import StatusPillSelect from '@/components/admin/StatusPillSelect'
import toast from 'react-hot-toast'
import { UserPlus, Loader2, MessageCircle, MapPin } from 'lucide-react'

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

function LeadRow({ lead, onChanged, onConvert }: { lead: LeadDto; onChanged: () => void; onConvert: (lead: LeadDto) => void }) {
  const [notas, setNotas] = useState(lead.notas ?? '')
  const [placeId, setPlaceId] = useState(lead.placeId ?? '')
  const [saving, setSaving] = useState(false)

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

  return (
    <tr className="border-b border-surface-700 last:border-0 align-top">
      <td className="py-3">
        <p className="text-white font-medium">{lead.nome}</p>
        <div className="flex items-center gap-1.5 text-xs text-gray-400 mt-0.5">
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
        {lead.mensagem && <p className="text-xs text-gray-500 mt-1 max-w-xs">{lead.mensagem}</p>}
      </td>
      <td className="py-3">
        <StatusPillSelect value={lead.status} options={STATUS_OPTIONS} styles={STATUS_STYLES} disabled={saving} onChange={updateStatus} />
      </td>
      <td className="py-3">
        <div className="flex flex-col gap-1.5 w-36">
          <select
            className="input text-xs py-1"
            value={lead.digitalPresence ?? ''}
            disabled={saving}
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
              className="input text-xs py-1 w-16"
              placeholder="Score"
              defaultValue={lead.opportunityScore ?? ''}
              disabled={saving}
              onBlur={e => updateScore(e.target.value)}
            />
          </div>
          <div className="flex items-center gap-1">
            <input
              className="input text-xs py-1 flex-1 min-w-0"
              placeholder="Place ID"
              value={placeId}
              disabled={saving}
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
        </div>
      </td>
      <td className="py-3">
        <textarea
          className="input text-xs py-1.5 w-64 resize-y min-h-[3.5rem]"
          placeholder="Anotações"
          value={notas}
          onChange={e => setNotas(e.target.value)}
          onBlur={saveNotas}
          disabled={saving}
        />
      </td>
      <td className="py-3 text-gray-400">{fmtDateTime(lead.createdAt)}</td>
      <td className="py-3 text-right">
        {lead.status !== 'Convertido' && (
          <button onClick={() => onConvert(lead)} className="btn-secondary text-xs py-1 px-2.5">
            Converter em tenant
          </button>
        )}
      </td>
    </tr>
  )
}

export default function PlataformaLeadsPage() {
  const [leads, setLeads] = useState<LeadDto[]>([])
  const [loading, setLoading] = useState(true)
  const [statusFilter, setStatusFilter] = useState<LeadStatus | ''>('')
  const [convertingLead, setConvertingLead] = useState<LeadDto | null>(null)

  const fetchLeads = useCallback(() => {
    setLoading(true)
    platformApi.listLeads(statusFilter || undefined)
      .then(r => setLeads(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar leads')))
      .finally(() => setLoading(false))
  }, [statusFilter])

  useEffect(() => { fetchLeads() }, [fetchLeads])

  async function handleTenantCreated(tenantId: string) {
    if (!convertingLead) return
    try {
      await platformApi.updateLead(convertingLead.id, { status: 'Convertido', notas: convertingLead.notas, convertedTenantId: tenantId })
      fetchLeads()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Tenant criado, mas não deu pra marcar o lead como convertido.'))
    }
  }

  return (
    <div className="space-y-5">
      <PageHeader
        icon={UserPlus}
        title="Leads"
        description="Quem demonstrou interesse em contratar a plataforma"
        actions={
          <select className="input text-sm py-1.5" value={statusFilter} onChange={e => setStatusFilter(e.target.value as LeadStatus | '')}>
            <option value="">Todos os status</option>
            {STATUS_OPTIONS.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        }
      />

      <div className="card overflow-x-auto">
        {loading ? (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="w-6 h-6 animate-spin text-brand-400" />
          </div>
        ) : leads.length === 0 ? (
          <p className="text-gray-400 text-center py-16">Nenhum lead ainda.</p>
        ) : (
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
              {leads.map(l => (
                <LeadRow key={l.id} lead={l} onChanged={fetchLeads} onConvert={setConvertingLead} />
              ))}
            </tbody>
          </table>
        )}
      </div>

      {convertingLead && (
        <CreateTenantModal
          initialEmail={convertingLead.email ?? ''}
          onClose={() => setConvertingLead(null)}
          onCreated={handleTenantCreated}
        />
      )}
    </div>
  )
}
