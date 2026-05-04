'use client'
import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { userApi, UserProfile } from '@/lib/api'
import { getUserName, clearAuth } from '@/lib/auth'
import { authApi } from '@/lib/api'
import { Star, User, Phone, CreditCard, Clock, AlertCircle, ArrowLeft, LogOut, CheckCircle } from 'lucide-react'
import Link from 'next/link'

export default function PerfilPage() {
  const router = useRouter()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    userApi.me()
      .then(r => setProfile(r.data))
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  async function handleLogout() {
    try { await authApi.logout() } catch {}
    clearAuth()
    router.push('/')
  }

  const isExpired = profile?.pointsExpired
  const hasPoints = profile && profile.pointsBalance > 0 && !isExpired

  return (
    <div className="min-h-screen bg-surface-900 pb-12">
      {/* Header */}
      <div className="bg-surface-800 border-b border-surface-500 px-4 py-4 sticky top-0 z-10">
        <div className="max-w-lg mx-auto flex items-center justify-between">
          <Link href="/cliente" className="flex items-center gap-2 text-gray-400 hover:text-white transition-colors">
            <ArrowLeft className="w-4 h-4" />
            <span className="text-sm">Voltar</span>
          </Link>
          <p className="font-bold text-white text-sm">Meu Perfil</p>
          <button onClick={handleLogout} className="flex items-center gap-1.5 text-gray-400 hover:text-red-400 transition-colors text-sm">
            <LogOut className="w-4 h-4" />
          </button>
        </div>
      </div>

      <div className="max-w-lg mx-auto px-4 py-6 space-y-5">
        {loading ? (
          <div className="flex justify-center py-16">
            <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          </div>
        ) : (
          <>
            {/* Avatar + nome */}
            <div className="card text-center">
              <div className="w-16 h-16 bg-brand-600/20 border border-brand-500/30 rounded-full flex items-center justify-center mx-auto mb-3">
                <User className="w-8 h-8 text-brand-400" />
              </div>
              <p className="font-bold text-white text-xl">{profile?.name ?? getUserName() ?? 'Visitante'}</p>
              <span className="text-xs text-gray-500 bg-surface-600 px-3 py-1 rounded-full mt-1 inline-block">Cliente</span>
            </div>

            {/* Saldo de pontos */}
            <div className="card">
              <div className="flex items-center gap-2 mb-4">
                <Star className="w-4.5 h-4.5 text-accent-gold" />
                <h2 className="font-semibold text-white text-sm">Meus Pontos</h2>
              </div>

              <div className="text-center py-4">
                <p className={`text-6xl font-black mb-1 ${isExpired ? 'text-gray-600' : 'text-accent-gold'}`}>
                  {isExpired ? '0' : (profile?.pointsBalance ?? 0)}
                </p>
                <p className="text-gray-500 text-sm">pontos disponíveis</p>

                {isExpired && (
                  <div className="flex items-center justify-center gap-1.5 mt-3 text-sm text-red-400 bg-red-500/10 border border-red-500/20 rounded-lg px-4 py-2">
                    <AlertCircle className="w-4 h-4 shrink-0" />
                    Seus pontos expiraram. Fale com o Maikon!
                  </div>
                )}

                {hasPoints && profile?.pointsExpiresAt && (
                  <div className="flex items-center justify-center gap-1.5 mt-3 text-sm text-gray-500">
                    <Clock className="w-3.5 h-3.5" />
                    Válido até {new Date(profile.pointsExpiresAt).toLocaleDateString('pt-BR', {
                      day: '2-digit', month: 'long', year: 'numeric'
                    })}
                  </div>
                )}

                {!profile?.pointsBalance && !isExpired && (
                  <p className="text-gray-600 text-sm mt-3">
                    Você ainda não tem pontos. Fale com o Maikon!
                  </p>
                )}
              </div>

              <div className="border-t border-surface-500 pt-4 mt-2">
                <div className="flex items-start gap-2.5 text-xs text-gray-500 leading-relaxed">
                  <CheckCircle className="w-3.5 h-3.5 text-accent-green shrink-0 mt-0.5" />
                  <span>Pontos são adicionados pelo Maikon e podem ser usados para comprar produtos na loja. Válidos por 30 dias.</span>
                </div>
              </div>
            </div>

            {/* Dados pessoais */}
            <div className="card space-y-3">
              <h2 className="font-semibold text-white text-sm mb-4">Dados da Conta</h2>
              {profile?.cpf && (
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 bg-surface-600 rounded-lg flex items-center justify-center shrink-0">
                    <CreditCard className="w-4 h-4 text-gray-400" />
                  </div>
                  <div>
                    <p className="text-xs text-gray-500">CPF</p>
                    <p className="text-sm text-white">
                      {profile.cpf.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4')}
                    </p>
                  </div>
                </div>
              )}
              {profile?.whatsApp && (
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 bg-surface-600 rounded-lg flex items-center justify-center shrink-0">
                    <Phone className="w-4 h-4 text-gray-400" />
                  </div>
                  <div>
                    <p className="text-xs text-gray-500">WhatsApp</p>
                    <p className="text-sm text-white">{profile.whatsApp}</p>
                  </div>
                </div>
              )}
              {profile?.createdAt && (
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 bg-surface-600 rounded-lg flex items-center justify-center shrink-0">
                    <Clock className="w-4 h-4 text-gray-400" />
                  </div>
                  <div>
                    <p className="text-xs text-gray-500">Cliente desde</p>
                    <p className="text-sm text-white">
                      {new Date(profile.createdAt).toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' })}
                    </p>
                  </div>
                </div>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  )
}
