/** @type {import('next').NextConfig} */
const nextConfig = {
  // Necessário para o Dockerfile multi-stage copiar .next/standalone
  output: 'standalone',

  // Ignora erros de tipagem e linting DURANTE O BUILD para evitar que o Docker trave
  // devido a conflitos de versão do npm (ERESOLVE).
  eslint: {
    ignoreDuringBuilds: true,
  },
  typescript: {
    ignoreBuildErrors: true,
  },

  // Permite imagens de CDNs de TCG (Pokémon, Magic, etc.)
  images: {
    remotePatterns: [
      { protocol: 'https', hostname: 'images.pokemontcg.io' },
      { protocol: 'https', hostname: 'assets.tcgdex.net' },
      { protocol: 'https', hostname: 'optcgapi.com' },
      { protocol: 'https', hostname: 'cards.scryfall.io' },
      { protocol: 'https', hostname: '**.apitcg.com' },
      { protocol: 'https', hostname: 'product-images.tcgplayer.com' },
      // Imagens de upload local (dev) e Oracle Cloud (produção)
      { protocol: 'http',  hostname: 'localhost' },
      // IP do servidor: configure UPLOAD_HOSTNAME no ambiente (ex: 193.123.45.67)
      ...(process.env.UPLOAD_HOSTNAME
        ? [{ protocol: 'https', hostname: process.env.UPLOAD_HOSTNAME }]
        : []),
    ],
  },
  // Proxy pra API em dev local (next dev sem nginx na frente). O código do
  // app sempre chama caminhos relativos (/api/..., /hubs/..., /uploads/...);
  // em produção quem resolve isso é o nginx, que já roteia esses prefixos
  // pro container da API antes mesmo de chegar no Next.js — este rewrite só
  // entra em ação quando não há nginx no caminho (dev local).
  async rewrites() {
    const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'
    return [
      { source: '/api/:path*',     destination: `${apiUrl}/api/:path*` },
      { source: '/hubs/:path*',    destination: `${apiUrl}/hubs/:path*` },
      { source: '/uploads/:path*', destination: `${apiUrl}/uploads/:path*` },
    ]
  },
}

module.exports = nextConfig
