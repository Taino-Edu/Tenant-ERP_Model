'use client'
import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { authApi } from '@/lib/api'
import { saveAuth } from '@/lib/auth'
import toast, { Toaster } from 'react-hot-toast'
import { Mail, KeyRound, Loader2, Gamepad2, ArrowLeft, UserPlus } from 'lucide-react'
import Link from 'next/link'

export default function EntrarPage() {
  const router = useRouter()
  const [email, setEmail]       = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading]   = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    try {
      const { data } = await authApi.clientLogin(email, password)
      saveAuth(data)
      toast.success(`Bem-vindo, ${data.userName}!`)
      router.push('/cliente/perfil')
    } catch {
      toast.error('E-mail ou senha inválidos.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-surface-900 flex items-center justify-center p-4">
      <Toaster position="top-center" toastOptions={{ style: { background: '#1e1e28', color: '#fff', border: '1px solid #32323f' }}} />

      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute -top-40 -right-40 w-96 h-96 bg-brand-600/10 rounded-full blur-3xl" />
        <div className="absolute -bottom-40 -left-40 w-96 h-96 bg-brand-800/10 rounded-full blur-3xl" />
      </div>

      <div className="relative w-full max-w-md">
        <Link href="/" className="flex items-center gap-2 text-gray-500 hover:text-white transition mb-8 w-fit">
          <ArrowLeft className="w-4 h-4" /> Voltar para a loja
        </Link>

        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 bg-brand-600/20 border border-brand-500/30 rounded-2xl mb-4">
            <Gamepad2 className="w-8 h-8 text-brand-400" />
          </div>
          <h1 className="text-2xl font-bold text-white">Minha Conta</h1>
          <p className="text-gray-400 mt-1 text-sm">Entre para ver seus pontos e histórico</p>
        </div>

        <form onSubmit={handleSubmit} className="card space-y-5">
          <div>
            <label className="label">E-mail</label>
            <div className="relative">
              <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
              <input
                type="email" required value={email}
                onChange={e => setEmail(e.target.value)}
                className="input pl-9"
                placeholder="seu@email.com"
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

          <button type="submit" disabled={loading} className="btn-primary w-full justify-center py-2.5">
            {loading ? <Loader2 className="w-5 h-5 animate-spin" /> : <KeyRound className="w-5 h-5" />}
            {loading ? 'Entrando...' : 'Entrar'}
          </button>

          <div className="text-center text-sm text-gray-500">
            Ainda não tem senha?{' '}
            <Link href="/primeiro-acesso" className="text-brand-400 hover:text-brand-300 font-medium transition">
              Criar minha conta
            </Link>
          </div>
        </form>

        <div className="mt-4 text-center">
          <Link href="/reset-password" className="text-xs text-gray-400 hover:text-gray-400 transition">
            Esqueci minha senha
          </Link>
        </div>
      </div>
    </div>
  )
}
