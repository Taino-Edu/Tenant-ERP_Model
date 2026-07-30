/** @type {import('next').NextConfig} */
const nextConfig = {
  // Necessário para o Dockerfile multi-stage copiar .next/standalone
  output: 'standalone',

  // Desligado só em dev: o StrictMode do React monta/desmonta cada componente
  // duas vezes de propósito pra achar side-effects mal limpos. Isso quebra o
  // hub do SignalR (lib/signalr.ts) — a desmontagem fantasma chama stopHub()
  // bem no meio do negotiate do hub real, e a conexão nunca sobe (fica preso
  // em "Desconectado" pra sempre). Zero efeito em produção: o StrictMode já
  // não faz nada fora de dev, isso só desativa o próprio double-invoke.
  reactStrictMode: false,

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
