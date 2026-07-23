'use client'
import { useState } from 'react'
import { platformApi, getErrorMessage, TENANT_MODULES, TENANT_PLAN_PRESETS } from '@/lib/api'
import toast from 'react-hot-toast'
import { Building2, Plus, Loader2, X, Check } from 'lucide-react'
import clsx from 'clsx'

export default function CreateTenantModal({
  onClose, onCreated, initialSlug = '', initialEmail = '',
}: {
  onClose: () => void
  onCreated: (tenantId: string) => void
  initialSlug?: string
  initialEmail?: string
}) {
  const [slug, setSlug]         = useState(initialSlug)
  const [email, setEmail]       = useState(initialEmail)
  const [password, setPassword] = useState('')
  // Fiscal vem marcado por padrão — mesmo default que o backend já aplicava
  // antes desse seletor existir (Tenant.EnabledModules = ["fiscal"]).
  const [modules, setModules]   = useState<string[]>(['fiscal'])
  const [planName, setPlanName] = useState('Personalizado')
  const [maxUsers, setMaxUsers] = useState<number | ''>('')
  const [loading, setLoading]   = useState(false)

  function applyPreset(preset: typeof TENANT_PLAN_PRESETS[number]) {
    setPlanName(preset.name)
    setModules([...preset.modules])
    setMaxUsers(preset.maxUsers ?? '')
  }

  // Qualquer ajuste manual depois de escolher um preset deixa de ser
  // exatamente aquele plano — evita mandar "Mar"/"Lagoa" pro backend quando
  // os módulos já não batem mais com o preset original.
  function toggleModule(value: string) {
    setModules(prev => prev.includes(value) ? prev.filter(m => m !== value) : [...prev, value])
    setPlanName('Personalizado')
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    try {
      const { data } = await platformApi.createTenant({
        slug: slug.trim().toLowerCase(), adminEmail: email.trim(), adminPassword: password,
        enabledModules: modules, planName,
        maxUsers: maxUsers === '' ? null : maxUsers,
      })
      toast.success(`Tenant "${slug}" criado com sucesso!`)
      onCreated(data.id)
      onClose()
    } catch (err) {
      toast.error(getErrorMessage(err, 'Erro ao criar tenant.'))
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
          <div>
            <label className="label">Plano</label>
            <div className="flex gap-2 mt-1">
              {TENANT_PLAN_PRESETS.map(preset => (
                <button
                  key={preset.name} type="button" onClick={() => applyPreset(preset)}
                  className={clsx(
                    'flex-1 px-3 py-2 rounded-lg border text-left transition-colors',
                    planName === preset.name ? 'border-brand-500 bg-brand-600/10' : 'border-surface-500 hover:border-surface-400',
                  )}
                >
                  <span className="block text-sm font-bold text-white">{preset.name}</span>
                  <span className="block text-xs text-gray-400">{preset.description}</span>
                </button>
              ))}
            </div>
            {planName === 'Personalizado' && (
              <p className="text-xs text-gray-500 mt-1">Nenhum preset selecionado — ajuste os módulos manualmente abaixo.</p>
            )}
          </div>
          <div>
            <label className="label">Módulos habilitados</label>
            <div className="space-y-1.5 mt-1">
              {TENANT_MODULES.map(m => (
                <button
                  key={m.value} type="button" onClick={() => toggleModule(m.value)}
                  className="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg border border-surface-500 hover:border-surface-400 text-left transition-colors"
                >
                  <span className={clsx('w-4 h-4 rounded border flex items-center justify-center shrink-0',
                    modules.includes(m.value) ? 'bg-brand-500 border-brand-500' : 'border-surface-400')}>
                    {modules.includes(m.value) && <Check className="w-2.5 h-2.5 text-white" />}
                  </span>
                  <span className="min-w-0">
                    <span className="block text-sm text-white">{m.label}</span>
                    <span className="block text-xs text-gray-400 truncate">{m.description}</span>
                  </span>
                </button>
              ))}
            </div>
            <p className="text-xs text-gray-400 mt-1">Dá pra mudar depois na tela da loja.</p>
          </div>
          <div>
            <label className="label">Limite de usuários com acesso ao painel</label>
            <input
              type="number" min={1} className="input" placeholder="Sem limite"
              value={maxUsers}
              onChange={e => { setMaxUsers(e.target.value === '' ? '' : Number(e.target.value)); setPlanName('Personalizado') }}
            />
            <p className="text-xs text-gray-400 mt-1">Vazio = sem limite. Conta Admin + Operator, não clientes.</p>
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
