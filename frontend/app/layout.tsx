import type { Metadata, Viewport } from 'next'
import { Nunito } from 'next/font/google'
import { headers } from 'next/headers'
import './globals.css'
import PWAInstallButton from '@/components/PWAInstallButton'
import CookieBanner from '@/components/CookieBanner'
import Footer from '@/components/Footer'
import VLibrasController from '@/components/VLibrasController'
import ClientProviders from '@/components/ClientProviders'
import { getTenantIconsForHost, withCacheBust } from '@/lib/serverSiteConfig'

const nunito = Nunito({
  subsets: ['latin'],
  variable: '--font-nunito',
  display: 'swap',
  weight: ['400', '500', '600', '700', '800', '900'],
})

export const viewport: Viewport = {
  themeColor: '#42B6EE',
  width: 'device-width',
  initialScale: 1,
}

// Ícone/favicon por tenant, resolvido pelo Host da requisição — o resto dos
// metadados (nome, descrição) continua fixo/genérico de propósito (fora de
// escopo aqui; o nome real da loja já é renderizado no client via
// useSiteConfig() em outros pontos da página). Fallback pros ícones estáticos
// de sempre em qualquer falha — getTenantIconsForHost nunca lança.
export async function generateMetadata(): Promise<Metadata> {
  const host = headers().get('host')
  const icons = await getTenantIconsForHost(host)

  const iconUrl = icons?.faviconUrl
    ? withCacheBust(icons.faviconUrl, icons.updatedAt)
    : '/icon.svg'

  return {
    title: { default: 'Minha Loja', template: '%s — Minha Loja' },
    description: 'Sistema de gestão para lojas e varejo',
    icons: {
      icon: [
        { url: iconUrl, type: icons?.faviconUrl ? undefined : 'image/svg+xml' },
      ],
      apple: iconUrl,
      shortcut: iconUrl,
    },
    appleWebApp: {
      capable: true,
      statusBarStyle: 'black-translucent',
      title: 'Minha Loja',
    },
  }
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR" className={nunito.variable}>
      <head>
        {/* iOS Safari PWA meta tags */}
        <meta name="mobile-web-app-capable" content="yes" />
        <meta name="apple-touch-fullscreen" content="yes" />
        {/* apple-touch-icon já vem de generateMetadata (icons.apple) — link estático
            removido daqui pra não duplicar/entrar em conflito com o dinâmico. */}
        {/* Aplica o tema salvo antes do primeiro render para evitar flash */}
        <script
          dangerouslySetInnerHTML={{
            __html: `(function(){try{var t=localStorage.getItem('theme');if(t==='dark'){document.documentElement.classList.remove('light')}else{document.documentElement.classList.add('light');if(!t)localStorage.setItem('theme','light')}}catch(e){document.documentElement.classList.add('light')}})();`
          }}
        />
      </head>
      <body>
        <ClientProviders>
        {/* Script VLibras — Acessibilidade (atributos customizados via spread para evitar erro TS) */}
        {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
        <div {...({ vw: 'true' } as any)} className="enabled">
          {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
          <div {...({ 'vw-access-button': 'true' } as any)} className="active"></div>
          {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
          <div {...({ 'vw-plugin-wrapper': 'true' } as any)}>
            <div className="vw-plugin-top-wrapper"></div>
          </div>
        </div>
        <script
          dangerouslySetInnerHTML={{
            __html: `
              window.VLibras = window.VLibras || {};
              var script = document.createElement('script');
              script.src = 'https://vlibras.gov.br/app/vlibras-plugin.js';
              script.onload = function() { new window.VLibras.Widget('https://vlibras.gov.br/app'); };
              document.body.appendChild(script);
            `
          }}
        />
        <VLibrasController />
        {children}
        {/* Rodapé com links legais (LGPD) — não aparece no painel admin */}
        <Footer />
        {/* Banner de consentimento de cookies (LGPD Art. 8°) */}
        <CookieBanner />
        {/* Botão flutuante de instalação PWA — aparece quando o Chrome suporta */}
        <PWAInstallButton />
        </ClientProviders>
      </body>
    </html>
  )
}
