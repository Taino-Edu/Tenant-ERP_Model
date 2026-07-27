'use client'

// =============================================================================
// error.tsx (raiz) — Pega qualquer erro de render que estoure fora do /admin:
// vitrine, login, cadastro, /cliente, /plataforma, /mesa, portal do contador.
// Segmentos com boundary próprio (ver app/admin/error.tsx) têm prioridade;
// este só recebe o que ninguém mais pegou, incluindo erro dos layouts deles.
//
// Sem este arquivo, uma exceção de render (o clássico .map() num campo que veio
// null da API) derrubava a árvore inteira e o lojista via tela branca, sem
// mensagem e sem como se recuperar a não ser fechando o navegador.
// =============================================================================

import { useEffect } from 'react'
import { AlertTriangle, RotateCw } from 'lucide-react'

export default function RootError({
  error,
  reset,
}: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  useEffect(() => {
    // Em produção a mensagem real é omitida do bundle pelo Next.js (só sobra o
    // digest); em dev isso aqui é o que mostra o erro de verdade no console.
    console.error('[error boundary]', error)
  }, [error])

  return (
    <div className="min-h-screen flex items-center justify-center bg-surface-900 px-6">
      <div className="max-w-md w-full text-center">
        <div className="w-16 h-16 mx-auto rounded-2xl bg-surface-700 border border-surface-500 flex items-center justify-center mb-6">
          <AlertTriangle className="w-8 h-8 text-gray-400" />
        </div>

        <h1 className="text-xl font-bold text-white mb-2">Algo deu errado</h1>

        <p className="text-gray-400 text-sm mb-6">
          Esta página não conseguiu carregar. Tente novamente — se o problema
          continuar, entre em contato com o suporte.
        </p>

        <button type="button" onClick={reset} className="btn-primary">
          <RotateCw className="w-4 h-4" />
          Tentar novamente
        </button>

        {error.digest && (
          <p className="text-xs text-gray-600 mt-6">
            Código do erro: <code>{error.digest}</code>
          </p>
        )}
      </div>
    </div>
  )
}
