'use client'
import { Suspense, useState } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import { authApi, LocateAccountMatch } from '@/lib/api'
import { saveAuth, buildLoginRedirectUrl } from '@/lib/auth'
import toast, { Toaster } from 'react-hot-toast'
import { Mail, KeyRound, Loader2, Gamepad2, ArrowLeft, UserPlus, SearchCheck, Building2 } from 'lucide-react'
import Link from 'next/link'

export default function EntrarPage() {
  return (
    <Suspense fallback={<div className="min-h-screen bg-surface-900" />}>
      <EntrarForm />
    </Suspense>
  )
}

function EntrarForm() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const returnTo = searchParams.get('returnTo') || '/cliente/perfil'
  const [email, setEmail]       = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading]   = useState(false)

  const [loginFailed, setLoginFailed] = useState(false)
  const [locating, setLocating]       = useState(false)
  const [matches, setMatches]         = useState<LocateAccountMatch[] | null>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    setMatches(null)
    try {
      const { data } = await authApi.clientLogin(email, password)
      saveAuth(data)
      toast.success(`Bem-vindo, ${data.userName}!`)
      router.push(returnTo)
    } catch {
      toast.error('E-mail ou senha inválidos.')
      setLoginFailed(true)
    } finally {
      setLoading(false)
    }
  }

  async function handleLocateAccount() {
    setLocating(true)
    try {
      const { data } = await authApi.locateAccount(email, password)
      if (data.length === 1) {
        toast.success(`Encontramos sua conta em ${data[0].label} — entrando...`)
        window.location.href = buildLoginRedirectUrl(data[0])
      } else {
        setMatches(data)
      }
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status
      toast.error(status === 429
        ? 'Muitas tentativas. Aguarde um pouco e tente de novo.'
        : 'Não encontramos essa conta em nenhum lugar.')
    } finally {
      setLocating(false)
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
            Ainda não é cliente?{' '}
            <Link href={`/cadastro?returnTo=${encodeURIComponent(returnTo)}`} className="text-brand-400 hover:text-brand-300 font-medium transition">
              Criar conta
            </Link>
          </div>
          <div className="text-center text-xs text-gray-600">
            Já comprou na loja física?{' '}
            <Link href="/primeiro-acesso" className="text-brand-400 hover:text-brand-300 transition">
              Ative sua conta pelo CPF
            </Link>
          </div>

          {loginFailed && matches === null && (
            <button
              type="button"
              onClick={handleLocateAccount}
              disabled={locating}
              className="w-full flex items-center justify-center gap-2 text-sm text-gray-400 hover:text-white
                         border border-surface-600 rounded-xl py-2.5 transition-colors disabled:opacity-50"
            >
              {locating ? <Loader2 className="w-4 h-4 animate-spin" /> : <SearchCheck className="w-4 h-4" />}
              {locating ? 'Procurando...' : 'Não é essa conta? Procurar em outra loja'}
            </button>
          )}

          {matches !== null && matches.length > 0 && (
            <div className="border border-surface-600 rounded-xl p-3 space-y-2">
              <p className="text-xs text-gray-400">Encontramos essa conta em mais de um lugar:</p>
              {matches.map(m => (
                <a
                  key={m.ticket}
                  href={buildLoginRedirectUrl(m)}
                  className="flex items-center gap-2 p-2.5 rounded-lg bg-surface-800 hover:bg-surface-700
                             text-sm text-white transition-colors"
                >
                  <Building2 className="w-4 h-4 text-brand-400 shrink-0" />
                  {m.label}
                </a>
              ))}
            </div>
          )}
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
