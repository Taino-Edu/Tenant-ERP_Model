import type { NextConfig } from 'next'

const nextConfig: NextConfig = {
  output: 'standalone',

  images: {
    remotePatterns: [
      { protocol: 'https', hostname: 'images.pokemontcg.io' },
      { protocol: 'https', hostname: 'cards.scryfall.io' },
      { protocol: 'https', hostname: '**.apitcg.com' },
      { protocol: 'https', hostname: 'product-images.tcgplayer.com' },
    ],
  },

  // Em produção o Next.js proxy as chamadas REST para o container da API
  // via rede Docker interna (evita expor IP/porta da API ao browser).
  // Para SignalR/WebSocket o browser conecta diretamente via NEXT_PUBLIC_API_URL.
  async rewrites() {
    return [
      {
        source: '/api/backend/:path*',
        destination: `${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'}/api/:path*`,
      },
    ]
  },
}

export default nextConfig
