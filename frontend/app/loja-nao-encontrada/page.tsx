'use client'
import { SearchX } from 'lucide-react'

export default function LojaNaoEncontradaPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-surface-900 px-6">
      <div className="max-w-md w-full text-center">
        <div className="w-16 h-16 mx-auto rounded-2xl bg-surface-700 border border-surface-500 flex items-center justify-center mb-6">
          <SearchX className="w-8 h-8 text-gray-400" />
        </div>
        <h1 className="text-xl font-bold text-white mb-2">Loja não encontrada</h1>
        <p className="text-gray-400 text-sm">
          O endereço acessado não corresponde a nenhuma loja ativa. Confira se o
          subdomínio está correto ou entre em contato com a loja.
        </p>
      </div>
    </div>
  )
}
