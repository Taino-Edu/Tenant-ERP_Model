'use client'
import { useState } from 'react'
import { useParams, useRouter } from 'next/navigation'
import { authApi } from '@/lib/api'
import { saveAuth } from '@/lib/auth'
import toast, { Toaster } from 'react-hot-toast'
import { Smartphone, User, Hash, MessageCircle, Loader2, Sword, TableProperties } from 'lucide-react'

export default function MesaPage() {
  const params  = useParams()
  const router  = useRouter()
  const mesa    = decodeURIComponent(params.mesa as string)

  const [step, setStep]       = useState<'form' | 'loading'>('form')
  const [name, setName]       = useState('')
  const [cpf, setCpf]         = useState('')
  const [whatsApp, setWhatsApp] = useState('')

  function formatCpf(v: string) {
    return v.replace(/\D/g, '').slice(0, 11)
      .replace(/(\d{3})(\d)/, '$1.$2')
      .replace(/(\d{3})(\d)/, '$1.$2')
      .replace(/(\d{3})(\d{1,2})$/, '$1-$2')
  }
  function formatPhone(v: string) {
    return v.replace(/\D/g, '').slice(0, 13)
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const rawCpf = cpf.replace(/\D/g, '')
    if (rawCpf.length !== 11) { toast.error('CPF inválido'); return }
    setStep('loading')
    try {
      // data é AuthResponse com comandaId preenchido pelo quick-login
      const { data } = await authApi.quickLogin(name, rawCpf, whatsApp, mesa)
      saveAuth(data)
      // Salva o ID da comanda ativa para a página do cliente
      if (data.comandaId) {
        document.cookie = `activeComandaId=${data.comandaId}; path=/; max-age=86400`
      }
      router.push('/cliente')
    } catch (err: unknown) {
      setStep('form')
      toast.error('Erro ao entrar. Tente novamente.')
    }
  }

  return (
    <div className="min-h-screen bg-surface-900 flex items-center justify-center p-4">
      <Toaster position="top-right" toastOptions={{ style: { background: '#1e1e28', color: '#fff', border: '1px solid #32323f' }}} />

      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute -top-32 -right-32 w-64 h-64 bg-brand-600/10 rounded-full blur-3xl" />
        <div className="absolute -bottom-32 -left-32 w-64 h-64 bg-brand-800/10 rounded-full blur-3xl" />
      </div>

      <div className="relative w-full max-w-sm">
        {/* Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-14 h-14 bg-brand-600/20 border border-brand-500/30 rounded-2xl mb-4">
            <Sword className="w-7 h-7 text-brand-400" />
          </div>
          <h1 className="text-2xl font-bold text-white">CardGameStore</h1>
          <div className="flex items-center justify-center gap-2 mt-2 text-sm text-brand-300">
            <TableProperties className="w-4 h-4" />
            <span className="font-medium">{mesa}</span>
          </div>
          <p className="text-gray-400 text-sm mt-1">Preencha para abrir sua comanda</p>
        </div>

        {step === 'loading' ? (
          <div className="card text-center py-12 space-y-4">
            <div className="w-12 h-12 border-2 border-brand-500 border-t-transparent rounded-full animate-spin mx-auto" />
            <p className="text-gray-300">Abrindo sua comanda...</p>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="card space-y-4">
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

            <button type="submit" className="btn-primary w-full justify-center py-3 text-base mt-2">
              <Smartphone className="w-5 h-5" />
              Abrir Minha Comanda
            </button>

            <p className="text-center text-xs text-gray-600">
              Seus dados são usados apenas para identificação na loja.
            </p>
          </form>
        )}
      </div>
    </div>
  )
}
