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

  // Claro e neutro — combina com a identidade branco/azul sem virar uma
  // faixa escura destoando no fim de páginas claras. A classe js-global-footer
  // permite à página institucional (que tem footer próprio) escondê-lo via CSS.
  return (
    <footer className="js-global-footer bg-white border-t border-[#0C3D5A]/10 text-[#6B8598] py-5 px-4 text-xs">
      <div className="max-w-5xl mx-auto space-y-3">
        {/* Links legais */}
        <nav className="flex flex-wrap items-center justify-center gap-x-4 gap-y-1">
          <Link href="/privacidade" className="hover:text-brand-600 transition-colors">Política de Privacidade</Link>
          <span className="text-[#0C3D5A]/15">|</span>
          <Link href="/termos" className="hover:text-brand-600 transition-colors">Termos de Uso</Link>
          <span className="text-[#0C3D5A]/15">|</span>
          <Link href="/lgpd" className="hover:text-brand-600 transition-colors">Seus Direitos (LGPD)</Link>
          <span className="text-[#0C3D5A]/15">|</span>
          <a href={`mailto:${site.contactEmail}`} className="hover:text-brand-600 transition-colors">
            {site.contactEmail}
          </a>
        </nav>
        {/* Copyright */}
        <p className="text-center text-[#8FA6B5]">
          © {new Date().getFullYear()} <span className="text-[#3E5A6E]">{site.siteName}</span> — {site.addressLine}. Todos os direitos reservados.
        </p>
      </div>
    </footer>
  )
}
