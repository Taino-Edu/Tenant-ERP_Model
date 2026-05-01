import type { Metadata } from 'next'
import './globals.css'

export const metadata: Metadata = {
  title: { default: 'CardGameStore', template: '%s — CardGameStore' },
  description: 'Painel de gestão da loja de Card Games',
  icons: { icon: '/favicon.ico' },
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR" className="dark">
      <body>
        {/* Script VLibras — Acessibilidade */}
        <div vw="true" className="enabled">
          <div vw-access-button="true" className="active"></div>
          <div vw-plugin-wrapper="true">
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
      </body>
    </html>
  )
}
