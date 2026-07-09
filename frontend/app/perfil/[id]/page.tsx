'use client'

import { useEffect, useState } from 'react'
import { useParams, useRouter } from 'next/navigation'
import { publicProfileApi, PublicProfileDto } from '@/lib/api'
import {
  ArrowLeft, User as UserIcon, Star, ShoppingBag,
  Loader2, Calendar,
} from 'lucide-react'

export default function PublicProfilePage() {
  const { id } = useParams<{ id: string }>()
  const router  = useRouter()

  const [profile, setProfile] = useState<PublicProfileDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [notFound, setNotFound] = useState(false)

  useEffect(() => {
    if (!id) return
    publicProfileApi.get(id)
      .then(r => setProfile(r.data))
      .catch(e => {
        if (e?.response?.status === 404) setNotFound(true)
      })
      .finally(() => setLoading(false))
  }, [id])

  if (loading) return (
    <div className="min-h-screen bg-surface-900 flex items-center justify-center">
      <Loader2 className="w-8 h-8 animate-spin text-brand-400" />
    </div>
  )

  if (notFound || !profile) return (
    <div className="min-h-screen bg-surface-900 flex flex-col items-center justify-center gap-4 text-center p-8">
      <UserIcon className="w-16 h-16 text-surface-600" />
      <h1 className="text-2xl font-bold text-white">Perfil não encontrado</h1>
      <p className="text-gray-400">Este usuário não existe ou tem o perfil oculto.</p>
      <button onClick={() => router.back()} className="btn-secondary">Voltar</button>
    </div>
  )

  const memberYear = new Date(profile.memberSince).getFullYear()

  return (
    <div className="min-h-screen bg-surface-900 pb-20">
      {/* Banner + avatar */}
      <div className="relative">
        <div className="h-36 bg-gradient-to-br from-brand-900 via-surface-800 to-surface-900" />
        <div className="absolute -bottom-14 left-6">
          {profile.profileImageUrl ? (
            <img
              src={profile.profileImageUrl}
              alt={profile.name}
              className="w-24 h-24 rounded-full border-4 border-surface-900 object-cover shadow-xl"
            />
          ) : (
            <div className="w-24 h-24 rounded-full border-4 border-surface-900 bg-surface-700 flex items-center justify-center shadow-xl">
              <UserIcon className="w-10 h-10 text-surface-400" />
            </div>
          )}
        </div>
      </div>

      <div className="pt-16 px-6">
        {/* Nome e info */}
        <div className="flex items-start justify-between">
          <div>
            <h1 className="text-2xl font-black text-white">{profile.name}</h1>
            <div className="flex items-center gap-3 mt-1 text-xs text-gray-400">
              <span className="flex items-center gap-1">
                <Calendar className="w-3 h-3" /> Membro desde {memberYear}
              </span>
            </div>
          </div>
          <button onClick={() => router.back()} className="p-2 rounded-xl bg-surface-800 hover:bg-surface-700 transition-colors text-gray-400 mt-1">
            <ArrowLeft className="w-4 h-4" />
          </button>
        </div>

        {/* Stats rápidos */}
        <div className="grid grid-cols-2 gap-3 mt-5">
          <div className="card text-center">
            <p className="text-xl font-black text-brand-300 flex items-center justify-center gap-1.5">
              <ShoppingBag className="w-4 h-4" /> {profile.totalCompras}
            </p>
            <p className="text-[11px] text-gray-400 mt-0.5">Compras realizadas</p>
          </div>
          <div className="card text-center">
            <p className="text-xl font-black text-yellow-400 flex items-center justify-center gap-1.5">
              <Star className="w-4 h-4" /> {profile.pointsBalance}
            </p>
            <p className="text-[11px] text-gray-400 mt-0.5">Pontos de fidelidade</p>
          </div>
        </div>
      </div>
    </div>
  )
}
