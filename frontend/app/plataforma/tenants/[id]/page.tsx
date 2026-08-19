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
import DataTable from '@/components/admin/ui/DataTable'
import { AuditLogDetailModal } from '@/components/admin/AuditLogDetailModal'
import { usePlatformPermissions } from '@/hooks/usePlatformPermissions'

function fmtDateTime(iso: string | null) {
  if (!iso) return '—'
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

type Tab = 'staff' | 'clientes' | 'logs' | 'suporte' | 'uso'

// Cada aba carrega de um endpoint diferente, com permissão própria: logs e
// suporte não vêm junto com `tenants.read`. Sem esse recorte, um perfil
// Comercial abria as duas abas só pra ver o erro de carregamento.
const TABS: { key: Tab; label: string; icon: typeof Users; permission: string }[] = [
  { key: 'staff',    label: 'Funcionários & Admins', icon: UserCog,   permission: 'platform.tenants.read' },
  { key: 'clientes', label: 'Clientes',               icon: Users,     permission: 'platform.tenants.read' },
  { key: 'logs',     label: 'Logs',                   icon: History,   permission: 'platform.logs' },
  { key: 'suporte',  label: 'Suporte',                icon: LifeBuoy,  permission: 'platform.support' },
  { key: 'uso',      label: 'Uso',                     icon: BarChart2, permission: 'platform.tenants.read' },
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
  '/admin/ia-config':      'Assistente de IA',
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

function StaffTab({ tenantId, podeRedefinirSenha }: { tenantId: string; podeRedefinirSenha: boolean }) {
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
      <DataTable
        rows={staff}
        rowKey={u => u.id}
        rowActions={podeRedefinirSenha ? u => (
          <button
            onClick={() => setResetTarget(u)}
            className="inline-flex items-center gap-1 py-2 text-xs text-brand-400 hover:text-brand-300"
          >
            <KeyRound className="w-3.5 h-3.5" /> Redefinir Senha
          </button>
        ) : undefined}
        columns={[
          { key: 'nome', header: 'Nome', mobile: 'title', className: 'text-white',
            cell: u => <>{u.name} {!u.isActive && <span className="text-xs text-red-400">(inativo)</span>}</> },
          { key: 'papel', header: 'Papel', mobile: 'trailing', className: 'text-gray-400',
            cell: u => <span className="text-xs font-normal text-gray-400">{u.role}</span> },
          { key: 'email', header: 'E-mail', mobile: 'field', className: 'text-gray-400', cell: u => u.email ?? '—' },
          { key: 'perfil', header: 'Perfil', mobile: 'field', className: 'text-gray-400', cell: u => u.perfilNome ?? '—' },
          { key: 'login', header: 'Último login', mobile: 'field', className: 'text-gray-400', cell: u => fmtDateTime(u.lastLoginAt) },
        ]}
      />
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
      <DataTable
        rows={result.items}
        rowKey={c => c.id}
        columns={[
          { key: 'nome', header: 'Nome', mobile: 'title', className: 'text-white', cell: c => c.name },
          { key: 'email', header: 'E-mail', mobile: 'field', className: 'text-gray-400', cell: c => c.email ?? '—' },
          { key: 'whats', header: 'WhatsApp', mobile: 'field', className: 'text-gray-400', cell: c => c.whatsApp ?? '—' },
          { key: 'criado', header: 'Cadastrado em', mobile: 'field', className: 'text-gray-400', cell: c => fmtDateTime(c.createdAt) },
        ]}
      />
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
      {/* Mesmos papéis do feed de auditoria da plataforma (/plataforma/logs) —
          as duas telas mostram o mesmo tipo de registro e devem ler igual. */}
      <DataTable
        rows={result.items}
        rowKey={a => a.id}
        onRowClick={setViewingLog}
        columns={[
          { key: 'acao', header: 'Ação', mobile: 'title', className: 'text-gray-400', cell: a => a.action },
          { key: 'severidade', header: 'Severidade', mobile: 'trailing', cell: a => <SeverityBadge severity={a.severity} /> },
          { key: 'quando', header: 'Quando', mobile: 'meta', className: 'text-gray-400 whitespace-nowrap', cell: a => fmtDateTime(a.createdAt) },
          { key: 'ator', header: 'Ator', mobile: 'field', className: 'text-white', cell: a => a.actorUserName ?? 'Sistema' },
          { key: 'entidade', header: 'Entidade', mobile: 'field', className: 'text-gray-400',
            cell: a => `${a.entityType}${a.entityId ? ` #${a.entityId.slice(0, 8)}` : ''}` },
          { key: 'resumo', header: 'Resumo', mobile: 'field', className: 'text-gray-400 max-w-[220px] truncate',
            cell: a => summarizeAuditDetails(a.details) },
          { key: 'abrir', header: '', mobile: 'hidden', className: 'text-gray-500', headerClassName: 'w-8',
            cell: () => <Eye className="w-3.5 h-3.5" /> },
        ]}
      />

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

function CustomDomainCard({ tenant, onSaved, podeEditar }: { tenant: TenantSummary; onSaved: (t: TenantSummary) => void; podeEditar: boolean }) {
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
          {/* O domínio em si continua visível — só a edição depende de
              `tenants.manage`, que é o que PATCH /tenants/{id}/domain exige. */}
          {podeEditar && (
            <button onClick={() => setEditing(true)} className="btn-secondary shrink-0 text-xs px-3 py-1.5">
              {tenant.customDomain ? 'Editar' : 'Configurar'}
            </button>
          )}
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
  const pode = usePlatformPermissions()
  const podeGerenciar = pode('platform.tenants.manage')
  const abasVisiveis = TABS.filter(({ permission }) => pode(permission))
  // A aba escolhida pode não existir pro perfil (o estado inicial é 'staff', e
  // a lista chega um render depois). Cai na primeira liberada, ou em nenhuma.
  const abaAtiva = abasVisiveis.some(({ key }) => key === tab) ? tab : abasVisiveis[0]?.key

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

      <CustomDomainCard tenant={tenant} onSaved={setTenant} podeEditar={podeGerenciar} />

      {/* `card-sm-up`: as abas Funcionários/Clientes/Logs renderizam listas de
          cards no celular — com o `.card` aqui elas ficavam card dentro de card
          (301px úteis de 375px). A barra de abas continua igual. */}
      <div className="card-sm-up">
        <div className="chip-row items-center border-b border-surface-600 mb-4 !gap-1">
          {abasVisiveis.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              onClick={() => setTab(key)}
              className={clsx(
                'flex items-center gap-1.5 px-3 py-2.5 text-sm font-medium border-b-2 whitespace-nowrap transition-colors',
                abaAtiva === key ? 'border-brand-400 text-white' : 'border-transparent text-gray-400 hover:text-white',
              )}
            >
              <Icon className="w-4 h-4" /> {label}
            </button>
          ))}
        </div>

        {/* `abaAtiva` e não `tab`: enquanto o cookie de permissões não é lido
            (primeiro render, antes do efeito) nenhuma aba está liberada, e
            renderizar o conteúdo de staff ali dispararia uma chamada que o
            perfil talvez nem possa fazer. */}
        {abaAtiva === 'staff'    && <StaffTab tenantId={tenantId} podeRedefinirSenha={podeGerenciar} />}
        {abaAtiva === 'clientes' && <ClientesTab tenantId={tenantId} />}
        {abaAtiva === 'logs'     && <LogsTab tenantId={tenantId} />}
        {abaAtiva === 'suporte'  && <SuporteTab tenantId={tenantId} />}
        {abaAtiva === 'uso'      && <UsoTab tenantId={tenantId} />}
      </div>
    </div>
  )
}
