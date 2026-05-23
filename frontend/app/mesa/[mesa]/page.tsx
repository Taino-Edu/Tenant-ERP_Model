'use client'
import { useState, useEffect } from 'react'
import { useParams, useRouter } from 'next/navigation'
import { authApi } from '@/lib/api'
import { saveAuth } from '@/lib/auth'
import toast, { Toaster } from 'react-hot-toast'
import { Smartphone, User, Hash, MessageCircle, Loader2, Sword, TableProperties, X, Shield, Zap, ChevronRight } from 'lucide-react'

// =============================================================================
// Chave usada para persistir dados do usuário no localStorage
// =============================================================================
const STORAGE_KEY = 'mesa-last-user'

interface SavedUser {
  name: string
  cpf: string       // raw 11 digits
  whatsApp: string
  displayCpf: string // formatted for display
}

// =============================================================================
// Modal de Política de Privacidade (LGPD — Art. 9 — direito à informação)
// =============================================================================
function PrivacyModal({ onClose }: { onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70">
      <div className="relative w-full max-w-md bg-surface-800 border border-surface-700 rounded-2xl shadow-xl max-h-[80vh] flex flex-col">
        <div className="flex items-center justify-between p-4 border-b border-surface-700">
          <div className="flex items-center gap-2">
            <Shield className="w-5 h-5 text-brand-400" />
            <h2 className="text-white font-semibold">Política de Privacidade</h2>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-white transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-4 overflow-y-auto text-sm text-gray-300 space-y-3">
          <p><strong className="text-white">softNerd — Política de Privacidade</strong></p>
          <p>
            Em conformidade com a Lei Geral de Proteção de Dados Pessoais (LGPD — Lei nº 13.709/2018),
            informamos como tratamos seus dados pessoais.
          </p>
          <p><strong className="text-white">Dados coletados:</strong> Nome completo, CPF e número de WhatsApp.</p>
          <p>
            <strong className="text-white">Finalidade:</strong> Identificação do cliente para abertura de comanda
            na loja softNerd e registro de pontos de fidelidade.
          </p>
          <p>
            <strong className="text-white">Base legal:</strong> Consentimento do titular (Art. 7º, I da LGPD).
          </p>
          <p>
            <strong className="text-white">Seus direitos:</strong> Você pode solicitar acesso, correção ou
            exclusão dos seus dados a qualquer momento pelo painel do cliente ou pelo WhatsApp da loja.
          </p>
          <p>
            <strong className="text-white">Compartilhamento:</strong> Seus dados não são vendidos nem
            compartilhados com terceiros. São usados exclusivamente para operação interna da loja.
          </p>
          <p>
            <strong className="text-white">Retenção:</strong> Os dados são mantidos pelo período necessário
            à prestação dos serviços e obrigações legais. Você pode solicitar a exclusão a qualquer momento.
          </p>
          <p className="text-gray-500 text-xs">
            Responsável: softNerd — Loja de Card Games. Dúvidas? Fale conosco pelo WhatsApp da loja.
          </p>
        </div>
        <div className="p-4 border-t border-surface-700">
          <button onClick={onClose} className="btn-primary w-full justify-center">
            Entendi
          </button>
        </div>
      </div>
    </div>
  )
}

