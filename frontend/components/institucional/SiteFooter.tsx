'use client'
// =============================================================================
// SiteFooter.tsx — Rodapé do site público (institucional e afiliados).
// =============================================================================

import Link from 'next/link'
import { Instagram, Linkedin, Youtube, Facebook } from 'lucide-react'
import Logo from '@/components/Logo'
import { CONTACTS, SOCIAL_PROFILES, type InstitucionalTheme } from '@/lib/institucional'

/** O lucide não tem ícone do TikTok (conferido na versão em uso: nenhuma
 *  exportação casa com /tik|tok/), então este é desenhado à mão. `currentColor`
 *  para acompanhar o tema e o hover como os outros. */
function TikTok({ size = 19 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M16.5 3a5.6 5.6 0 0 0 4.5 4.4v2.9a8.5 8.5 0 0 1-4.5-1.4v6.4a5.9 5.9 0 1 1-5.9-5.9c.3 0 .6 0 .9.1v3a2.9 2.9 0 1 0 2 2.8V3h3Z" />
    </svg>
  )
}

const ICONS: Record<string, (props: { size?: number }) => JSX.Element> = {
  instagram: p => <Instagram size={p.size ?? 19} />,
  tiktok:    p => <TikTok size={p.size ?? 19} />,
  youtube:   p => <Youtube size={p.size ?? 19} />,
  facebook:  p => <Facebook size={p.size ?? 19} />,
  linkedin:  p => <Linkedin size={p.size ?? 19} />,
}

export default function SiteFooter({ theme }: { theme: InstitucionalTheme }) {
  return (
    <footer className={`border-t px-5 py-10 lg:px-8 ${theme.border}`}>
      <div className={`mx-auto flex max-w-7xl flex-col gap-6 text-sm sm:flex-row sm:items-center sm:justify-between ${theme.muted}`}>
        <div>
          {/* Mesma razão do cabeçalho: o lockup já contém o nome escrito. */}
          <Logo className="h-14 w-[104px]" title="3E Systen" />
          <p className="mt-3">Octus · gestão completa com a identidade da sua empresa.</p>
        </div>

        <div className="flex flex-wrap items-center gap-5">
          {/* Renderizado da mesma lista que alimenta o `sameAs` do JSON-LD —
              perfil novo aparece aqui e é declarado ao Google de uma vez só. */}
          {SOCIAL_PROFILES.map(perfil => {
            const Icone = ICONS[perfil.key]
            if (!Icone) return null
            return (
              <a key={perfil.key} href={perfil.url} target="_blank" rel="noreferrer me"
                aria-label={perfil.label} className="rounded transition octus-accent-hover">
                <Icone />
              </a>
            )
          })}
          <a href={`mailto:${CONTACTS.email}`} className="transition octus-accent-hover">Contato</a>
          <Link href="/parceiros" className="transition octus-accent-hover">Afiliados</Link>
          <Link href="/termos" className="transition octus-accent-hover">Termos</Link>
          <Link href="/privacidade" className="transition octus-accent-hover">Privacidade</Link>
        </div>
      </div>
    </footer>
  )
}
