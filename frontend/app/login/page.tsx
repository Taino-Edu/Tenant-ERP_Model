'use client'
import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { authApi, LocateAccountMatch } from '@/lib/api'
import { saveAuth, buildLoginRedirectUrl } from '@/lib/auth'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import toast, { Toaster } from 'react-hot-toast'
import { KeyRound, Mail, Loader2, Eye, EyeOff, SearchCheck, Building2 } from 'lucide-react'
import Link from 'next/link'

export default function LoginPage() {
  const router  = useRouter()
  const { site } = useSiteConfig()
  const [email, setEmail]       = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading]   = useState(false)
  const [showPass, setShowPass] = useState(false)

  // "Não achei minha conta aqui, procurar em outro lugar" — só aparece depois
  // de um login falhar de verdade (senha errada), nunca antes.
  const [loginFailed, setLoginFailed] = useState(false)
  const [locating, setLocating]       = useState(false)
  const [matches, setMatches]         = useState<LocateAccountMatch[] | null>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    setMatches(null)
    try {
      const { data } = await authApi.login(email, password)
      saveAuth(data)
      toast.success(`Bem-vindo, ${data.userName}!`)
      router.push(
        data.role === 'Customer' ? '/cliente'
          : data.role === 'PlatformOwner' ? '/plataforma'
          : data.role === 'Contador' ? '/contador'
          : '/admin/comanda'
      )
    } catch (err: unknown) {
      const response = (err as { response?: { status?: number; data?: { errorCode?: string; message?: string } } })?.response
      if (response?.status === 429) {
        toast.error('Muitas tentativas. Aguarde 1 minuto e tente novamente.')
      } else if (response?.data?.errorCode === 'wrong_domain') {
        // Senha certa, domínio errado (conta de Dono da Plataforma/Contador
        // tentando logar no subdomínio de uma loja) — mensagem específica em
        // vez do genérico "e-mail ou senha inválidos", já que a senha estava
        // certa e a pessoa só precisa ir pro domínio principal.
        toast.error(response.data.message ?? 'Essa conta precisa ser acessada por outro domínio.', { duration: 6000 })
      } else {
        toast.error('E-mail ou senha inválidos.')
        setLoginFailed(true)
      }
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
      <Toaster position="top-right" toastOptions={{ style: { background: '#1e1e28', color: '#fff', border: '1px solid #32323f' }}} />

      {/* Background decorativo */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute -top-40 -right-40 w-96 h-96 bg-brand-600/10 rounded-full blur-3xl" />
        <div className="absolute -bottom-40 -left-40 w-96 h-96 bg-brand-800/10 rounded-full blur-3xl" />
      </div>

      <div className="relative w-full max-w-md">
        {/* Logo */}
        <div className="text-center mb-10">
          <div className="inline-flex items-center justify-center w-20 h-20 bg-brand-600/10 border border-brand-500/20 rounded-full mb-4">
            <img src={site.logoUrl || '/logo-placeholder.svg'} alt={site.siteName} className="w-14 h-14 object-contain" />
          </div>
          <h1 className="text-3xl font-bold text-white">{site.siteName}</h1>
          <p className="text-gray-400 mt-1 text-sm">Painel de Gestão</p>
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
                placeholder="seu@email.com.br"
              />
            </div>
          </div>
          <div>
            <label className="label">Senha</label>
            <div className="relative">
              <KeyRound className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
              <input
                type={showPass ? 'text' : 'password'} required value={password}
                onChange={e => setPassword(e.target.value)}
                className="input pl-9 pr-10"
                placeholder="••••••••"
              />
              <button
                type="button"
                onClick={() => setShowPass(v => !v)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-300 transition-colors"
                tabIndex={-1}
                aria-label={showPass ? 'Ocultar senha' : 'Mostrar senha'}
              >
                {showPass ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
          </div>
          <button type="submit" disabled={loading} className="btn-primary w-full justify-center py-2.5 text-base">
            {loading ? <Loader2 className="w-5 h-5 animate-spin" /> : <KeyRound className="w-5 h-5" />}
            {loading ? 'Entrando...' : 'Entrar no Painel'}
          </button>
          <div className="text-center">
            <Link href="/reset-password?from=admin" className="text-xs text-gray-500 hover:text-gray-300 transition-colors">
              Esqueci minha senha
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
              {locating ? 'Procurando...' : 'Não é essa conta? Procurar em outro lugar'}
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

        <div className="text-center mt-6 space-y-1">
          <p className="text-xs text-gray-400">
            Clientes acessam via QR Code nas mesas
          </p>
          <p className="text-xs text-gray-500">
            É cliente?{' '}
            <Link href="/entrar" className="text-brand-400 hover:text-brand-300 transition-colors">
              Acesse a área do cliente
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}
