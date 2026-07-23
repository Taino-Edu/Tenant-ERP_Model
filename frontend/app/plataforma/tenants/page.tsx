'use client'
import { useEffect, useState, useCallback } from 'react'
import { createPortal } from 'react-dom'
import Link from 'next/link'
import { platformApi, TenantSummary, TenantStatus, TenantPaymentStatus, PlatformOverviewDto, getErrorMessage, TENANT_MODULES } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import CreateTenantModal from '@/components/plataforma/CreateTenantModal'
import toast from 'react-hot-toast'
import { Building2, Plus, Loader2, Power, PowerOff, Check, LogIn, ChevronRight, Download, Trash2, X, AlertTriangle } from 'lucide-react'
import clsx from 'clsx'

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

/** "há X min/h/dias" — null = nunca teve atividade registrada. */
function fmtRelative(iso: string | null): { text: string; tone: 'success' | 'warning' | 'danger' } {
  if (!iso) return { text: 'sem atividade', tone: 'danger' }
  const diffMs   = Date.now() - new Date(iso).getTime()
  const diffDays = diffMs / 86_400_000
  const tone: 'success' | 'warning' | 'danger' = diffDays <= 3 ? 'success' : diffDays <= 14 ? 'warning' : 'danger'

  if (diffMs < 0)          return { text: 'agora', tone: 'success' }
  const diffMin = diffMs / 60_000
  if (diffMin < 60)        return { text: `há ${Math.max(1, Math.round(diffMin))} min`, tone }
  const diffH = diffMin / 60
  if (diffH < 24)          return { text: `há ${Math.round(diffH)}h`, tone }
  return { text: `há ${Math.round(diffDays)} dia${diffDays >= 2 ? 's' : ''}`, tone }
}

const ACTIVITY_TONE: Record<'success' | 'warning' | 'danger', string> = {
  success: 'text-emerald-400',
  warning: 'text-amber-400',
  danger:  'text-gray-500',
}

function StatusBadge({ status }: { status: TenantStatus }) {
  const active = status === 'Active'
  return (
    <span className={clsx('text-xs font-medium px-2 py-0.5 rounded-full border',
      active ? 'bg-accent-green/10 text-accent-green border-accent-green/30' : 'bg-red-500/10 text-red-400 border-red-500/30')}>
      {active ? 'Ativo' : 'Suspenso'}
    </span>
  )
}

const PAYMENT_STYLES: Record<TenantPaymentStatus, string> = {
  Pago:     'bg-accent-green/10 text-accent-green border-accent-green/30',
  Atrasado: 'bg-red-500/10 text-red-400 border-red-500/30',
  Isento:   'bg-gray-500/10 text-gray-400 border-gray-500/30',
}

