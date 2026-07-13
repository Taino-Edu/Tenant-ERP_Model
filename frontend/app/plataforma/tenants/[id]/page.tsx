'use client'
import { useEffect, useState, useCallback } from 'react'
import { useParams } from 'next/navigation'
import Link from 'next/link'
import {
  platformApi, TenantSummary, TenantStaffDto, TenantCustomerDto, AuditLogDto,
  SupportTicketDto, PagedResult, getErrorMessage,
} from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import toast from 'react-hot-toast'
import { ArrowLeft, Loader2, Users, UserCog, History, LifeBuoy } from 'lucide-react'
import clsx from 'clsx'

function fmtDateTime(iso: string | null) {
  if (!iso) return '—'
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

type Tab = 'staff' | 'clientes' | 'logs' | 'suporte'

const TABS: { key: Tab; label: string; icon: typeof Users }[] = [
  { key: 'staff',    label: 'Funcionários & Admins', icon: UserCog },
  { key: 'clientes', label: 'Clientes',               icon: Users },
  { key: 'logs',     label: 'Logs',                   icon: History },
  { key: 'suporte',  label: 'Suporte',                icon: LifeBuoy },
]

function StaffTab({ tenantId }: { tenantId: string }) {
  const [staff, setStaff] = useState<TenantStaffDto[] | null>(null)

  useEffect(() => {
    platformApi.getTenantStaff(tenantId)
      .then(r => setStaff(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar funcionários')))
  }, [tenantId])

  if (staff === null) return <div className="flex justify-center py-10"><Loader2 className="w-5 h-5 animate-spin text-brand-400" /></div>
  if (staff.length === 0) return <p className="text-gray-400 text-center py-10">Nenhum funcionário/admin cadastrado.</p>

  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="text-left text-gray-500 border-b border-surface-600">
          <th className="py-2 font-medium">Nome</th>
          <th className="py-2 font-medium">E-mail</th>
          <th className="py-2 font-medium">Papel</th>
          <th className="py-2 font-medium">Perfil</th>
          <th className="py-2 font-medium">Último login</th>
        </tr>
      </thead>
      <tbody>
        {staff.map(u => (
          <tr key={u.id} className="border-b border-surface-700 last:border-0">
            <td className="py-2.5 text-white">{u.name} {!u.isActive && <span className="text-xs text-red-400">(inativo)</span>}</td>
            <td className="py-2.5 text-gray-400">{u.email ?? '—'}</td>
            <td className="py-2.5 text-gray-400">{u.role}</td>
            <td className="py-2.5 text-gray-400">{u.perfilNome ?? '—'}</td>
            <td className="py-2.5 text-gray-400">{fmtDateTime(u.lastLoginAt)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function ClientesTab({ tenantId }: { tenantId: string }) {
  const [result, setResult] = useState<PagedResult<TenantCustomerDto> | null>(null)
  const [page, setPage] = useState(1)

  useEffect(() => {
    platformApi.getTenantCustomers(tenantId, page)
      .then(r => setResult(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar clientes')))
  }, [tenantId, page])

  if (result === null) return <div className="flex justify-center py-10"><Loader2 className="w-5 h-5 animate-spin text-brand-400" /></div>
  if (result.items.length === 0) return <p className="text-gray-400 text-center py-10">Nenhum cliente cadastrado.</p>

  return (
    <div className="space-y-3">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left text-gray-500 border-b border-surface-600">
            <th className="py-2 font-medium">Nome</th>
            <th className="py-2 font-medium">E-mail</th>
            <th className="py-2 font-medium">WhatsApp</th>
            <th className="py-2 font-medium">Cadastrado em</th>
          </tr>
        </thead>
        <tbody>
          {result.items.map(c => (
            <tr key={c.id} className="border-b border-surface-700 last:border-0">
              <td className="py-2.5 text-white">{c.name}</td>
              <td className="py-2.5 text-gray-400">{c.email ?? '—'}</td>
              <td className="py-2.5 text-gray-400">{c.whatsApp ?? '—'}</td>
              <td className="py-2.5 text-gray-400">{fmtDateTime(c.createdAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {result.totalPages > 1 && (
        <div className="flex items-center justify-center gap-3 text-sm text-gray-400">
          <button className="btn-secondary text-xs py-1 px-2.5" disabled={!result.hasPrev} onClick={() => setPage(p => p - 1)}>Anterior</button>
          Página {result.page} de {result.totalPages}
          <button className="btn-secondary text-xs py-1 px-2.5" disabled={!result.hasNext} onClick={() => setPage(p => p + 1)}>Próxima</button>
        </div>
      )}
    </div>
  )
}

function LogsTab({ tenantId }: { tenantId: string }) {
  const [result, setResult] = useState<PagedResult<AuditLogDto> | null>(null)

  useEffect(() => {
    platformApi.getTenantAuditLogs(tenantId)
      .then(r => setResult(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar logs')))
  }, [tenantId])

  if (result === null) return <div className="flex justify-center py-10"><Loader2 className="w-5 h-5 animate-spin text-brand-400" /></div>
  if (result.items.length === 0) return <p className="text-gray-400 text-center py-10">Nenhum registro de auditoria ainda.</p>

  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="text-left text-gray-500 border-b border-surface-600">
          <th className="py-2 font-medium">Quando</th>
          <th className="py-2 font-medium">Ator</th>
          <th className="py-2 font-medium">Ação</th>
          <th className="py-2 font-medium">Entidade</th>
        </tr>
      </thead>
      <tbody>
        {result.items.map(a => (
          <tr key={a.id} className="border-b border-surface-700 last:border-0">
            <td className="py-2.5 text-gray-400">{fmtDateTime(a.createdAt)}</td>
            <td className="py-2.5 text-white">{a.actorUserName ?? 'Sistema'}</td>
            <td className="py-2.5 text-gray-400">{a.action}</td>
            <td className="py-2.5 text-gray-400">{a.entityType}{a.entityId ? ` #${a.entityId.slice(0, 8)}` : ''}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function SuporteTab({ tenantId }: { tenantId: string }) {
  const [tickets, setTickets] = useState<SupportTicketDto[] | null>(null)

  useEffect(() => {
    platformApi.listSupportTickets({ tenantId })
      .then(r => setTickets(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar chamados')))
  }, [tenantId])

  if (tickets === null) return <div className="flex justify-center py-10"><Loader2 className="w-5 h-5 animate-spin text-brand-400" /></div>
  if (tickets.length === 0) return <p className="text-gray-400 text-center py-10">Nenhum chamado de suporte desta loja.</p>

  return (
    <div className="divide-y divide-surface-700">
      {tickets.map(t => (
        <Link key={t.id} href={`/plataforma/suporte/${t.id}`} className="flex items-center justify-between py-3 hover:bg-surface-700/30 -mx-2 px-2 rounded-lg">
          <div>
            <p className="text-white font-medium">{t.subject}</p>
            <p className="text-xs text-gray-400">Aberto por {t.createdByUserName} · {fmtDateTime(t.createdAt)}</p>
          </div>
          <span className="text-xs font-medium px-2 py-0.5 rounded-full border border-surface-500 text-gray-300">{t.status}</span>
        </Link>
      ))}
    </div>
  )
}

export default function TenantDetailPage() {
  const params = useParams<{ id: string }>()
  const tenantId = params.id

  const [tenant, setTenant] = useState<TenantSummary | null | undefined>(undefined)
  const [tab, setTab] = useState<Tab>('staff')

  const fetchTenant = useCallback(() => {
    platformApi.listTenants()
      .then(r => setTenant(r.data.find(t => t.id === tenantId) ?? null))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar tenant')))
  }, [tenantId])

  useEffect(() => { fetchTenant() }, [fetchTenant])

  if (tenant === undefined) return <div className="flex justify-center py-16"><Loader2 className="w-6 h-6 animate-spin text-brand-400" /></div>
  if (tenant === null) return <p className="text-gray-400 text-center py-16">Tenant não encontrado.</p>

  return (
    <div className="space-y-5">
      <Link href="/plataforma/tenants" className="inline-flex items-center gap-1.5 text-sm text-gray-400 hover:text-white">
        <ArrowLeft className="w-4 h-4" /> Voltar pra Tenants
      </Link>

      <PageHeader
        icon={UserCog}
        title={tenant.slug}
        description={`${tenant.planName} · ${tenant.paymentStatus} · ${tenant.status === 'Active' ? 'Ativo' : 'Suspenso'}`}
      />

      <div className="card">
        <div className="flex items-center gap-1 border-b border-surface-600 mb-4 overflow-x-auto">
          {TABS.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              onClick={() => setTab(key)}
              className={clsx(
                'flex items-center gap-1.5 px-3 py-2.5 text-sm font-medium border-b-2 whitespace-nowrap transition-colors',
                tab === key ? 'border-brand-400 text-white' : 'border-transparent text-gray-400 hover:text-white',
              )}
            >
              <Icon className="w-4 h-4" /> {label}
            </button>
          ))}
        </div>

        {tab === 'staff'    && <StaffTab tenantId={tenantId} />}
        {tab === 'clientes' && <ClientesTab tenantId={tenantId} />}
        {tab === 'logs'     && <LogsTab tenantId={tenantId} />}
        {tab === 'suporte'  && <SuporteTab tenantId={tenantId} />}
      </div>
    </div>
  )
}
