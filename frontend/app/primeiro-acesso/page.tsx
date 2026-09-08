'use client'
// =============================================================================
// /primeiro-acesso — cliente da loja define e-mail e senha na conta que já
// existe (criada pelo quick-login do QR Code da mesa).
//
// Esta tela mudou em 2026-09-08 junto com a correção de segurança do
// quick-login. Antes ela começava pedindo o CPF e o servidor respondia com o
// NOME do titular — dado pessoal devolvido a quem não estava autenticado, e um
// jeito de descobrir se um CPF é cliente daquela loja. O endpoint que fazia
// isso (POST /api/auth/cpf-lookup) foi removido.
//
// A ativação agora exige sessão do próprio cliente: identificador cadastral
// não é credencial, então nem o CPF nem o WhatsApp abrem conta de outra
// pessoa. Isso divide a tela em dois casos, e o segundo é honesto sobre o que
// não dá para fazer daqui:
//
//   - COM sessão de cliente (entrou pelo QR Code na mesa) → o formulário
//     aparece e funciona; o CPF é pedido só como confirmação e é conferido
//     contra a conta da sessão no servidor.
//   - SEM sessão → não há formulário. Oferecer um seria repetir o defeito
//     anterior de outra forma: o envio falharia sempre, porque o servidor não
//     tem como saber que a conta é sua.
// =============================================================================
import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { authApi } from '@/lib/api'
import { saveAuth, getRole, getUserName } from '@/lib/auth'
import toast, { Toaster } from 'react-hot-toast'
import { Loader2, Gamepad2, ArrowLeft, CreditCard, Mail, KeyRound, CheckCircle, QrCode } from 'lucide-react'
import Link from 'next/link'

function formatCpf(value: string) {
  return value.replace(/\D/g, '').slice(0, 11)
}

export default function PrimeiroAcessoPage() {
  const router = useRouter()
  // null enquanto não sabemos: os cookies de sessão só existem no browser, e
  // renderizar "você não está logado" antes de checar faria a tela piscar a
  // mensagem errada pra quem está logado.
  const [isCustomer, setIsCustomer] = useState<boolean | null>(null)
  const [userName, setUserName]     = useState('')
  const [cpf, setCpf]               = useState('')
  const [email, setEmail]           = useState('')
  const [password, setPassword]     = useState('')
  const [confirm, setConfirm]       = useState('')
  const [loading, setLoading]       = useState(false)

  useEffect(() => {
    setIsCustomer(getRole() === 'Customer')
    setUserName(getUserName())
  }, [])

  async function handleSetup(e: React.FormEvent) {
    e.preventDefault()
    if (password !== confirm) { toast.error('As senhas não coincidem.'); return }
    setLoading(true)
    try {
      const { data } = await authApi.setupAccount(cpf, email, password)
      saveAuth(data)
      toast.success('Senha criada! Bem-vindo, ' + data.userName)
      router.push('/cliente/perfil')
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      toast.error(msg || 'Não foi possível criar a senha.')
    } finally {
      setLoading(false)
    }
  }

  return (
    // Ver comentário em app/login/page.tsx: "admin-shell" é o escopo do tema claro.
    <div className="admin-shell min-h-screen bg-surface-900 flex items-center justify-center p-4">
      <Toaster position="top-center" toastOptions={{ style: { background: '#1e1e28', color: '#fff', border: '1px solid #32323f' }}} />

      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute -top-40 -right-40 w-96 h-96 bg-brand-600/10 rounded-full blur-3xl" />
        <div className="absolute -bottom-40 -left-40 w-96 h-96 bg-brand-800/10 rounded-full blur-3xl" />
      </div>

      <div className="relative w-full max-w-md">
        <Link href="/" className="flex items-center gap-2 text-gray-500 hover:text-white transition mb-8 w-fit">
          <ArrowLeft className="w-4 h-4" />
          Voltar para a loja
        </Link>

        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 bg-brand-600/20 border border-brand-500/30 rounded-2xl mb-4">
            <Gamepad2 className="w-8 h-8 text-brand-400" />
          </div>
          <h1 className="text-2xl font-bold text-white">
            {isCustomer && userName ? `Olá, ${userName}!` : 'Primeiro Acesso'}
          </h1>
          <p className="text-gray-400 mt-1 text-sm">
            {isCustomer
              ? 'Crie uma senha para entrar pelo site quando quiser'
              : 'Como criar sua senha de acesso ao site'}
          </p>
        </div>

        {isCustomer === null && (
          <div className="card flex items-center justify-center py-10">
            <Loader2 className="w-6 h-6 animate-spin text-brand-400" />
          </div>
        )}

        {/* Com sessão de cliente: o formulário funciona. */}
        {isCustomer === true && (
          <form onSubmit={handleSetup} className="card space-y-5">
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
              <p className="mt-1.5 text-xs text-gray-500">Confirmação — precisa ser o CPF desta conta.</p>
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
            <button type="submit" disabled={loading || cpf.length !== 11} className="btn-primary w-full justify-center py-2.5">
              {loading ? <Loader2 className="w-5 h-5 animate-spin" /> : <CheckCircle className="w-5 h-5" />}
              {loading ? 'Criando senha...' : 'Criar minha senha'}
            </button>
          </form>
        )}

        {/* Sem sessão: nenhum formulário — ele não teria como funcionar. */}
        {isCustomer === false && (
          <div className="card space-y-5">
            <div className="flex gap-3 rounded-xl border border-brand-500/20 bg-brand-600/10 p-3">
              <QrCode className="mt-0.5 h-5 w-5 shrink-0 text-brand-300" />
              <p className="text-sm text-brand-200">
                A senha é criada de dentro da sua conta. Escaneie o QR Code da mesa
                na loja para entrar, e a opção de criar senha aparece aqui.
              </p>
            </div>

            <p className="text-sm text-gray-400">
              Fazemos assim porque CPF e telefone não são senha: se bastasse
              digitá-los, qualquer pessoa que soubesse os seus dados entraria na
              sua conta.
            </p>

            <Link href="/entrar" className="btn-primary w-full justify-center py-2.5">
              <KeyRound className="w-5 h-5" />
              Já tenho e-mail e senha
            </Link>

            <p className="text-center text-sm text-gray-500">
              Não consegue escanear o QR Code? Fale com a equipe da loja.
            </p>
          </div>
        )}
      </div>
    </div>
  )
}
