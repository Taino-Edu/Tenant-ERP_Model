'use client'

// =============================================================================
// not-found.tsx — 404 do app inteiro. Antes disso o Next.js servia a página
// preta padrão dele ("This page could not be found"), em inglês e sem link de
// volta — nada a ver com o resto do site.
//
// Não confundir com /loja-nao-encontrada, que é outra coisa: aquela é o
// subdomínio que não bate com nenhum tenant (404 vindo do
// TenantResolutionMiddleware, no backend). Esta aqui é rota inexistente dentro
// de uma loja que existe.
// =============================================================================

import Link from 'next/link'
import { FileQuestion, Home } from 'lucide-react'

export default function NotFound() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-surface-900 px-6">
      <div className="max-w-md w-full text-center">
        <div className="w-16 h-16 mx-auto rounded-2xl bg-surface-700 border border-surface-500 flex items-center justify-center mb-6">
          <FileQuestion className="w-8 h-8 text-gray-400" />
        </div>

        <h1 className="text-xl font-bold text-white mb-2">Página não encontrada</h1>

        <p className="text-gray-400 text-sm mb-6">
          O endereço acessado não existe ou foi movido.
        </p>

        <Link href="/" className="btn-primary">
          <Home className="w-4 h-4" />
          Voltar ao início
        </Link>
      </div>
    </div>
  )
}
