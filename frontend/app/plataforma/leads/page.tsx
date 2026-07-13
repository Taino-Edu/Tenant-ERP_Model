'use client'
import { useEffect, useState, useCallback } from 'react'
import { leadsApi, platformApi, LeadDto, LeadStatus, getErrorMessage } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import CreateTenantModal from '@/components/plataforma/CreateTenantModal'
import toast from 'react-hot-toast'
import { UserPlus, Loader2 } from 'lucide-react'
import clsx from 'clsx'

const STATUS_OPTIONS: LeadStatus[] = ['Novo', 'Contatado', 'Convertido', 'Perdido']

const STATUS_STYLES: Record<LeadStatus, string> = {
  Novo:       'bg-brand-500/10 text-brand-300 border-brand-500/30',
  Contatado:  'bg-amber-500/10 text-amber-400 border-amber-500/30',
  Convertido: 'bg-accent-green/10 text-accent-green border-accent-green/30',
  Perdido:    'bg-gray-500/10 text-gray-400 border-gray-500/30',
}

function fmtDateTime(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

function LeadRow({ lead, onChanged, onConvert }: { lead: LeadDto; onChanged: () => void; onConvert: (lead: LeadDto) => void }) {
  const [notas, setNotas] = useState(lead.notas ?? '')
  const [saving, setSaving] = useState(false)

  async function updateStatus(status: LeadStatus) {
    setSaving(true)
    try {
      await platformApi.updateLead(lead.id, { status, notas: lead.notas })
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
      await platformApi.updateLead(lead.id, { status: lead.status, notas })
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao salvar anotação.'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <tr className="border-b border-surface-700 last:border-0 align-top">
      <td className="py-3">
        <p className="text-white font-medium">{lead.nome}</p>
        <p className="text-xs text-gray-400">{lead.telefone}{lead.email ? ` · ${lead.email}` : ''}</p>
        {lead.mensagem && <p className="text-xs text-gray-500 mt-1 max-w-xs">{lead.mensagem}</p>}
      </td>
      <td className="py-3">
        <select
          className="input text-xs py-1"
          value={lead.status}
          disabled={saving}
          onChange={e => updateStatus(e.target.value as LeadStatus)}
        >
          {STATUS_OPTIONS.map(s => <option key={s} value={s}>{s}</option>)}
        </select>
        <span className={clsx('ml-2 text-xs font-medium px-2 py-0.5 rounded-full border', STATUS_STYLES[lead.status])}>
          {lead.status}
        </span>
      </td>
      <td className="py-3">
        <input
          className="input text-xs py-1 w-48"
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
          <table className="w-full min-w-[700px] text-sm">
            <thead>
              <tr className="text-left text-gray-500 border-b border-surface-600">
                <th className="py-2 font-medium">Contato</th>
                <th className="py-2 font-medium">Status</th>
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