// ── Modal: Apagar Tenant (irreversível — exige digitar o slug de volta) ───────
function DeleteTenantModal({ tenant, onClose, onDeleted }: { tenant: TenantSummary; onClose: () => void; onDeleted: () => void }) {
  const [confirmSlug, setConfirmSlug] = useState('')
  const [loading, setLoading]         = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    try {
      await platformApi.deleteTenant(tenant.id, confirmSlug)
      toast.success(`Tenant "${tenant.slug}" apagado.`)
      onDeleted()
      onClose()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao apagar tenant'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="bg-surface-800 border border-red-500/40 rounded-2xl w-full max-w-sm shadow-2xl">
        <div className="flex items-center justify-between px-6 py-4 border-b border-surface-500">
          <h2 className="font-bold text-white text-lg flex items-center gap-2">
            <AlertTriangle className="w-5 h-5 text-red-400" /> Apagar Tenant
          </h2>
          <button onClick={onClose} className="text-gray-500 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
          <p className="text-sm text-gray-400">
            Isso apaga <strong className="text-white">{tenant.slug}</strong> e todos os seus dados
            (produtos, vendas, clientes) <strong className="text-red-400">permanentemente</strong>. Não dá pra desfazer.
            Considera baixar um backup antes.
          </p>
          <div>
            <label className="label">Digite <code className="text-red-400">{tenant.slug}</code> pra confirmar</label>
            <input
              className="input" value={confirmSlug} onChange={e => setConfirmSlug(e.target.value)}
              placeholder={tenant.slug} required
            />
          </div>
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
            <button
              type="submit" disabled={loading || confirmSlug !== tenant.slug}
              className="flex-1 justify-center inline-flex items-center gap-2 rounded-xl bg-red-600 hover:bg-red-500 disabled:opacity-40 disabled:cursor-not-allowed text-white text-sm font-medium py-2 transition-colors"
            >
              {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />}
              Apagar Definitivamente
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

function TenantRow({ tenant, lastActivityAt, onChanged }: { tenant: TenantSummary; lastActivityAt: string | null | undefined; onChanged: () => void }) {
  const [planName, setPlanName]   = useState(tenant.planName)
  const [savingBilling, setSavingBilling] = useState(false)
  const [updatingStatus, setUpdatingStatus] = useState(false)
  const [impersonating, setImpersonating] = useState(false)
  const [backingUp, setBackingUp] = useState(false)
  const [showDelete, setShowDelete] = useState(false)

  useEffect(() => { setPlanName(tenant.planName) }, [tenant.planName])

  async function saveBilling(next: Partial<{ planName: string; paymentStatus: TenantPaymentStatus; enabledModules: string[] }>) {
    setSavingBilling(true)
    try {
      await platformApi.updateTenantBilling(tenant.id, {
        planName:       next.planName       ?? planName,
        paymentStatus:  next.paymentStatus  ?? tenant.paymentStatus,
        enabledModules: next.enabledModules ?? tenant.enabledModules,
      })
      toast.success('Billing atualizado.')
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao atualizar billing do tenant.'))
    } finally {
      setSavingBilling(false)
    }
  }

  async function toggleStatus() {
    const next: TenantStatus = tenant.status === 'Active' ? 'Suspended' : 'Active'
    setUpdatingStatus(true)
    try {
      await platformApi.updateTenantStatus(tenant.id, next)
      toast.success(next === 'Active' ? 'Tenant reativado.' : 'Tenant suspenso.')
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao atualizar status do tenant.'))
    } finally {
      setUpdatingStatus(false)
    }
  }

  function toggleModule(module: string) {
    const has = tenant.enabledModules.includes(module)
    const nextModules = has
      ? tenant.enabledModules.filter(m => m !== module)
      : [...tenant.enabledModules, module]
    saveBilling({ enabledModules: nextModules })
  }

  async function baixarBackup() {
    setBackingUp(true)
    try {
      const { data } = await platformApi.downloadTenantBackup(tenant.id)
      const url = URL.createObjectURL(data as Blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `backup-${tenant.slug}-${new Date().toISOString().slice(0, 10)}.sql`
      a.click()
      URL.revokeObjectURL(url)
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao gerar backup'))
    } finally {
      setBackingUp(false)
    }
  }

  async function acessarAdmin() {
    setImpersonating(true)
    try {
      const { data } = await platformApi.impersonate(tenant.id)
      const rootDomain = process.env.NEXT_PUBLIC_ROOT_DOMAIN
      const url = `${window.location.protocol}//${tenant.slug}.${rootDomain}/api/auth/impersonate?ticket=${encodeURIComponent(data.ticket)}`
      window.open(url, '_blank', 'noopener')
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao gerar acesso de simulação.'))
    } finally {
      setImpersonating(false)
    }
  }

  return (
    <tr className="border-b border-surface-700 last:border-0">
      <td className="py-3">
        <Link href={`/plataforma/tenants/${tenant.id}`} className="text-white font-medium hover:text-brand-400 flex items-center gap-1">
          {tenant.slug} <ChevronRight className="w-3.5 h-3.5 text-gray-500" />
        </Link>
      </td>
      <td className="py-3"><StatusBadge status={tenant.status} /></td>
      <td className="py-3">
        <div className="flex items-center gap-1.5">
          <input
            className="input text-xs py-1 w-28"
            value={planName}
            onChange={e => setPlanName(e.target.value)}
            onBlur={() => { if (planName.trim() && planName !== tenant.planName) saveBilling({ planName: planName.trim() }) }}
            disabled={savingBilling}
          />
        </div>
      </td>
      <td className="py-3">
        <select
          className="input text-xs py-1"
          value={tenant.paymentStatus}
          disabled={savingBilling}
          onChange={e => saveBilling({ paymentStatus: e.target.value as TenantPaymentStatus })}
        >
          <option value="Pago">Pago</option>
          <option value="Atrasado">Atrasado</option>
          <option value="Isento">Isento</option>
        </select>
        <span className={clsx('ml-2 text-xs font-medium px-2 py-0.5 rounded-full border', PAYMENT_STYLES[tenant.paymentStatus])}>
          {tenant.paymentStatus}
        </span>
      </td>
      <td className="py-3">
        <div className="flex flex-wrap gap-1.5">
          {TENANT_MODULES.map(({ value: module, label }) => (
            <button
              key={module}
              type="button"
              onClick={() => toggleModule(module)}
              disabled={savingBilling}
              title={TENANT_MODULES.find(m => m.value === module)?.description}
              className={clsx('flex items-center gap-1.5 text-xs font-medium px-2 py-1 rounded-lg border transition-colors',
                tenant.enabledModules.includes(module)
                  ? 'bg-brand-600/10 border-brand-500/40 text-brand-300'
                  : 'border-surface-500 text-gray-500 hover:border-surface-400 hover:text-gray-300')}
            >
              <span className={clsx('w-3.5 h-3.5 rounded border flex items-center justify-center shrink-0',
                tenant.enabledModules.includes(module) ? 'bg-brand-500 border-brand-500' : 'border-surface-400')}>
                {tenant.enabledModules.includes(module) && <Check className="w-2.5 h-2.5 text-white" />}
              </span>
              {label}
            </button>
          ))}
        </div>
      </td>
      <td className="py-3 text-gray-400">{fmtDate(tenant.createdAt)}</td>
      <td className="py-3">
        {(() => {
          const activity = fmtRelative(lastActivityAt ?? null)
          return <span className={clsx('text-xs font-medium', ACTIVITY_TONE[activity.tone])}>{activity.text}</span>
        })()}
      </td>
      <td className="py-3 text-right">
        <div className="flex items-center justify-end gap-2">
          <button
            onClick={acessarAdmin}
            disabled={impersonating || tenant.status !== 'Active'}
            title={tenant.status !== 'Active' ? 'Reative o tenant para acessar' : 'Acessar o admin desta loja'}
            className="btn-secondary text-xs py-1 px-2.5 disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {impersonating ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <LogIn className="w-3.5 h-3.5" />}
            Acessar admin
          </button>
          <button
            onClick={toggleStatus}
            disabled={updatingStatus}
            className={clsx('btn-secondary text-xs py-1 px-2.5',
              tenant.status === 'Active' ? 'hover:text-red-400' : 'hover:text-accent-green')}
          >
            {updatingStatus
              ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
              : tenant.status === 'Active' ? <PowerOff className="w-3.5 h-3.5" /> : <Power className="w-3.5 h-3.5" />}
            {tenant.status === 'Active' ? 'Suspender' : 'Reativar'}
          </button>
          <button
            onClick={baixarBackup}
            disabled={backingUp}
            title="Baixar backup (.sql) desta loja"
            className="btn-secondary text-xs py-1 px-2.5"
          >
            {backingUp ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Download className="w-3.5 h-3.5" />}
            Backup
          </button>
          <button
            onClick={() => setShowDelete(true)}
            title="Apagar esta loja permanentemente"
            className="text-xs py-1 px-2.5 inline-flex items-center gap-1.5 rounded-lg border border-red-500/30 text-red-400 hover:bg-red-500/10 transition-colors"
          >
            <Trash2 className="w-3.5 h-3.5" /> Apagar
          </button>
        </div>
      </td>
      {showDelete && createPortal(
        <DeleteTenantModal tenant={tenant} onClose={() => setShowDelete(false)} onDeleted={onChanged} />,
        document.body,
      )}
    </tr>
  )
}

export default function PlataformaTenantsPage() {
  const [tenants, setTenants] = useState<TenantSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [showCreate, setShowCreate] = useState(false)
  const [overview, setOverview] = useState<PlatformOverviewDto | null>(null)

  const fetchTenants = useCallback(() => {
    setLoading(true)
    platformApi.listTenants()
      .then(r => setTenants(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar tenants')))
      .finally(() => setLoading(false))
  }, [])

  const fetchOverview = useCallback(() => {
    // Falha aqui não pode derrubar a tabela de tenants — só a coluna de
    // atividade fica vazia (overview permanece null).
    platformApi.getOverview()
      .then(r => setOverview(r.data))
      .catch(() => {})
  }, [])

  useEffect(() => { fetchTenants() }, [fetchTenants])
  useEffect(() => { fetchOverview() }, [fetchOverview])

  const activityByTenant = new Map((overview?.tenants ?? []).map(t => [t.tenantId, t.lastActivityAt]))

  return (
    <div className="space-y-5">
      <PageHeader
        icon={Building2}
        title="Tenants"
        description="Lojas cadastradas na plataforma"
        actions={
          <button onClick={() => setShowCreate(true)} className="btn-primary text-sm py-1.5">
            <Plus className="w-4 h-4" /> Cadastrar Tenant
          </button>
        }
      />

      <div className="card overflow-x-auto">
        {loading ? (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="w-6 h-6 animate-spin text-brand-400" />
          </div>
        ) : tenants.length === 0 ? (
          <p className="text-gray-400 text-center py-16">Nenhum tenant cadastrado ainda.</p>
        ) : (
          <table className="w-full min-w-[760px] text-sm">
            <thead>
              <tr className="text-left text-gray-500 border-b border-surface-600">
                <th className="py-2 font-medium">Slug</th>
                <th className="py-2 font-medium">Status</th>
                <th className="py-2 font-medium">Plano</th>
                <th className="py-2 font-medium">Pagamento</th>
                <th className="py-2 font-medium">Módulos</th>
                <th className="py-2 font-medium">Criado em</th>
                <th className="py-2 font-medium">Última atividade</th>
                <th className="py-2 font-medium text-right">Ações</th>
              </tr>
            </thead>
            <tbody>
              {tenants.map(t => (
                <TenantRow key={t.id} tenant={t} lastActivityAt={activityByTenant.get(t.id)} onChanged={fetchTenants} />
              ))}
            </tbody>
          </table>
        )}
      </div>

      {showCreate && <CreateTenantModal onClose={() => setShowCreate(false)} onCreated={fetchTenants} />}
    </div>
  )
}
