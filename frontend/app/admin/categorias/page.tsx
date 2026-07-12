'use client'
import { useEffect } from 'react'
import { useRouter } from 'next/navigation'

// Categorias virou uma aba dentro de Estoque — esta rota fica só como
// redirect pra não quebrar links/favoritos antigos.
export default function CategoriasRedirect() {
  const router = useRouter()
  useEffect(() => { router.replace('/admin/estoque?tab=categorias') }, [router])
  return null
}
