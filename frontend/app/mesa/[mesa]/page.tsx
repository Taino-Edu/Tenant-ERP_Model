'use client'
import { useState, useEffect } from 'react'
import { useParams, useRouter } from 'next/navigation'
import { authApi } from '@/lib/api'
import { saveAuth } from '@/lib/auth'
import toast, { Toaster } from 'react-hot-toast'
import { 
  Smartphone, User, Hash, MessageCircle, Loader2, 
  Sword, TableProperties, X, Shield, Zap, 
  ChevronRight, ArrowLeft, BookOpen, Award
} from 'lucide-react'
import clsx from 'clsx'

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
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-md">
      <div className="relative w-full max-w-md bg-surface-800 border border-surface-600 rounded-3xl shadow-2xl max-h-[85vh] flex flex-col overflow-hidden">
        <div className="flex items-center justify-between p-6 border-b border-surface-700">
          <div className="flex items-center gap-3">
            <Shield className="w-6 h-6 text-brand-500" />
            <h2 className="text-white font-bold tracking-tight">Privacidade & Dados</h2>
          </div>
          <button onClick={onClose} className="p-2 text-gray-400 hover:text-white transition-colors">
            <X className="w-6 h-6" />
          </button>
        </div>
        <div className="p-6 overflow-y-auto text-sm text-gray-400 space-y-4 leading-relaxed">
          <p><strong className="text-white">Santuário Nerd — Política de Privacidade</strong></p>
          <p>
            Em conformidade com a Lei Geral de Proteção de Dados Pessoais (LGPD — Lei nº 13.709/2018),
            informamos como tratamos seus dados pessoais.
          </p>
          <div className="space-y-3">
            <div className="bg-surface-700 p-4 rounded-2xl border border-surface-600">
              <p><strong className="text-white text-xs uppercase">Dados coletados:</strong></p>
              <p>Nome completo, CPF e número de WhatsApp.</p>
            </div>
            <div className="bg-surface-700 p-4 rounded-2xl border border-surface-600">
              <p><strong className="text-white text-xs uppercase">Finalidade:</strong></p>
              <p>Identificação do cliente para abertura de comanda e registro de pontos de fidelidade.</p>
            </div>
          </div>
          <p>
            <strong className="text-white">Seus direitos:</strong> Você pode solicitar acesso, correção ou
            exclusão dos seus dados a qualquer momento pelo painel do cliente.
          </p>
          <p className="text-[10px] text-gray-400 italic">
            Responsável: Santuário Nerd — São José do Rio Preto, SP.
          </p>
        </div>
        <div className="p-6 border-t border-surface-700">
          <button onClick={onClose} className="w-full py-4 bg-brand-500 text-white font-bold rounded-2xl shadow-lg shadow-brand-500/20 hover:bg-brand-600 transition-all">
            Entendido
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

  const [step, setStep]             = useState<'quick' | 'form' | 'loading'>('form')
  const [savedUser, setSavedUser]   = useState<SavedUser | null>(null)

  const [name, setName]             = useState('')
  const [cpf, setCpf]               = useState('')
  const [whatsApp, setWhatsApp]     = useState('')
  const [consent, setConsent]       = useState(false)
  const [showPrivacy, setShowPrivacy] = useState(false)

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
    } catch { /* ignore */ }
  }, [])

  function formatCpf(v: string) {
    const digits = v.replace(/\D/g, '').slice(0, 11)
    setCpf(digits)
    return digits
      .replace(/(\d{3})(\d)/, '$1.$2')
      .replace(/(\d{3})(\d)/, '$1.$2')
      .replace(/(\d{3})(\d{1,2})$/, '$1-$2')
  }

  function formatPhone(v: string) {
    const digits = v.replace(/\D/g, '').slice(0, 13)
    setWhatsApp(digits)
    return digits
  }

  function maskCpf(cpf: string) {
    const d = cpf.replace(/\D/g, '')
    if (d.length !== 11) return cpf
    return `***.***.${d.slice(6, 9)}-${d.slice(9)}`
  }

  async function handleLogin(isQuick = false) {
    const requestCpf      = isQuick ? savedUser!.cpf : cpf.replace(/\D/g, '')
    const requestName     = isQuick ? savedUser!.name : name
    const requestWhatsApp = isQuick ? savedUser!.whatsApp : whatsApp

    if (!isQuick && !consent) {
      toast.error('Você precisa aceitar os termos de privacidade.')
      return
    }

    setStep('loading')
    try {
      const { data } = await authApi.quickLogin(
        requestName,
        requestCpf,
        requestWhatsApp,
        mesa
      )

      saveAuth(data)
      localStorage.setItem(STORAGE_KEY, JSON.stringify({
        name: requestName,
        cpf: requestCpf,
        whatsApp: requestWhatsApp,
        displayCpf: maskCpf(requestCpf)
      }))

      toast.success('Entrada autorizada! Boas compras.', { icon: '🏰' })
      setTimeout(() => router.push('/cliente'), 800)
    } catch (err: any) {
      setStep(isQuick ? 'quick' : 'form')
      toast.error(err.response?.data?.message || 'Erro ao realizar login rápido.')
    }
  }

  return (
    <div className="min-h-screen bg-[#0f0f13] flex flex-col text-white">
      <Toaster position="top-center" />

      {/* ── BACKGROUND DECORATION ── */}
      <div className="fixed inset-0 overflow-hidden pointer-events-none opacity-20">
        <div className="absolute -top-24 -left-24 w-96 h-96 bg-[#42B6EE] rounded-full blur-[120px]" />
        <div className="absolute top-1/2 -right-24 w-64 h-64 bg-[#00F0A8] rounded-full blur-[100px]" />
      </div>

      <main className="flex-1 flex flex-col items-center justify-center p-6 relative z-10">
        
        {/* LOGO & TITLE */}
        <div className="text-center mb-10">
          <img src="/maikon-avatar.png" alt="Mascote" className="w-28 h-28 mx-auto mb-4 rounded-full object-cover object-top" />
          <h1 className="text-3xl font-black uppercase tracking-[0.2em] leading-tight text-white">
            Santuário<br/><span className="text-[#42B6EE]">Nerd</span>
          </h1>
          <div className="mt-4 flex items-center justify-center gap-2 text-gray-500">
            <div className="h-px w-8 bg-[#32323f]" />
            <span className="text-[10px] font-bold uppercase tracking-widest flex items-center gap-1.5">
              <TableProperties className="w-3 h-3 text-[#00F0A8]" /> Mesa {mesa}
            </span>
            <div className="h-px w-8 bg-[#32323f]" />
          </div>
        </div>

        {/* STEP: LOADING */}
        {step === 'loading' && (
          <div className="flex flex-col items-center gap-4 py-12">
            <div className="relative">
               <Loader2 className="w-12 h-12 animate-spin text-[#42B6EE]" />
               <Zap className="absolute inset-0 m-auto w-4 h-4 text-white animate-pulse" />
            </div>
            <p className="text-sm font-bold uppercase tracking-tighter text-gray-400">Consultando o oráculo...</p>
          </div>
        )}

        {/* STEP: QUICK LOGIN */}
        {step === 'quick' && savedUser && (
          <div className="w-full max-w-sm space-y-6 animate-in fade-in zoom-in-95 duration-300">
            <div className="bg-[#1e1e28] border border-[#32323f] rounded-3xl p-8 text-center shadow-xl">
               <div className="w-20 h-20 bg-[#42B6EE]/10 border-2 border-[#42B6EE]/30 rounded-full flex items-center justify-center mx-auto mb-4">
                  <Award className="w-10 h-10 text-[#42B6EE]" />
               </div>
               <p className="text-sm mb-1 font-medium" style={{ color: '#9CA3AF' }}>Seja bem-vindo de volta,</p>
               <h2 className="text-xl font-black mb-6" style={{ color: '#FFFFFF' }}>{savedUser.name.split(' ')[0]}!</h2>
               
               <button onClick={() => handleLogin(true)} className="w-full py-4 bg-[#42B6EE] text-white font-bold rounded-2xl shadow-lg shadow-[#42B6EE]/20 flex items-center justify-center gap-2 hover:scale-[1.02] transition-transform">
                  Entrar no Santuário <ChevronRight className="w-5 h-5" />
               </button>
               
               <button onClick={() => setStep('form')} className="mt-6 text-xs text-gray-500 font-bold uppercase tracking-widest hover:text-white transition-colors">
                  Não é você? Clique aqui
               </button>
            </div>
          </div>
        )}

        {/* STEP: COMPLETE FORM */}
        {step === 'form' && (
          <div className="w-full max-w-sm animate-in fade-in slide-in-from-bottom-4 duration-500">
            <div className="bg-[#1e1e28] border border-[#32323f] rounded-3xl p-8 shadow-xl space-y-6">
              <div className="space-y-1">
                <h2 className="text-lg font-bold text-white">Novo Acesso</h2>
                <p className="text-xs text-gray-500">Preencha os campos para abrir sua comanda.</p>
              </div>

              <div className="space-y-4">
                {/* Nome */}
                <div className="space-y-1.5 group">
                  <label className="text-[10px] font-black uppercase text-gray-500 ml-1 tracking-widest group-focus-within:text-[#42B6EE] transition-colors">Seu Nome</label>
                  <div className="relative">
                    <User className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                    <input
                      type="text" value={name} onChange={e => setName(e.target.value)}
                      placeholder="Como quer ser chamado?"
                      className="w-full bg-[#16161d] border border-[#252530] focus:border-[#42B6EE] rounded-2xl py-3.5 pl-11 pr-4 text-sm font-medium outline-none transition-all placeholder:text-gray-500"
                    />
                  </div>
                </div>

                {/* CPF */}
                <div className="space-y-1.5 group">
                  <label className="text-[10px] font-black uppercase text-gray-500 ml-1 tracking-widest group-focus-within:text-[#42B6EE] transition-colors">Seu CPF</label>
                  <div className="relative">
                    <Hash className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                    <input
                      type="tel" value={cpf}
                      onChange={e => setCpf(formatCpf(e.target.value))}
                      placeholder="000.000.000-00"
                      className="w-full bg-[#16161d] border border-[#252530] focus:border-[#42B6EE] rounded-2xl py-3.5 pl-11 pr-4 text-sm font-mono outline-none transition-all placeholder:text-gray-500"
                    />
                  </div>
                </div>

                {/* WhatsApp */}
                <div className="space-y-1.5 group">
                  <label className="text-[10px] font-black uppercase text-gray-500 ml-1 tracking-widest group-focus-within:text-[#42B6EE] transition-colors">WhatsApp</label>
                  <div className="relative">
                    <MessageCircle className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
                    <input
                      type="tel" value={whatsApp} onChange={e => setWhatsApp(formatPhone(e.target.value))}
                      placeholder="55 (17) 99999-9999"
                      className="w-full bg-[#16161d] border border-[#252530] focus:border-[#42B6EE] rounded-2xl py-3.5 pl-11 pr-4 text-sm outline-none transition-all placeholder:text-gray-500"
                    />
                  </div>
                </div>

                {/* Consentimento */}
                <label className="flex items-start gap-3 p-1 cursor-pointer select-none">
                  <input
                    type="checkbox" checked={consent} onChange={e => setConsent(e.target.checked)}
                    className="mt-1 w-4 h-4 rounded-md border-[#32323f] bg-[#16161d] text-[#42B6EE] focus:ring-[#42B6EE] transition-all"
                  />
                  <span className="text-[11px] text-gray-500 leading-tight">
                    Aceito que meus dados sejam usados para gerenciar minha comanda e pontos conforme a{' '}
                    <button type="button" onClick={() => setShowPrivacy(true)} className="text-[#42B6EE] font-bold hover:underline">
                      Política de Privacidade
                    </button>.
                  </span>
                </label>
              </div>

              <button
                onClick={() => handleLogin(false)}
                className="w-full py-4 bg-[#42B6EE] text-white font-bold rounded-2xl shadow-lg shadow-[#42B6EE]/20 flex items-center justify-center gap-2 hover:bg-[#2ea8e0] active:scale-[0.98] transition-all"
              >
                Abrir Comanda <ChevronRight className="w-5 h-5" />
              </button>
              
              {savedUser && (
                <button onClick={() => setStep('quick')} className="w-full flex items-center justify-center gap-2 text-xs font-bold uppercase tracking-widest text-gray-500 hover:text-white transition-colors">
                  <ArrowLeft className="w-3 h-3" /> Voltar
                </button>
              )}
            </div>
          </div>
        )}
      </main>

      {/* FOOTER */}
      <footer className="p-8 text-center text-[10px] text-gray-400 font-bold uppercase tracking-[0.3em]">
        Santuário Nerd © 2026
      </footer>

      {showPrivacy && <PrivacyModal onClose={() => setShowPrivacy(false)} />}
    </div>
  )
}
