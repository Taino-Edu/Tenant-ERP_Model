'use client'
import { useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { isLoggedIn, isAdmin } from '@/lib/auth'

export default function Home() {
  const router = useRouter()
  useEffect(() => {
    if (!isLoggedIn()) router.replace('/login')
    else if (isAdmin()) router.replace('/admin/dashboard')
    else router.replace('/cliente')
  }, [router])
  return (
    <div className="min-h-screen flex items-center justify-center bg-surface-900">
      <div className="flex flex-col items-center gap-4">
        <div className="w-10 h-10 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        <span className="text-gray-400 text-sm">Carregando...</span>
      </div>
    </div>
  )
}
