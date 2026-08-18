'use client'
// =============================================================================
// SiteFooter.tsx — Rodapé do site público (institucional e afiliados).
// =============================================================================

import Link from 'next/link'
import { Instagram, Linkedin } from 'lucide-react'
import Logo from '@/components/Logo'
import { CONTACTS, type InstitucionalTheme } from '@/lib/institucional'

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
          <a href={CONTACTS.instagram} target="_blank" rel="noreferrer" aria-label="Instagram" className="rounded transition octus-accent-hover"><Instagram size={19} /></a>
          <a href={CONTACTS.linkedin} target="_blank" rel="noreferrer" aria-label="LinkedIn" className="rounded transition octus-accent-hover"><Linkedin size={19} /></a>
          <Link href="/parceiros" className="transition octus-accent-hover">Afiliados</Link>
          <Link href="/termos" className="transition octus-accent-hover">Termos</Link>
          <Link href="/privacidade" className="transition octus-accent-hover">Privacidade</Link>
        </div>
      </div>
    </footer>
  )
}
