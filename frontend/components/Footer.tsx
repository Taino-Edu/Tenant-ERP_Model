'use client'
// =============================================================================
// Footer.tsx — Rodapé global com links legais (LGPD)
// Oculto automaticamente no painel admin (/admin/*).
// =============================================================================

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import { OPEN_COOKIE_SETTINGS_EVENT } from '@/lib/cookieConsent'
import Logo from '@/components/Logo'
import { ROOT_DOMAIN } from '@/lib/institucional'

// Site da plataforma, montado do mesmo NEXT_PUBLIC_ROOT_DOMAIN que o resto do
// app usa pra compor subdomínio de loja — sem o domínio configurado o logo fica
// sem link, como era antes, em vez de apontar pra um href quebrado.
const PLATFORM_SITE_URL = ROOT_DOMAIN ? `https://${ROOT_DOMAIN}` : null

export default function Footer() {
  const pathname = usePathname()
  const { site } = useSiteConfig()

  // Não exibe o footer no painel admin
  if (pathname?.startsWith('/admin') || pathname?.startsWith('/login')) return null

  // Rotas que seguem o tema claro/escuro (usam os tokens surface-*/text-* do
  // admin). O rodapé é renderizado pelo layout RAIZ, então fica fora do
  // `.admin-shell` da página e não herda o escopo do tema — daí ele próprio
  // vestir a classe quando está numa dessas rotas. Sem isso, uma faixa branca
  // fixa colava no fim de uma página escura.
  const rotaComTema = ['/entrar', '/cadastro', '/reset-password', '/primeiro-acesso']
    .some(r => pathname === r || pathname?.startsWith(r + '/'))

  // Claro e neutro — combina com a identidade branco/azul sem virar uma
  // faixa escura destoando no fim de páginas claras. A classe js-global-footer
  // permite à página institucional (que tem footer próprio) escondê-lo via CSS.
  return (
    <footer className={`js-global-footer border-t py-5 px-4 text-xs${rotaComTema ? ' admin-shell' : ''}`}>
      <div className="max-w-5xl mx-auto space-y-3">
        {/* Marca da plataforma. Só aparece quando o lojista NÃO tem logo
            próprio — quem personalizou a loja não deve ver o Octus no rodapé
            da própria vitrine. Mesma regra da tela de login. */}
        {!site.logoUrl && (
          <div className="flex justify-center">
            {/* Clicável: o polvo é a marca da plataforma, e um logo que não leva
                a lugar nenhum é a primeira coisa que o visitante tenta clicar.
                Vai pro site do Octus (não pra home da loja) porque é ali que ele
                é a marca — quem chegou pela vitrine do lojista e clica aqui está
                perguntando "que sistema é esse?". <a> comum em vez de <Link>: é
                navegação pra outra origem, que o roteador do Next não cobre. */}
            {PLATFORM_SITE_URL ? (
              <a
                href={PLATFORM_SITE_URL}
                target="_blank"
                rel="noreferrer"
                aria-label="Octus — site da plataforma"
                className="transition-opacity hover:opacity-100"
              >
                <Logo className="h-7 w-[52px] opacity-90" />
              </a>
            ) : (
              <Logo className="h-7 w-[52px] opacity-90" />
            )}
          </div>
        )}
        {/* Links legais.
            `py-1.5` em cada item (alvo de ~28px) e os separadores "|" escondidos
            no celular: em 375px os seis links quebram em três fileiras, e com as
            barras no meio o usuário mira num alvo de 16px de altura cercado por
            outros dois. Sem as barras, a quebra por si só já separa os itens. */}
        <nav className="flex flex-wrap items-center justify-center gap-x-4 gap-y-0 sm:gap-y-1">
          <Link href="/privacidade" className="py-1.5 transition-colors hover:text-brand-600">Política de Privacidade</Link>
          <span className="js-footer-sep hidden sm:inline">|</span>
          <Link href="/termos" className="py-1.5 transition-colors hover:text-brand-600">Termos de Uso</Link>
          <span className="js-footer-sep hidden sm:inline">|</span>
          <Link href="/cookies" className="py-1.5 transition-colors hover:text-brand-600">Política de Cookies</Link>
          <span className="js-footer-sep hidden sm:inline">|</span>
          <button type="button" onClick={() => window.dispatchEvent(new Event(OPEN_COOKIE_SETTINGS_EVENT))} className="py-1.5 transition-colors hover:text-brand-600">Preferências de cookies</button>
          <span className="js-footer-sep hidden sm:inline">|</span>
          <Link href="/lgpd" className="py-1.5 transition-colors hover:text-brand-600">Seus Direitos (LGPD)</Link>
          <span className="js-footer-sep hidden sm:inline">|</span>
          <a href={`mailto:${site.contactEmail}`} className="py-1.5 transition-colors hover:text-brand-600">
            {site.contactEmail}
          </a>
        </nav>
        {/* Copyright */}
        <p className="js-footer-copy text-center">
          © {new Date().getFullYear()} <span className="js-footer-brand">{site.siteName}</span> — {site.addressLine}. Todos os direitos reservados.
        </p>
      </div>
    </footer>
  )
}
