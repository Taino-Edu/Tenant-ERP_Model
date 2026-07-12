'use client'
import { Store } from 'lucide-react'

export default function LojaSuspensaPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-surface-900 px-6">
      <div className="max-w-md w-full text-center">
        <div className="w-16 h-16 mx-auto rounded-2xl bg-surface-700 border border-surface-500 flex items-center justify-center mb-6">
          <Store className="w-8 h-8 text-gray-400" />
        </div>
        <h1 className="text-xl font-bold text-white mb-2">Esta loja está temporariamente indisponível</h1>
        <p className="text-gray-400 text-sm">
          Volte mais tarde ou entre em contato diretamente com a loja para mais informações.
        </p>
      </div>
    </div>
  )
}
