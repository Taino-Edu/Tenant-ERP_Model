'use client'
// =============================================================================
// Footer.tsx — Rodapé global com links legais (LGPD)
// Oculto automaticamente no painel admin (/admin/*).
// =============================================================================

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useSiteConfig } from '@/contexts/SiteConfigContext'

export default function Footer() {
  const pathname = usePathname()
  const { site } = useSiteConfig()

  // Não exibe o footer no painel admin
  if (pathname?.startsWith('/admin') || pathname?.startsWith('/login')) return null

  return (
    <footer className="bg-[#1a0a2e] text-gray-400 py-5 px-4 text-xs">
      <div className="max-w-5xl mx-auto space-y-3">
        {/* Links legais */}
        <nav className="flex flex-wrap items-center justify-center gap-x-4 gap-y-1">
          <Link href="/privacidade" className="hover:text-white transition-colors">Política de Privacidade</Link>
          <span className="text-gray-700">|</span>
          <Link href="/termos" className="hover:text-white transition-colors">Termos de Uso</Link>
          <span className="text-gray-700">|</span>
          <Link href="/lgpd" className="hover:text-white transition-colors">Seus Direitos (LGPD)</Link>
          <span className="text-gray-700">|</span>
          <a href={`mailto:${site.contactEmail}`} className="hover:text-white transition-colors">
            {site.contactEmail}
          </a>
        </nav>
        {/* Copyright */}
        <p className="text-center text-gray-600">
          © {new Date().getFullYear()} <span className="text-gray-400">{site.siteName}</span> — {site.addressLine}. Todos os direitos reservados.
        </p>
      </div>
    </footer>
  )
}
