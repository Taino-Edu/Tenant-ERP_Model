'use client'
// =============================================================================
// CookieBanner.tsx — Banner de consentimento de cookies (LGPD Art. 8°)
//
// Aparece na parte inferior da tela enquanto o usuário não se manifestou.
// Registra a escolha no localStorage e, se aceitar, reporta ao backend.
// =============================================================================

import Link from 'next/link'
import { useState, useEffect } from 'react'
import { X } from 'lucide-react'

const STORAGE_KEY = 'cookieConsent'
const API_BASE    = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'

export default function CookieBanner() {
  const [visible,  setVisible]  = useState(false)
  const [rejected, setRejected] = useState(false)

  // Verifica se o usuário já se manifestou — só mostra se nunca escolheu
  useEffect(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY)
      if (!saved) setVisible(true)
    } catch {
      // localStorage indisponível (modo privado restrito) — não exibe o banner
    }
  }, [])

  async function handleAccept() {
    try {
      localStorage.setItem(STORAGE_KEY, 'accepted')
    } catch { /* ignore */ }

    // Registra consentimento no backend (sem bloquear o fluxo)
    try {
      await fetch(`${API_BASE}/api/lgpd/consent`, {
        method:      'POST',
        headers:     { 'Content-Type': 'application/json' },
        credentials: 'include',
        body:        JSON.stringify({ accepted: true }),
      })
    } catch {
      // Falha na API não impede o uso — o localStorage já foi salvo
    }

    setVisible(false)
  }

  function handleReject() {
    try {
      localStorage.setItem(STORAGE_KEY, 'rejected')
    } catch { /* ignore */ }

    // Recusa não é enviada ao backend — nenhum cookie rastreável deve ser definido
    setRejected(true)

    // Fecha o banner após alguns segundos
    setTimeout(() => setVisible(false), 5000)
  }

  if (!visible) return null

  return (
    <div
      className="fixed bottom-0 left-0 right-0 z-[9999] bg-surface-400 border-t-2 border-brand-500 shadow-[0_-4px_24px_rgba(0,0,0,0.5)]"
      role="dialog"
      aria-label="Aviso de cookies"
    >
      <div className="max-w-5xl mx-auto px-5 py-4 flex flex-col sm:flex-row items-start sm:items-center gap-4">
        {/* Ícone decorativo */}
        <div className="hidden sm:flex w-9 h-9 rounded-full bg-brand-500/20 border border-brand-500/50 items-center justify-center shrink-0">
          <span className="text-base">🍪</span>
        </div>

        {/* Texto */}
        <div className="flex-1 text-sm leading-relaxed text-white">
          {rejected ? (
            <p className="text-yellow-300 font-medium">
              Você recusou os cookies. Algumas funcionalidades (como login e comandas)
              podem não funcionar corretamente sem cookies essenciais.
            </p>
          ) : (
            <p>
              Usamos <strong>cookies essenciais</strong> para funcionamento do sistema
              e identificação do usuário. Ao continuar, você concorda com nossa{' '}
              <Link
                href="/privacidade"
                className="underline text-brand-400 hover:text-brand-300 transition-colors font-medium"
              >
                Política de Privacidade
              </Link>
              .
            </p>
          )}
        </div>

        {/* Botões */}
        {!rejected && (
          <div className="flex items-center gap-3 shrink-0">
            <button
              onClick={handleReject}
              className="px-4 py-2 text-sm font-medium border border-gray-300 text-gray-100 rounded-lg hover:bg-white/15 transition-all duration-150"
            >
              Recusar
            </button>
            <button
              onClick={handleAccept}
              className="px-5 py-2 text-sm font-bold bg-brand-600 text-white rounded-lg hover:bg-brand-700 transition-colors"
            >
              Aceitar
            </button>
          </div>
        )}

        {/* Fechar (quando rejeitado) */}
        {rejected && (
          <button
            onClick={() => setVisible(false)}
            className="shrink-0 text-gray-200 hover:text-white transition-colors p-1.5 rounded-lg hover:bg-white/15"
            aria-label="Fechar"
          >
            <X className="w-5 h-5" />
          </button>
        )}
      </div>
    </div>
  )
}
