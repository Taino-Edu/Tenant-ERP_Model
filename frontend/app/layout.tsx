import type { Metadata, Viewport } from 'next'
import { headers } from 'next/headers'
import Script from 'next/script'
import './globals.css'
import PWAInstallButton from '@/components/PWAInstallButton'
import CookieBanner from '@/components/CookieBanner'
import Footer from '@/components/Footer'
import VLibrasController from '@/components/VLibrasController'
import ClientProviders from '@/components/ClientProviders'
import MarketingTags from '@/components/MarketingTags'
import { getTenantIconsForHost, resolveShareImage, withCacheBust } from '@/lib/serverSiteConfig'

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
  const host = (await headers()).get('host')
  const icons = await getTenantIconsForHost(host)

  const iconUrl = icons?.faviconUrl
    ? withCacheBust(icons.faviconUrl, icons.updatedAt)
    : '/icon.svg'

  const siteName = icons?.siteName || 'Octus'
  const description = icons?.heroSubtitle || 'Sistema de gestão para lojas e varejo'
  const hostname = host?.split(':')[0] || '3esysten.com.br'
  const protocol = hostname === 'localhost' ? 'http' : 'https'
  const metadataBase = new URL(`${protocol}://${host || hostname}`)

  // Imagem de compartilhamento. Sem ela, o link da loja colado no WhatsApp, no
  // Instagram ou na bio do TikTok aparecia como uma linha de texto sem cartão —
  // e cartão sem imagem é o que separa um link clicado de um link ignorado.
  const shareImage = resolveShareImage(icons)

  return {
    metadataBase,
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
      images: [{ url: shareImage, alt: siteName }],
    },
    twitter: {
      card: 'summary_large_image',
      title: siteName,
      description,
      images: [shareImage],
    },
    manifest: '/manifest.webmanifest',
    formatDetection: { telephone: false },
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
        {/* Aplica o tema salvo antes do primeiro render.
            <script dangerouslySetInnerHTML> e NÃO <Script strategy="beforeInteractive">:
            no App Router, o next/script com conteúdo inline é serializado no
            payload do React Flight (`self.__next_f.push([1,{"children":"…"}])`),
            ou seja, viaja como DADO e nunca executa como script bloqueante. O
            efeito era o tema claro simplesmente não ser aplicado no load: dentro
            do /admin ele voltava só quando o ThemeToggle montava (daí o flash
            escuro), e nas telas sem ThemeToggle — login, entrar, cadastro,
            reset-password, primeiro-acesso — nunca era aplicado.
            Este é o mesmo padrão que app/admin/layout.tsx já usa para o FOUC da
            cor de marca, e que funciona. */}
        {/* Sem preferência salva, segue o tema do SISTEMA (prefers-color-scheme),
            como o Chrome e os produtos do Google fazem.
            A versão anterior assumia claro e — o problema de verdade — GRAVAVA
            'light' no localStorage. Isso apagava a diferença entre "nunca
            escolhi" e "escolhi claro": depois da primeira visita não havia mais
            como voltar a seguir o sistema, e quem perdesse o localStorage
            (janela anônima, outro perfil, limpeza de dados, ou a troca de origem
            2esysten→3esysten, que são dois storages distintos) via a escolha
            trocar sozinha.
            Agora só escreve quando alguém clica no ThemeToggle — que é o único
            momento em que existe escolha de fato. A leitura aqui e a do
            ThemeToggle têm que continuar idênticas: se divergirem, a tela pisca
            de um tema pro outro na hidratação.
            O listener de `change` fica AQUI, e não só no ThemeToggle, porque
            login/entrar/cadastro/reset-password não têm toggle nenhum: com o
            listener só lá, o sistema virava pro escuro e essas telas seguiam
            claras até alguém recarregar. Este script roda em toda página. */}
        <script dangerouslySetInnerHTML={{ __html: `(function(){try{var q=window.matchMedia('(prefers-color-scheme: dark)');function ap(){var t=localStorage.getItem('theme');document.documentElement.classList.toggle('light',t?t==='light':!q.matches)}ap();q.addEventListener('change',ap)}catch(e){document.documentElement.classList.add('light')}})();` }} />
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
        {/* GTM/Meta só são carregados depois da escolha correspondente. */}
        <MarketingTags />
        {/* Botão flutuante de instalação PWA — aparece quando o Chrome suporta */}
        <PWAInstallButton />
        </ClientProviders>
      </body>
    </html>
  )
}
