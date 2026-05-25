'use client'
import { useState, Suspense } from 'react'
import { useSearchParams, useRouter } from 'next/navigation'
import { authApi } from '@/lib/api'
import toast from 'react-hot-toast'
import { Sword, Loader2, CheckCircle, KeyRound } from 'lucide-react'

function ResetPasswordForm() {
  const params   = useSearchParams()
  const router   = useRouter()
  const token    = params.get('token') ?? ''

  const [password,  setPassword]  = useState('')
  const [confirm,   setConfirm]   = useState('')
  const [loading,   setLoading]   = useState(false)
  const [done,      setDone]      = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (password !== confirm) { toast.error('As senhas não coincidem.'); return }
    if (password.length < 8)  { toast.error('Mínimo 8 caracteres.'); return }

    setLoading(true)
    try {
      await authApi.resetPassword(token, password)
      setDone(true)
    } catch {
      toast.error('Token inválido ou expirado. Solicite um novo link.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-surface-900 flex items-center justify-center p-4">
      <div className="w-full max-w-sm">
        <div className="flex items-center gap-3 mb-8 justify-center">
          <div className="w-10 h-10 bg-brand-600/30 border border-brand-500/30 rounded-xl flex items-center justify-center">
            <Sword className="w-5 h-5 text-brand-400" />
          </div>
          <span className="font-bold text-white text-xl">Santuário Nerd</span>
        </div>

        <div className="card">
          {done ? (
            <div className="text-center py-4 space-y-4">
              <CheckCircle className="w-12 h-12 text-accent-green mx-auto" />
              <div>
                <p className="font-bold text-white text-lg">Senha redefinida!</p>
                <p className="text-gray-400 text-sm mt-1">Sua nova senha foi salva com sucesso.</p>
              </div>
              <button onClick={() => router.push('/login')} className="btn-primary w-full justify-center">
                Ir para o login
              </button>
            </div>
          ) : (
            <>
              <div className="flex items-center gap-3 mb-6">
                <div className="w-9 h-9 bg-brand-600/20 rounded-lg flex items-center justify-center">
                  <KeyRound className="w-4 h-4 text-brand-400" />
                </div>
                <div>
                  <h1 className="font-bold text-white">Nova senha</h1>
                  <p className="text-xs text-gray-500">Mínimo 8 caracteres</p>
                </div>
              </div>

              <form onSubmit={handleSubmit} className="space-y-4">
                <div>
                  <label className="label">Nova senha</label>
                  <input
                    type="password"
                    className="input"
                    placeholder="••••••••"
                    value={password}
                    onChange={e => setPassword(e.target.value)}
                    required
                    minLength={8}
                  />
                </div>
                <div>
                  <label className="label">Confirmar senha</label>
                  <input
                    type="password"
                    className="input"
                    placeholder="••••••••"
                    value={confirm}
                    onChange={e => setConfirm(e.target.value)}
                    required
                    minLength={8}
                  />
                </div>
                <button type="submit" disabled={loading} className="btn-primary w-full justify-center">
                  {loading ? <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</> : 'Redefinir senha'}
                </button>
              </form>
            </>
          )}
        </div>
      </div>
    </div>
  )
}

export default function ResetPasswordPage() {
  return (
    <Suspense>
      <ResetPasswordForm />
    </Suspense>
  )
}
