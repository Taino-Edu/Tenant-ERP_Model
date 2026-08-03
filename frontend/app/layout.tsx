import type { Metadata, Viewport } from 'next'
import { headers } from 'next/headers'
import Script from 'next/script'
import './globals.css'
import PWAInstallButton from '@/components/PWAInstallButton'
import CookieBanner from '@/components/CookieBanner'
import Footer from '@/components/Footer'
import VLibrasController from '@/components/VLibrasController'
import ClientProviders from '@/components/ClientProviders'
import { getTenantIconsForHost, withCacheBust } from '@/lib/serverSiteConfig'

export const viewport: Viewport = {
  themeColor: '#42B6EE',
  width: 'device-width',
  initialScale: 1,
}

// Favicon + título/descrição por tenant, resolvidos pelo Host da requisição.
// Páginas públicas (Home, /cadastro, /login etc.) não têm generateMetadata
// própria, então herdam esse default — é ele quem aparece na aba do
// navegador e no snippet do Google pra cada loja. Fallback genérico
// ("Octus") em qualquer falha — getTenantIconsForHost nunca lança.
export async function generateMetadata(): Promise<Metadata> {
  const host = headers().get('host')
  const icons = await getTenantIconsForHost(host)

  const iconUrl = icons?.faviconUrl
    ? withCacheBust(icons.faviconUrl, icons.updatedAt)
    : '/icon.svg'

  const siteName = icons?.siteName || 'Octus'
  const description = icons?.heroSubtitle || 'Sistema de gestão para lojas e varejo'

  return {
    title: { default: siteName, template: `%s — ${siteName}` },
    description,
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
      title: siteName,
    },
    openGraph: {
      title: siteName,
      description,
      siteName,
      locale: 'pt_BR',
      type: 'website',
    },
    twitter: {
      card: 'summary',
      title: siteName,
      description,
    },
  }
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR" suppressHydrationWarning>
      <head>
        {/* iOS Safari PWA meta tags */}
        <meta name="mobile-web-app-capable" content="yes" />
        <meta name="apple-touch-fullscreen" content="yes" />
        {/* apple-touch-icon já vem de generateMetadata (icons.apple) — link estático
            removido daqui pra não duplicar/entrar em conflito com o dinâmico. */}
        {/* Aplica o tema salvo antes do primeiro render para evitar flash */}
        <Script id="initial-theme" strategy="beforeInteractive">
          {`(function(){try{var t=localStorage.getItem('theme');if(t==='dark'){document.documentElement.classList.remove('light')}else{document.documentElement.classList.add('light');if(!t)localStorage.setItem('theme','light')}}catch(e){document.documentElement.classList.add('light')}})();`}
        </Script>
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
        <Script id="vlibras-loader" strategy="afterInteractive">
          {`
              window.VLibras = window.VLibras || {};
              var script = document.createElement('script');
              script.src = 'https://vlibras.gov.br/app/vlibras-plugin.js';
              script.onload = function() { new window.VLibras.Widget('https://vlibras.gov.br/app'); };
              document.body.appendChild(script);
            `}
        </Script>
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
