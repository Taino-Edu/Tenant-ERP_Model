'use client'
import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { authApi } from '@/lib/api'
import { saveAuth } from '@/lib/auth'
import toast, { Toaster } from 'react-hot-toast'
import { KeyRound, Mail, Loader2, Sword } from 'lucide-react'

export default function LoginPage() {
  const router  = useRouter()
  const [email, setEmail]     = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    try {
      const { data } = await authApi.login(email, password)
      saveAuth(data)
      toast.success(`Bem-vindo, ${data.userName}!`)
      router.push(data.role === 'Admin' ? '/admin/dashboard' : '/cliente')
    } catch {
      toast.error('E-mail ou senha inválidos.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-surface-900 flex items-center justify-center p-4">
      <Toaster position="top-right" toastOptions={{ style: { background: '#1e1e28', color: '#fff', border: '1px solid #32323f' }}} />

      {/* Background decorativo */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute -top-40 -right-40 w-96 h-96 bg-brand-600/10 rounded-full blur-3xl" />
        <div className="absolute -bottom-40 -left-40 w-96 h-96 bg-brand-800/10 rounded-full blur-3xl" />
      </div>

      <div className="relative w-full max-w-md">
        {/* Logo */}
        <div className="text-center mb-10">
          <div className="inline-flex items-center justify-center w-16 h-16 bg-brand-600/20 border border-brand-500/30 rounded-2xl mb-4">
            <Sword className="w-8 h-8 text-brand-400" />
          </div>
          <h1 className="text-3xl font-bold text-white">CardGameStore</h1>
          <p className="text-gray-400 mt-1 text-sm">Painel de Gestão — Maikon</p>
        </div>

        {/* Formulário */}
        <form onSubmit={handleSubmit} className="card space-y-5">
          <div>
            <label className="label">E-mail</label>
            <div className="relative">
              <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
              <input
                type="email" required value={email}
                onChange={e => setEmail(e.target.value)}
                className="input pl-9"
                placeholder="admin@cardgamestore.com.br"
              />
            </div>
          </div>
          <div>
            <label className="label">Senha</label>
            <div className="relative">
              <KeyRound className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
              <input
                type="password" required value={password}
                onChange={e => setPassword(e.target.value)}
                className="input pl-9"
                placeholder="••••••••"
              />
            </div>
          </div>
          <button type="submit" disabled={loading} className="btn-primary w-full justify-center py-2.5 text-base">
            {loading ? <Loader2 className="w-5 h-5 animate-spin" /> : <KeyRound className="w-5 h-5" />}
            {loading ? 'Entrando...' : 'Entrar no Painel'}
          </button>
        </form>

        <p className="text-center text-xs text-gray-600 mt-6">
          Clientes acessam via QR Code nas mesas
        </p>
      </div>
    </div>
  )
}