// =============================================================================
// Página de Quick-Login por QR Code
// =============================================================================
export default function MesaPage() {
  const params  = useParams()
  const router  = useRouter()
  const mesa    = decodeURIComponent(params.mesa as string)

  // 'quick' = mostra login rápido, 'form' = formulário completo, 'loading' = aguardando
  const [step, setStep]             = useState<'quick' | 'form' | 'loading'>('form')
  const [savedUser, setSavedUser]   = useState<SavedUser | null>(null)

  // Campos do formulário completo
  const [name, setName]             = useState('')
  const [cpf, setCpf]               = useState('')
  const [whatsApp, setWhatsApp]     = useState('')
  const [consent, setConsent]       = useState(false)
  const [showPrivacy, setShowPrivacy] = useState(false)

  // ── Detecta usuário salvo no localStorage ──────────────────────────────────
  useEffect(() => {
    try {
      const raw = localStorage.getItem(STORAGE_KEY)
      if (raw) {
        const user: SavedUser = JSON.parse(raw)
        if (user.name && user.cpf && user.whatsApp) {
          setSavedUser(user)
          setStep('quick')
        }
      }
    } catch { /* ignora erros de localStorage */ }
  }, [])

  // ── Formatadores ──────────────────────────────────────────────────────────
  function formatCpf(v: string) {
    return v.replace(/\D/g, '').slice(0, 11)
      .replace(/(\d{3})(\d)/, '$1.$2')
      .replace(/(\d{3})(\d)/, '$1.$2')
      .replace(/(\d{3})(\d{1,2})$/, '$1-$2')
  }
  function formatPhone(v: string) {
    return v.replace(/\D/g, '').slice(0, 13)
  }
  function maskCpf(cpf: string) {
    // "***.***.123-45"
    const d = cpf.replace(/\D/g, '')
    if (d.length !== 11) return cpf
    return `***.***.${d.slice(6, 9)}-${d.slice(9)}`
  }

  // ── Função central de login ───────────────────────────────────────────────
  async function doLogin(loginName: string, rawCpf: string, loginWhatsApp: string) {
    setStep('loading')
    try {
      const { data } = await authApi.quickLogin(loginName, rawCpf, loginWhatsApp, mesa)
      saveAuth(data)
      if (data.comandaId) {
        document.cookie = `activeComandaId=${data.comandaId}; path=/; max-age=86400`
      }
      // Salva para o próximo acesso
      const toSave: SavedUser = {
        name: loginName,
        cpf: rawCpf,
        whatsApp: loginWhatsApp,
        displayCpf: maskCpf(rawCpf),
      }
      localStorage.setItem(STORAGE_KEY, JSON.stringify(toSave))
      router.push('/cliente')
    } catch {
      setStep(savedUser ? 'quick' : 'form')
      toast.error('Erro ao entrar. Tente novamente.')
    }
  }

  // ── Quick-login (usuário retornante) ─────────────────────────────────────
  async function handleQuickLogin() {
    if (!savedUser) return
    await doLogin(savedUser.name, savedUser.cpf, savedUser.whatsApp)
  }

  // ── Formulário completo ───────────────────────────────────────────────────
  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!consent) {
      toast.error('Você precisa aceitar a Política de Privacidade para continuar.')
      return
    }
    const rawCpf = cpf.replace(/\D/g, '')
    if (rawCpf.length !== 11) { toast.error('CPF inválido'); return }
    await doLogin(name, rawCpf, whatsApp)
  }

  // ── Troca para outro usuário ──────────────────────────────────────────────
  function switchUser() {
    setStep('form')
    setName(''); setCpf(''); setWhatsApp(''); setConsent(false)
  }

  // ── Header compartilhado ──────────────────────────────────────────────────
  const Header = (
    <div className="text-center mb-8">
      <div className="inline-flex items-center justify-center w-14 h-14 bg-brand-600/20 border border-brand-500/30 rounded-2xl mb-4">
        <Sword className="w-7 h-7 text-brand-400" />
      </div>
      <h1 className="text-2xl font-bold text-white">softNerd</h1>
      <div className="flex items-center justify-center gap-2 mt-2 text-sm text-brand-300">
        <TableProperties className="w-4 h-4" />
        <span className="font-medium">{mesa}</span>
      </div>
    </div>
  )

  return (
    <div className="min-h-screen bg-surface-900 flex items-center justify-center p-4">
      <Toaster position="top-right" toastOptions={{ style: { background: '#1e1e28', color: '#fff', border: '1px solid #32323f' }}} />

      {showPrivacy && <PrivacyModal onClose={() => setShowPrivacy(false)} />}

      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute -top-32 -right-32 w-64 h-64 bg-brand-600/10 rounded-full blur-3xl" />
        <div className="absolute -bottom-32 -left-32 w-64 h-64 bg-brand-800/10 rounded-full blur-3xl" />
      </div>

      <div className="relative w-full max-w-sm">
        {Header}

        {/* ── LOADING ── */}
        {step === 'loading' && (
          <div className="card text-center py-12 space-y-4">
            <div className="w-12 h-12 border-2 border-brand-500 border-t-transparent rounded-full animate-spin mx-auto" />
            <p className="text-gray-300">Abrindo sua comanda...</p>
          </div>
        )}

        {/* ── QUICK LOGIN ── */}
        {step === 'quick' && savedUser && (
          <div className="card space-y-4">
            <div className="text-center space-y-1">
              <div className="inline-flex items-center justify-center w-10 h-10 bg-emerald-500/10 border border-emerald-500/30 rounded-xl mb-2">
                <Zap className="w-5 h-5 text-emerald-400" />
              </div>
              <h2 className="text-white font-semibold text-lg">Olá, {savedUser.name.split(' ')[0]}!</h2>
              <p className="text-gray-400 text-sm">CPF: <span className="font-mono text-gray-300">{savedUser.displayCpf}</span></p>
            </div>

            <button
              onClick={handleQuickLogin}
              className="btn-primary w-full justify-between py-3 text-base group"
            >
              <span className="flex items-center gap-2">
                <Smartphone className="w-5 h-5" />
                Abrir Minha Comanda
              </span>
              <ChevronRight className="w-5 h-5 group-hover:translate-x-0.5 transition-transform" />
            </button>

            <div className="relative">
              <div className="absolute inset-0 flex items-center">
                <div className="w-full border-t border-surface-600" />
              </div>
              <div className="relative flex justify-center text-xs">
                <span className="bg-surface-800 px-3 text-gray-500">ou</span>
              </div>
            </div>

            <button
              onClick={switchUser}
              className="btn-secondary w-full justify-center text-sm"
            >
              <User className="w-4 h-4" />
              Usar outro CPF
            </button>
          </div>
        )}

        {/* ── FORMULÁRIO COMPLETO ── */}
        {step === 'form' && (
          <form onSubmit={handleSubmit} className="card space-y-4">
            {savedUser && (
              <button
                type="button"
                onClick={() => setStep('quick')}
                className="flex items-center gap-2 text-xs text-brand-400 hover:text-brand-300 transition-colors"
              >
                <ChevronRight className="w-3.5 h-3.5 rotate-180" />
                Voltar para {savedUser.name.split(' ')[0]}
              </button>
            )}

            <div>
              <label className="label">Seu nome</label>
              <div className="relative">
                <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                <input
                  className="input pl-9" required placeholder="João Silva"
                  value={name} onChange={e => setName(e.target.value)}
                />
              </div>
            </div>

            <div>
              <label className="label">CPF</label>
              <div className="relative">
                <Hash className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                <input
                  className="input pl-9 font-mono" required placeholder="000.000.000-00"
                  value={cpf} onChange={e => setCpf(formatCpf(e.target.value))}
                  inputMode="numeric"
                />
              </div>
            </div>

            <div>
              <label className="label">WhatsApp</label>
              <div className="relative">
                <MessageCircle className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                <input
                  className="input pl-9 font-mono" required placeholder="55119XXXXXXXX"
                  value={whatsApp} onChange={e => setWhatsApp(formatPhone(e.target.value))}
                  inputMode="tel"
                />
              </div>
              <p className="text-xs text-gray-600 mt-1">DDD + número, sem espaços</p>
            </div>

            {/* Consentimento LGPD */}
            <div className="flex items-start gap-3 p-3 bg-surface-800/50 border border-surface-700 rounded-lg">
              <input
                id="consent"
                type="checkbox"
                checked={consent}
                onChange={e => setConsent(e.target.checked)}
                className="mt-0.5 w-4 h-4 rounded border-surface-600 text-brand-500 cursor-pointer flex-shrink-0"
              />
              <label htmlFor="consent" className="text-xs text-gray-400 leading-relaxed cursor-pointer">
                Li e aceito a{' '}
                <button
                  type="button"
                  onClick={() => setShowPrivacy(true)}
                  className="text-brand-400 underline hover:text-brand-300 transition-colors"
                >
                  Política de Privacidade
                </button>
                {' '}e autorizo o uso dos meus dados (nome, CPF e WhatsApp) para identificação na loja softNerd.{' '}
                <button
                  type="button"
                  onClick={() => setShowPrivacy(true)}
                  className="text-gray-500 hover:text-gray-400 transition-colors text-xs"
                >
                  (ver política)
                </button>
              </label>
            </div>

            <button
              type="submit"
              disabled={!consent}
              className="btn-primary w-full justify-center py-3 text-base mt-2 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <Smartphone className="w-5 h-5" />
              Abrir Minha Comanda
            </button>
          </form>
        )}
      </div>
    </div>
  )
}
