'use client'
// =============================================================================
// SiteHeader.tsx — Cabeçalho do site público (institucional e afiliados).
//
// Traz a marca desenhada, não só o texto: até aqui o topo mostrava "3E Systen"
// escrito, e o único lugar do produto com a logo aplicada era a tela de login.
// O componente Logo pinta o PNG monocromático por `mask-image`, então a mesma
// arte serve o tema claro e o escuro sem um segundo arquivo.
// =============================================================================

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useState } from 'react'
import { Menu, Moon, Sun, X } from 'lucide-react'
import Logo from '@/components/Logo'
import { NAV_LINKS, type InstitucionalTheme } from '@/lib/institucional'

export default function SiteHeader({
  theme,
  isDark,
  onToggleTheme,
}: {
  theme: InstitucionalTheme
  isDark: boolean
  onToggleTheme: () => void
}) {
  const [menuOpen, setMenuOpen] = useState(false)
  const pathname = usePathname()

  // Links de âncora funcionam dentro da própria página; vindo de /parceiros
  // eles precisam navegar para /institucional antes de rolar. Guardar o
  // pathname aqui evita que o menu de afiliados aponte para âncoras que não
  // existem naquela tela.
  const isHome = pathname === '/institucional'
  const hrefFor = (href: string) =>
    isHome && href.startsWith('/institucional#') ? href.slice('/institucional'.length) : href

  return (
    <>
      {/* Pular navegação: são cinco links e mais três botões antes do conteúdo,
          repetidos em toda página do site. Quem usa teclado ou leitor de tela
          percorreria os oito a cada troca de tela. Fica invisível até receber
          foco. */}
      <a
        href="#conteudo"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded-xl focus:bg-octus-600 focus:px-4 focus:py-2.5 focus:font-bold focus:text-white"
      >
        Pular para o conteúdo
      </a>
      <header className={`sticky top-0 z-40 border-b backdrop-blur-xl ${theme.header}`}>
      <div className="mx-auto flex h-[72px] max-w-7xl items-center justify-between px-5 lg:px-8">
        {/* A arte NÃO é só um símbolo: é o lockup completo, polvo sobre o
            letreiro "3E SYSTEN". Repetir o nome ao lado em texto escreveria a
            marca duas vezes. Daí o tamanho generoso — a 48px de altura o
            letreiro dentro do PNG fica com ~19px e continua legível. */}
        <Link
          href="/institucional"
          aria-label="3E Systen — início"
          className="flex items-center rounded-lg outline-none focus-visible:ring-2 focus-visible:ring-octus-500 focus-visible:ring-offset-4"
        >
          <Logo className="h-12 w-[89px]" title="3E Systen" />
        </Link>

        <nav className="hidden items-center gap-7 lg:flex" aria-label="Navegação principal">
          {NAV_LINKS.map(link => {
            const active = link.href === pathname
            return (
              <Link
                key={link.href}
                href={hrefFor(link.href)}
                aria-current={active ? 'page' : undefined}
                className={`rounded text-sm font-semibold outline-none transition octus-accent-hover focus-visible:ring-2 focus-visible:ring-octus-500 focus-visible:ring-offset-4 ${
                  active ? 'octus-accent' : theme.body
                }`}
              >
                {link.label}
              </Link>
            )
          })}
        </nav>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={onToggleTheme}
            aria-label={isDark ? 'Ativar tema claro' : 'Ativar tema escuro'}
            className={`rounded-xl border p-2.5 outline-none transition focus-visible:ring-2 focus-visible:ring-octus-500 ${theme.outline}`}
          >
            {isDark ? <Sun size={18} /> : <Moon size={18} />}
          </button>
          <Link href="/login" className={`hidden rounded-xl border px-4 py-2.5 text-sm font-bold sm:block ${theme.outline}`}>
            Entrar
          </Link>
          <Link
            href="/institucional#contato"
            className="hidden rounded-xl bg-octus-600 px-4 py-2.5 text-sm font-bold text-white transition hover:bg-octus-700 md:block"
          >
            Teste grátis
          </Link>
          <button
            type="button"
            onClick={() => setMenuOpen(open => !open)}
            aria-expanded={menuOpen}
            aria-label={menuOpen ? 'Fechar menu' : 'Abrir menu'}
            className={`rounded-xl border p-2.5 lg:hidden ${theme.outline}`}
          >
            {menuOpen ? <X size={18} /> : <Menu size={18} />}
          </button>
        </div>
      </div>

      {menuOpen && (
        <nav className={`border-t px-5 py-5 lg:hidden ${theme.border}`} aria-label="Navegação principal (celular)">
          <div className="mx-auto flex max-w-7xl flex-col gap-4">
            {NAV_LINKS.map(link => (
              <Link
                key={link.href}
                href={hrefFor(link.href)}
                onClick={() => setMenuOpen(false)}
                className={`font-semibold ${link.href === pathname ? 'octus-accent' : theme.body}`}
              >
                {link.label}
              </Link>
            ))}
            <Link href="/login" onClick={() => setMenuOpen(false)} className="font-semibold octus-accent">Entrar</Link>
            <Link href="/institucional#contato" onClick={() => setMenuOpen(false)} className="font-semibold octus-accent">
              Começar teste grátis
            </Link>
          </div>
        </nav>
      )}
      </header>
    </>
  )
}
