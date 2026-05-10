/** @type {import('next').NextConfig} */
const nextConfig = {
  // Necessário para o Dockerfile multi-stage copiar .next/standalone
  output: 'standalone',

  // Permite imagens de CDNs de TCG (Pokémon, Magic, etc.)
  images: {
    remotePatterns: [
      { protocol: 'https', hostname: 'images.pokemontcg.io' },
      { protocol: 'https', hostname: 'cards.scryfall.io' },
      { protocol: 'https', hostname: '**.apitcg.com' },
      { protocol: 'https', hostname: 'product-images.tcgplayer.com' },
    ],
  },
  // Proxy para a API em desenvolvimento (evita CORS)
  async rewrites() {
    return [
      {
        source: '/api/backend/:path*',
        destination: `${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'}/api/:path*`,
      },
    ]
  },
}

module.exports = nextConfig
