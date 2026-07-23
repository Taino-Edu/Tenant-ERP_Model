'use client'
import { useEffect, useState, useCallback } from 'react'
import { useParams } from 'next/navigation'
import Link from 'next/link'
import {
  platformApi, TenantSummary, TenantStaffDto, TenantCustomerDto, AuditLogDto,
  SupportTicketDto, PagedResult, TenantUsageDto, getErrorMessage,
} from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import toast from 'react-hot-toast'
import { ArrowLeft, Loader2, Users, UserCog, History, LifeBuoy, Eye, BarChart2, Globe, Check, X, KeyRound } from 'lucide-react'
import clsx from 'clsx'
import { summarizeAuditDetails } from '@/lib/auditFormat'
import SeverityBadge from '@/components/admin/SeverityBadge'
import { AuditLogDetailModal } from '@/components/admin/AuditLogDetailModal'

function fmtDateTime(iso: string | null) {
  if (!iso) return '—'
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

type Tab = 'staff' | 'clientes' | 'logs' | 'suporte' | 'uso'

const TABS: { key: Tab; label: string; icon: typeof Users }[] = [
  { key: 'staff',    label: 'Funcionários & Admins', icon: UserCog },
  { key: 'clientes', label: 'Clientes',               icon: Users },
  { key: 'logs',     label: 'Logs',                   icon: History },
  { key: 'suporte',  label: 'Suporte',                icon: LifeBuoy },
  { key: 'uso',      label: 'Uso',                     icon: BarChart2 },
]

const PATH_LABELS: Record<string, string> = {
  '/admin/comanda':        'Comanda',
  '/admin/dashboard':      'Painel Geral',
  '/admin/venda-avulsa':   'Frente de Caixa',
  '/admin/qrcodes':        'Gatilhos QR Code',
  '/admin/estoque':        'Estoque',
  '/admin/usuarios':       'Clientes',
  '/admin/crediario':      'Crediário',
  '/admin/reservas':       'Pré-vendas',
  '/admin/financeiro':     'Financeiro',
  '/admin/contas-receber': 'Contas a Pagar/Receber',
  '/admin/relatorios':     'Relatórios',
  '/admin/anuncios':       'Anúncios',
  '/admin/mensageria':     'Mensageria',
  '/admin/fiscal':         'Fiscal',
  '/admin/lgpd':           'LGPD & Auditoria',
  '/admin/perfis':         'Perfis de Acesso',
  '/admin/site':           'Personalizar Site',
  '/admin/email':          'E-mail',
  '/admin/suporte':        'Suporte',
}

function pathLabel(path: string) {
  return PATH_LABELS[path] ?? path
}

function ResetStaffPasswordModal({ tenantId, user, onClose }: { tenantId: string; user: TenantStaffDto; onClose: () => void }) {
  const [senha, setSenha]       = useState('')
  const [confirma, setConfirma] = useState('')
  const [loading, setLoading]   = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (senha.length < 8) { toast.error('Mínimo 8 caracteres'); return }
    if (senha !== confirma) { toast.error('As senhas não coincidem'); return }
    setLoading(true)
    try {
      await platformApi.resetTenantStaffPassword(tenantId, user.id, senha)
      toast.success(`Senha de ${user.name} redefinida!`)
      onClose()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao redefinir senha'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-sm shadow-2xl">
        <div className="flex items-center justify-between px-6 py-4 border-b border-surface-500">
          <h2 className="font-bold text-white text-lg flex items-center gap-2">
            <KeyRound className="w-5 h-5 text-brand-400" /> Redefinir Senha
          </h2>
          <button onClick={onClose} className="text-gray-500 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
          <p className="text-sm text-gray-400">Definindo nova senha para <strong className="text-white">{user.name}</strong> ({user.role}).</p>
          <div>
            <label className="label">Nova senha</label>
            <input type="password" className="input" placeholder="Mínimo 8 caracteres" value={senha} onChange={e => setSenha(e.target.value)} required minLength={8} />
          </div>
          <div>
            <label className="label">Confirmar senha</label>
            <input type="password" className="input" placeholder="Repita a senha" value={confirma} onChange={e => setConfirma(e.target.value)} required minLength={8} />
          </div>
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
            <button type="submit" disabled={loading} className="btn-primary flex-1 justify-center">
              {loading ? <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</> : <><KeyRound className="w-4 h-4" /> Redefinir</>}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

function StaffTab({ tenantId }: { tenantId: string }) {
  const [staff, setStaff] = useState<TenantStaffDto[] | null>(null)
  const [resetTarget, setResetTarget] = useState<TenantStaffDto | null>(null)

  useEffect(() => {
    platformApi.getTenantStaff(tenantId)
      .then(r => setStaff(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar funcionários')))
  }, [tenantId])

  if (staff === null) return <div className="flex justify-center py-10"><Loader2 className="w-5 h-5 animate-spin text-brand-400" /></div>
  if (staff.length === 0) return <p className="text-gray-400 text-center py-10">Nenhum funcionário/admin cadastrado.</p>

  return (
    <>
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left text-gray-500 border-b border-surface-600">
            <th className="py-2 font-medium">Nome</th>
            <th className="py-2 font-medium">E-mail</th>
            <th className="py-2 font-medium">Papel</th>
            <th className="py-2 font-medium">Perfil</th>
            <th className="py-2 font-medium">Último login</th>
            <th className="py-2 font-medium"></th>
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
              <td className="py-2.5 text-right">
                <button
                  onClick={() => setResetTarget(u)}
                  className="inline-flex items-center gap-1 text-xs text-brand-400 hover:text-brand-300"
                >
                  <KeyRound className="w-3.5 h-3.5" /> Redefinir Senha
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {resetTarget && (
        <ResetStaffPasswordModal tenantId={tenantId} user={resetTarget} onClose={() => setResetTarget(null)} />
      )}
    </>
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
  const [viewingLog, setViewingLog] = useState<AuditLogDto | null>(null)

  useEffect(() => {
    platformApi.getTenantAuditLogs(tenantId)
      .then(r => setResult(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar logs')))
  }, [tenantId])

  if (result === null) return <div className="flex justify-center py-10"><Loader2 className="w-5 h-5 animate-spin text-brand-400" /></div>
  if (result.items.length === 0) return <p className="text-gray-400 text-center py-10">Nenhum registro de auditoria ainda.</p>

  return (
    <>
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left text-gray-500 border-b border-surface-600">
            <th className="py-2 font-medium">Quando</th>
            <th className="py-2 font-medium">Ator</th>
            <th className="py-2 font-medium">Ação</th>
            <th className="py-2 font-medium">Entidade</th>
            <th className="py-2 font-medium">Resumo</th>
            <th className="py-2 font-medium">Severidade</th>
            <th className="w-8"></th>
          </tr>
        </thead>
        <tbody>
          {result.items.map(a => (
            <tr
              key={a.id}
              onClick={() => setViewingLog(a)}
              className="border-b border-surface-700 last:border-0 hover:bg-surface-700/40 transition-colors cursor-pointer"
            >
              <td className="py-2.5 text-gray-400 whitespace-nowrap">{fmtDateTime(a.createdAt)}</td>
              <td className="py-2.5 text-white">{a.actorUserName ?? 'Sistema'}</td>
              <td className="py-2.5 text-gray-400">{a.action}</td>
              <td className="py-2.5 text-gray-400">{a.entityType}{a.entityId ? ` #${a.entityId.slice(0, 8)}` : ''}</td>
              <td className="py-2.5 text-gray-400 max-w-[220px] truncate">{summarizeAuditDetails(a.details)}</td>
              <td className="py-2.5"><SeverityBadge severity={a.severity} /></td>
              <td className="py-2.5 text-gray-500"><Eye className="w-3.5 h-3.5" /></td>
            </tr>
          ))}
        </tbody>
      </table>

      {viewingLog && (
        <AuditLogDetailModal log={viewingLog} onClose={() => setViewingLog(null)} />
      )}
    </>
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

function UsoTab({ tenantId }: { tenantId: string }) {
  const [usage, setUsage] = useState<TenantUsageDto | null>(null)
  const [dias, setDias] = useState(7)

  useEffect(() => {
    const de = new Date(Date.now() - dias * 24 * 60 * 60 * 1000).toISOString()
    platformApi.getTenantUsage(tenantId, de)
      .then(r => setUsage(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar uso')))
  }, [tenantId, dias])

  if (usage === null) return <div className="flex justify-center py-10"><Loader2 className="w-5 h-5 animate-spin text-brand-400" /></div>

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end gap-2 text-sm">
        {[7, 30].map(d => (
          <button
            key={d}
            onClick={() => { setUsage(null); setDias(d) }}
            className={clsx(
              'px-2.5 py-1 rounded-lg border text-xs font-medium',
              dias === d ? 'border-brand-400 text-white bg-brand-500/10' : 'border-surface-600 text-gray-400',
            )}
          >
            Últimos {d} dias
          </button>
        ))}
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div className="card p-4">
          <p className="text-xs text-gray-400">Horas de uso no período</p>
          <p className="text-2xl font-black text-white mt-1">{usage.totalHoras.toFixed(1)}h</p>
        </div>
        <div className="card p-4">
          <p className="text-xs text-gray-400">Usuários ativos</p>
          <p className="text-2xl font-black text-white mt-1">{usage.usuariosAtivos}</p>
        </div>
      </div>

      {usage.topPaths.length === 0 ? (
        <p className="text-gray-400 text-center py-10">Nenhum uso registrado neste período.</p>
      ) : (
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-gray-500 border-b border-surface-600">
              <th className="py-2 font-medium">Tela</th>
              <th className="py-2 font-medium">Tempo</th>
              <th className="py-2 font-medium">Visitas</th>
            </tr>
          </thead>
          <tbody>
            {usage.topPaths.map(p => (
              <tr key={p.path} className="border-b border-surface-700 last:border-0">
                <td className="py-2.5 text-white">{pathLabel(p.path)}</td>
                <td className="py-2.5 text-gray-400">{p.horas.toFixed(1)}h</td>
                <td className="py-2.5 text-gray-400">{p.visitas}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

function CustomDomainCard({ tenant, onSaved }: { tenant: TenantSummary; onSaved: (t: TenantSummary) => void }) {
  const [editing, setEditing] = useState(false)
  const [value, setValue]     = useState(tenant.customDomain ?? '')
  const [saving, setSaving]   = useState(false)

  async function salvar(novoValor: string | null) {
    setSaving(true)
    try {
      const { data } = await platformApi.updateTenantDomain(tenant.id, novoValor)
      onSaved(data)
      setEditing(false)
      toast.success(novoValor ? 'Domínio próprio salvo!' : 'Domínio próprio removido.')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao salvar domínio'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="card">
      <div className="flex items-center gap-2 mb-1">
        <Globe className="w-4 h-4 text-brand-400" />
        <h2 className="text-sm font-bold text-white">Domínio próprio (BYO domain)</h2>
      </div>

      {!editing ? (
        <div className="flex items-center justify-between gap-3 mt-2">
          <p className="text-sm text-gray-300">
            {tenant.customDomain
              ? <>Ativo em <span className="font-mono text-white">{tenant.customDomain}</span> (além de <span className="font-mono">{tenant.slug}.2esysten.com.br</span>)</>
              : <>Nenhum — só <span className="font-mono">{tenant.slug}.2esysten.com.br</span> funciona hoje.</>}
          </p>
          <button onClick={() => setEditing(true)} className="btn-secondary shrink-0 text-xs px-3 py-1.5">
            {tenant.customDomain ? 'Editar' : 'Configurar'}
          </button>
        </div>
      ) : (
        <div className="mt-2 space-y-3">
          <input
            className="input" placeholder="minhaloja.com.br" value={value}
            onChange={e => setValue(e.target.value)}
          />
          <p className="text-xs text-gray-400">
            Não emitimos certificado TLS automaticamente. O lojista precisa colocar o domínio dele
            atrás da própria conta Cloudflare (grátis), modo <span className="font-medium">Flexible</span>,
            apontando (A/CNAME) pra <span className="font-mono">179.197.67.64</span> — mesmo esquema que
            <span className="font-mono"> 2esysten.com.br</span> já usa.
          </p>
          <div className="flex gap-2">
            <button onClick={() => setEditing(false)} className="btn-secondary text-xs px-3 py-1.5">Cancelar</button>
            {tenant.customDomain && (
              <button onClick={() => salvar(null)} disabled={saving} className="flex items-center gap-1 text-xs px-3 py-1.5 rounded-lg border border-red-500/40 text-red-400 hover:bg-red-500/10">
                <X className="w-3.5 h-3.5" /> Remover
              </button>
            )}
            <button onClick={() => salvar(value.trim())} disabled={saving || !value.trim()} className="btn-primary text-xs px-3 py-1.5 ml-auto">
              {saving ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Check className="w-3.5 h-3.5" />} Salvar
            </button>
          </div>
        </div>
      )}
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

      <CustomDomainCard tenant={tenant} onSaved={setTenant} />

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
        {tab === 'uso'      && <UsoTab tenantId={tenantId} />}
      </div>
    </div>
  )
}
