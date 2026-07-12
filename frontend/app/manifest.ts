import type { MetadataRoute } from 'next'
import { headers } from 'next/headers'
import { getTenantIconsForHost, withCacheBust } from '@/lib/serverSiteConfig'

// Arquivo especial do Next.js (App Router) — auto-descoberto e servido em
// /manifest.webmanifest, substitui a referência estática que existia antes em
// metadata.manifest ('/manifest.json', ver layout.tsx). O uso de headers()
// aqui embaixo já é o suficiente pro Next.js tratar essa rota como dinâmica
// (por requisição), mesmo sendo um arquivo de metadata especial.
//
// Fallback: sem ícone de PWA configurado pro tenant (todo tenant hoje), o
// conteúdo devolvido é idêntico ao antigo public/manifest.json (mantido no
// lugar, só não é mais referenciado — nada quebra se algo ainda apontar pra
// ele direto por path).
export default async function manifest(): Promise<MetadataRoute.Manifest> {
  const host = headers().get('host')
  const tenantIcons = await getTenantIconsForHost(host)

  const iconUrl = tenantIcons?.pwaIconUrl
    ? withCacheBust(tenantIcons.pwaIconUrl, tenantIcons.updatedAt)
    : null

  const name = tenantIcons?.siteName || 'Minha Loja'

  return {
    name,
    short_name: name,
    description: 'Sistema de gestão para lojas e varejo',
    start_url: '/',
    display: 'standalone',
    background_color: '#121215',
    theme_color: '#42B6EE',
    orientation: 'portrait-primary',
    icons: iconUrl
      ? [
          { src: iconUrl, sizes: 'any', purpose: 'any' },
          { src: iconUrl, sizes: 'any', purpose: 'maskable' },
        ]
      : [
          { src: '/icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
          { src: '/icon-maskable.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'maskable' },
        ],
    shortcuts: [
      {
        name: 'Frente de Caixa',
        short_name: 'Caixa',
        url: '/admin/venda-avulsa',
        icons: [{ src: '/icon.svg', sizes: 'any' }],
      },
      {
        name: 'Dashboard',
        short_name: 'Dashboard',
        url: '/admin/dashboard',
        icons: [{ src: '/icon.svg', sizes: 'any' }],
      },
    ],
    categories: ['business', 'productivity'],
    lang: 'pt-BR',
  }
}
