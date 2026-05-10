import type { Metadata, Viewport } from 'next'
import './globals.css'
import PWAInstallButton from '@/components/PWAInstallButton'

export const viewport: Viewport = {
  themeColor: '#6C3FC5',
  width: 'device-width',
  initialScale: 1,
}

export const metadata: Metadata = {
  title: { default: 'softNerd', template: '%s — softNerd' },
  description: 'Sistema de gestão da loja de Card Games',
  manifest: '/manifest.json',
  icons: {
    icon: '/icon.svg',
    apple: '/icon.svg',
  },
  appleWebApp: {
    capable: true,
    statusBarStyle: 'black-translucent',
    title: 'softNerd',
  },
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR">
      <head>
        {/* iOS Safari PWA meta tags */}
        <meta name="mobile-web-app-capable" content="yes" />
        <meta name="apple-touch-fullscreen" content="yes" />
        <link rel="apple-touch-icon" href="/icon.svg" />
        {/* Aplica o tema salvo antes do primeiro render para evitar flash */}
        <script
          dangerouslySetInnerHTML={{
            __html: `(function(){try{if(localStorage.getItem('theme')==='light')document.documentElement.classList.add('light')}catch(e){}})();`
          }}
        />
      </head>
      <body>
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
        {children}
        {/* Botão flutuante de instalação PWA — aparece quando o Chrome suporta */}
        <PWAInstallButton />
      </body>
    </html>
  )
}
