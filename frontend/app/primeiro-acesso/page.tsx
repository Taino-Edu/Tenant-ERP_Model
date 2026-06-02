'use client'
import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { authApi, CpfLookupResponse } from '@/lib/api'
import { saveAuth } from '@/lib/auth'
import toast, { Toaster } from 'react-hot-toast'
import { Loader2, Gamepad2, ArrowLeft, CreditCard, Mail, KeyRound, CheckCircle } from 'lucide-react'
import Link from 'next/link'

type Step = 'cpf' | 'criar' | 'login'

function formatCpf(value: string) {
  return value.replace(/\D/g, '').slice(0, 11)
}

export default function PrimeiroAcessoPage() {
  const router = useRouter()
  const [step, setStep]           = useState<Step>('cpf')
  const [cpf, setCpf]             = useState('')
  const [lookup, setLookup]       = useState<CpfLookupResponse | null>(null)
  const [email, setEmail]         = useState('')
  const [password, setPassword]   = useState('')
  const [confirm, setConfirm]     = useState('')
  const [loading, setLoading]     = useState(false)

  async function handleCpfSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    try {
      const { data } = await authApi.cpfLookup(cpf)
      setLookup(data)
      setStep(data.hasPassword ? 'login' : 'criar')
    } catch {
      toast.error('CPF não encontrado. Visite a loja e escaneie o QR Code para criar sua conta.')
    } finally {
      setLoading(false)
    }
  }

  async function handleSetup(e: React.FormEvent) {
    e.preventDefault()
    if (password !== confirm) { toast.error('As senhas não coincidem.'); return }
    setLoading(true)
    try {
      const { data } = await authApi.setupAccount(cpf, email, password)
      saveAuth(data)
      toast.success('Conta criada! Bem-vindo, ' + data.userName)
      router.push('/cliente/perfil')
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Erro ao criar conta.')
    } finally {
      setLoading(false)
    }
  }

  async function handleLogin(e: React.FormEvent) {
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
        <Link href={step === 'cpf' ? '/' : '#'} onClick={step !== 'cpf' ? () => setStep('cpf') : undefined}
          className="flex items-center gap-2 text-gray-500 hover:text-white transition mb-8 w-fit">
          <ArrowLeft className="w-4 h-4" />
          {step === 'cpf' ? 'Voltar para a loja' : 'Voltar'}
        </Link>

        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 bg-brand-600/20 border border-brand-500/30 rounded-2xl mb-4">
            <Gamepad2 className="w-8 h-8 text-brand-400" />
          </div>
          <h1 className="text-2xl font-bold text-white">
            {step === 'cpf'   && 'Primeiro Acesso'}
            {step === 'criar' && `Olá, ${lookup?.name}!`}
            {step === 'login' && `Bem-vindo, ${lookup?.name}!`}
          </h1>
          <p className="text-gray-400 mt-1 text-sm">
            {step === 'cpf'   && 'Digite seu CPF para identificar sua conta'}
            {step === 'criar' && 'Crie uma senha para acessar o site'}
            {step === 'login' && 'Você já tem uma conta. Faça login.'}
          </p>
        </div>

        {/* Step 1 — CPF */}
        {step === 'cpf' && (
          <form onSubmit={handleCpfSubmit} className="card space-y-5">
            <div>
              <label className="label">CPF</label>
              <div className="relative">
                <CreditCard className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                <input
                  className="input pl-9 font-mono tracking-wider"
                  placeholder="Somente números"
                  value={cpf}
                  onChange={e => setCpf(formatCpf(e.target.value))}
                  maxLength={11}
                  required
                />
              </div>
            </div>
            <button type="submit" disabled={loading || cpf.length !== 11} className="btn-primary w-full justify-center py-2.5">
              {loading ? <Loader2 className="w-5 h-5 animate-spin" /> : <CheckCircle className="w-5 h-5" />}
              {loading ? 'Buscando...' : 'Continuar'}
            </button>
            <p className="text-center text-sm text-gray-500">
              Já tem conta?{' '}
              <Link href="/entrar" className="text-brand-400 hover:text-brand-300 font-medium">Entrar</Link>
            </p>
          </form>
        )}

        {/* Step 2 — Criar conta */}
        {step === 'criar' && (
          <form onSubmit={handleSetup} className="card space-y-5">
            <div>
              <label className="label">E-mail</label>
              <div className="relative">
                <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                <input type="email" required className="input pl-9" placeholder="seu@email.com"
                  value={email} onChange={e => setEmail(e.target.value)} />
              </div>
            </div>
            <div>
              <label className="label">Senha</label>
              <div className="relative">
                <KeyRound className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                <input type="password" required minLength={8} className="input pl-9" placeholder="Mínimo 8 caracteres"
                  value={password} onChange={e => setPassword(e.target.value)} />
              </div>
            </div>
            <div>
              <label className="label">Confirmar senha</label>
              <div className="relative">
                <KeyRound className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                <input type="password" required className="input pl-9" placeholder="Repita a senha"
                  value={confirm} onChange={e => setConfirm(e.target.value)} />
              </div>
            </div>
            <button type="submit" disabled={loading} className="btn-primary w-full justify-center py-2.5">
              {loading ? <Loader2 className="w-5 h-5 animate-spin" /> : <CheckCircle className="w-5 h-5" />}
              {loading ? 'Criando conta...' : 'Criar minha conta'}
            </button>
          </form>
        )}

        {/* Step 3 — Já tem senha, fazer login */}
        {step === 'login' && (
          <form onSubmit={handleLogin} className="card space-y-5">
            <div className="bg-brand-600/10 border border-brand-500/20 rounded-xl p-3 text-sm text-brand-300">
              Você já ativou sua conta. Entre com seu e-mail e senha.
            </div>
            <div>
              <label className="label">E-mail</label>
              <div className="relative">
                <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                <input type="email" required className="input pl-9" placeholder="seu@email.com"
                  value={email} onChange={e => setEmail(e.target.value)} />
              </div>
            </div>
            <div>
              <label className="label">Senha</label>
              <div className="relative">
                <KeyRound className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                <input type="password" required className="input pl-9" placeholder="••••••••"
                  value={password} onChange={e => setPassword(e.target.value)} />
              </div>
            </div>
            <button type="submit" disabled={loading} className="btn-primary w-full justify-center py-2.5">
              {loading ? <Loader2 className="w-5 h-5 animate-spin" /> : <KeyRound className="w-5 h-5" />}
              {loading ? 'Entrando...' : 'Entrar'}
            </button>
            <Link href="/reset-password" className="block text-center text-xs text-gray-400 hover:text-gray-400 transition">
              Esqueci minha senha
            </Link>
          </form>
        )}
      </div>
    </div>
  )
}
