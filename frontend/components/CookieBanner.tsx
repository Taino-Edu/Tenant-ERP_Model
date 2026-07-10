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
      await fetch('/api/lgpd/consent', {
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

  // Card branco flutuante com texto escuro — legível sobre qualquer fundo
  // (vitrine clara/escura, institucional), sem virar um barrão preto no rodapé.
  return (
    <div
      className="fixed bottom-4 left-4 right-4 z-[9999] sm:left-1/2 sm:right-auto sm:w-full sm:max-w-2xl sm:-translate-x-1/2"
      role="dialog"
      aria-label="Aviso de cookies"
    >
      <div className="flex items-center gap-3 rounded-xl border border-[#0C3D5A]/15 bg-white px-4 py-3 shadow-lg shadow-[#0C3D5A]/10">
        {/* Ícone */}
        <span className="text-lg shrink-0">🍪</span>

        {/* Texto */}
        <p className="flex-1 text-sm leading-snug text-[#22384A]">
          {rejected ? (
            <span className="font-medium text-amber-700">
              Cookies recusados. Login e comandas podem não funcionar sem cookies essenciais.
            </span>
          ) : (
            <>
              Usamos <strong>cookies essenciais</strong> para autenticação.{' '}
              <Link href="/privacidade" className="underline text-brand-600 hover:text-brand-700 transition-colors">
                Política de Privacidade
              </Link>
            </>
          )}
        </p>

        {/* Botões */}
        {!rejected ? (
          <div className="flex items-center gap-2 shrink-0">
            <button
              onClick={handleReject}
              className="px-3 py-1.5 text-xs font-medium border border-[#0C3D5A]/25 text-[#3E5A6E] rounded-lg hover:bg-brand-50 transition-colors"
            >
              Recusar
            </button>
            <button
              onClick={handleAccept}
              className="px-4 py-1.5 text-xs font-bold bg-brand-600 text-white rounded-lg hover:bg-brand-700 transition-colors"
            >
              Aceitar
            </button>
          </div>
        ) : (
          <button
            onClick={() => setVisible(false)}
            className="shrink-0 text-[#6B8598] hover:text-[#0C3D5A] transition-colors p-1 rounded-lg hover:bg-brand-50"
            aria-label="Fechar"
          >
            <X className="w-4 h-4" />
          </button>
        )}
      </div>
    </div>
  )
}
