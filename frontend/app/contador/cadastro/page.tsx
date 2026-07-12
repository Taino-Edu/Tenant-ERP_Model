'use client'
import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { authApi } from '@/lib/api'
import { saveAuth } from '@/lib/auth'
import toast from 'react-hot-toast'
import { User, Mail, KeyRound, Building2, Loader2 } from 'lucide-react'
import Link from 'next/link'

export default function ContadorCadastroPage() {
  const router = useRouter()

  const [name, setName]         = useState('')
  const [email, setEmail]       = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm]   = useState('')
  const [tenantSlug, setTenantSlug] = useState('')
  const [loading, setLoading]   = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (password !== confirm) { toast.error('As senhas não coincidem.'); return }
    if (password.length < 8) { toast.error('A senha precisa ter pelo menos 8 caracteres.'); return }
    if (!tenantSlug.trim()) { toast.error('Informe o slug da loja que você atende.'); return }

    setLoading(true)
    try {
      const { data } = await authApi.registerContador(name.trim(), email.trim(), password, tenantSlug.trim().toLowerCase())
      saveAuth(data)
      toast.success('Conta criada! Sua solicitação de acesso foi enviada ao lojista.')
      router.push('/contador')
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao criar conta.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="max-w-md mx-auto">
      <div className="text-center mb-6">
        <h1 className="text-xl font-bold text-white">Cadastro de Contador</h1>
        <p className="text-gray-400 mt-1 text-sm">
          Crie sua conta e solicite acesso à loja que você atende — o lojista precisa aprovar antes que você veja os dados fiscais.
        </p>
      </div>

      <form onSubmit={handleSubmit} className="card space-y-4">
        <div>
          <label className="label">Nome completo</label>
          <div className="relative">
            <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input type="text" required value={name} onChange={e => setName(e.target.value)}
                   className="input pl-9" placeholder="José Contabilidade" />
          </div>
        </div>
        <div>
          <label className="label">E-mail</label>
          <div className="relative">
            <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input type="email" required value={email} onChange={e => setEmail(e.target.value)}
                   className="input pl-9" placeholder="contador@escritorio.com" />
          </div>
        </div>
        <div>
          <label className="label">Slug da loja</label>
          <div className="relative">
            <Building2 className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input type="text" required value={tenantSlug} onChange={e => setTenantSlug(e.target.value)}
                   className="input pl-9" placeholder="slug-da-loja" />
          </div>
          <p className="text-xs text-gray-500 mt-1">Peça este código ao lojista.</p>
        </div>
        <div>
          <label className="label">Senha</label>
          <div className="relative">
            <KeyRound className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input type="password" required value={password} onChange={e => setPassword(e.target.value)}
                   className="input pl-9" placeholder="Mínimo 8 caracteres" />
          </div>
        </div>
        <div>
          <label className="label">Confirmar senha</label>
          <div className="relative">
            <KeyRound className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
            <input type="password" required value={confirm} onChange={e => setConfirm(e.target.value)}
                   className="input pl-9" placeholder="••••••••" />
          </div>
        </div>

        <button type="submit" disabled={loading} className="btn-primary w-full justify-center py-2.5">
          {loading ? <Loader2 className="w-5 h-5 animate-spin" /> : <User className="w-5 h-5" />}
          {loading ? 'Criando...' : 'Criar conta e solicitar acesso'}
        </button>

        <div className="text-center text-sm text-gray-500">
          Já tem conta?{' '}
          <Link href="/login" className="text-brand-400 hover:text-brand-300 font-medium transition">
            Entrar
          </Link>
        </div>
      </form>
    </div>
  )
}
