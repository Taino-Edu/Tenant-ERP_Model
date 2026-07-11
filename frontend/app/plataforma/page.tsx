'use client'
import { useEffect, useState, useCallback } from 'react'
import { platformApi, TenantSummary, TenantStatus } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import toast from 'react-hot-toast'
import { Building2, Plus, Loader2, X, Power, PowerOff } from 'lucide-react'
import clsx from 'clsx'

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' })
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

// ── Modal: cadastrar tenant ────────────────────────────────────────────────────

function CreateTenantModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [slug, setSlug]         = useState('')
  const [email, setEmail]       = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading]   = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    try {
      await platformApi.createTenant({ slug: slug.trim().toLowerCase(), adminEmail: email.trim(), adminPassword: password })
      toast.success(`Tenant "${slug}" criado com sucesso!`)
      onCreated()
      onClose()
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao criar tenant.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="bg-surface-800 border border-surface-500 rounded-2xl w-full max-w-md shadow-2xl">
        <div className="flex items-center justify-between px-6 py-4 border-b border-surface-500">
          <h2 className="font-bold text-white text-lg flex items-center gap-2">
            <Building2 className="w-5 h-5 text-brand-400" /> Cadastrar Tenant
          </h2>
          <button onClick={onClose} className="text-gray-500 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
          <div>
            <label className="label">Slug (subdomínio) *</label>
            <input
              className="input" placeholder="loja-exemplo" value={slug}
              onChange={e => setSlug(e.target.value)} required maxLength={20}
            />
            <p className="text-xs text-gray-400 mt-1">Só letras minúsculas, números e hífen — ficará em &lt;slug&gt;.2esysten.com.br</p>
          </div>
          <div>
            <label className="label">E-mail do admin da loja *</label>
            <input
              type="email" className="input" placeholder="dono@loja.com" value={email}
              onChange={e => setEmail(e.target.value)} required
            />
          </div>
          <div>
            <label className="label">Senha inicial *</label>
            <input
              type="password" className="input" placeholder="Mínimo 6 caracteres" value={password}
              onChange={e => setPassword(e.target.value)} required minLength={6}
            />
          </div>
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1 justify-center">Cancelar</button>
            <button type="submit" disabled={loading} className="btn-primary flex-1 justify-center">
              {loading ? <><Loader2 className="w-4 h-4 animate-spin" /> Criando...</> : <><Plus className="w-4 h-4" /> Criar Tenant</>}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── Página principal ───────────────────────────────────────────────────────────

export default function PlataformaPage() {
  const [tenants, setTenants] = useState<TenantSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [showCreate, setShowCreate] = useState(false)
  const [updatingId, setUpdatingId] = useState<string | null>(null)

  const fetchTenants = useCallback(() => {
    setLoading(true)
    platformApi.listTenants()
      .then(r => setTenants(r.data))
      .catch(() => toast.error('Erro ao carregar tenants'))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => { fetchTenants() }, [fetchTenants])

  async function toggleStatus(tenant: TenantSummary) {
    const next: TenantStatus = tenant.status === 'Active' ? 'Suspended' : 'Active'
    setUpdatingId(tenant.id)
    try {
      await platformApi.updateTenantStatus(tenant.id, next)
      toast.success(next === 'Active' ? 'Tenant reativado.' : 'Tenant suspenso.')
      fetchTenants()
    } catch {
      toast.error('Erro ao atualizar status do tenant.')
    } finally {
      setUpdatingId(null)
    }
  }

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
          <table className="w-full min-w-[560px] text-sm">
            <thead>
              <tr className="text-left text-gray-500 border-b border-surface-600">
                <th className="py-2 font-medium">Slug</th>
                <th className="py-2 font-medium">Status</th>
                <th className="py-2 font-medium">Criado em</th>
                <th className="py-2 font-medium text-right">Ações</th>
              </tr>
            </thead>
            <tbody>
              {tenants.map(t => (
                <tr key={t.id} className="border-b border-surface-700 last:border-0">
                  <td className="py-3 text-white font-medium">{t.slug}</td>
                  <td className="py-3"><StatusBadge status={t.status} /></td>
                  <td className="py-3 text-gray-400">{fmtDate(t.createdAt)}</td>
                  <td className="py-3 text-right">
                    <button
                      onClick={() => toggleStatus(t)}
                      disabled={updatingId === t.id}
                      className={clsx('btn-secondary text-xs py-1 px-2.5 ml-auto',
                        t.status === 'Active' ? 'hover:text-red-400' : 'hover:text-accent-green')}
                    >
                      {updatingId === t.id
                        ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                        : t.status === 'Active' ? <PowerOff className="w-3.5 h-3.5" /> : <Power className="w-3.5 h-3.5" />}
                      {t.status === 'Active' ? 'Suspender' : 'Reativar'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {showCreate && <CreateTenantModal onClose={() => setShowCreate(false)} onCreated={fetchTenants} />}
    </div>
  )
}
