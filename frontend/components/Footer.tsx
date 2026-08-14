'use client'
// =============================================================================
// Footer.tsx — Rodapé global com links legais (LGPD)
// Oculto automaticamente no painel admin (/admin/*).
// =============================================================================

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import { OPEN_COOKIE_SETTINGS_EVENT } from '@/lib/cookieConsent'

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
        {/* Links legais.
            `py-1.5` em cada item (alvo de ~28px) e os separadores "|" escondidos
            no celular: em 375px os seis links quebram em três fileiras, e com as
            barras no meio o usuário mira num alvo de 16px de altura cercado por
            outros dois. Sem as barras, a quebra por si só já separa os itens. */}
        <nav className="flex flex-wrap items-center justify-center gap-x-4 gap-y-0 sm:gap-y-1">
          <Link href="/privacidade" className="py-1.5 transition-colors hover:text-brand-600">Política de Privacidade</Link>
          <span className="hidden text-[#0C3D5A]/15 sm:inline">|</span>
          <Link href="/termos" className="py-1.5 transition-colors hover:text-brand-600">Termos de Uso</Link>
          <span className="hidden text-[#0C3D5A]/15 sm:inline">|</span>
          <Link href="/cookies" className="py-1.5 transition-colors hover:text-brand-600">Política de Cookies</Link>
          <span className="hidden text-[#0C3D5A]/15 sm:inline">|</span>
          <button type="button" onClick={() => window.dispatchEvent(new Event(OPEN_COOKIE_SETTINGS_EVENT))} className="py-1.5 transition-colors hover:text-brand-600">Preferências de cookies</button>
          <span className="hidden text-[#0C3D5A]/15 sm:inline">|</span>
          <Link href="/lgpd" className="py-1.5 transition-colors hover:text-brand-600">Seus Direitos (LGPD)</Link>
          <span className="hidden text-[#0C3D5A]/15 sm:inline">|</span>
          <a href={`mailto:${site.contactEmail}`} className="py-1.5 transition-colors hover:text-brand-600">
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
