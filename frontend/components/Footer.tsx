'use client'
// =============================================================================
// Footer.tsx — Rodapé global com links legais (LGPD)
// Oculto automaticamente no painel admin (/admin/*).
// =============================================================================

import Link from 'next/link'
import { usePathname } from 'next/navigation'

export default function Footer() {
  const pathname = usePathname()

  // Não exibe o footer no painel admin
  if (pathname?.startsWith('/admin') || pathname?.startsWith('/login')) return null

  return (
    <footer className="bg-[#1a0a2e] text-gray-400 py-6 px-4 text-sm">
      <div className="max-w-5xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-3">
        <p className="text-gray-500">
          © {new Date().getFullYear()}{' '}
          <span className="text-white font-medium">Santuário Nerd</span>{' '}
          — Todos os direitos reservados. José Bonifácio, SP.
        </p>
        <nav className="flex flex-wrap items-center gap-x-4 gap-y-1 justify-center">
          <Link href="/privacidade" className="hover:text-white transition-colors">
            Política de Privacidade
          </Link>
          <span className="text-gray-600 hidden sm:inline">|</span>
          <Link href="/termos" className="hover:text-white transition-colors">
            Termos de Uso
          </Link>
          <span className="text-gray-600 hidden sm:inline">|</span>
          <Link href="/lgpd" className="hover:text-white transition-colors">
            Seus Direitos (LGPD)
          </Link>
          <span className="text-gray-600 hidden sm:inline">|</span>
          <a
            href="mailto:contato@santuarionerd.com.br"
            className="hover:text-white transition-colors"
          >
            contato@santuarionerd.com.br
          </a>
        </nav>
      </div>
    </footer>
  )
}
