/** @type {import('next').NextConfig} */
const nextConfig = {
  // Permite que servidores locais paralelos usem caches separados. Sem isso,
  // um `next build` pode substituir chunks enquanto `next dev` ainda os serve.
  distDir: process.env.NEXT_DIST_DIR || '.next',

  // Necessário para o Dockerfile multi-stage copiar .next/standalone
  output: 'standalone',

  // Desligado só em dev: o StrictMode do React monta/desmonta cada componente
  // duas vezes de propósito pra achar side-effects mal limpos. Isso quebra o
  // hub do SignalR (lib/signalr.ts) — a desmontagem fantasma chama stopHub()
  // bem no meio do negotiate do hub real, e a conexão nunca sobe (fica preso
  // em "Desconectado" pra sempre). Zero efeito em produção: o StrictMode já
  // não faz nada fora de dev, isso só desativa o próprio double-invoke.
  reactStrictMode: false,

  // Cada tenant usa um subdomínio local (ex.: loja.localhost:3000). NÃO dá
  // pra declarar isso como 'allowedDevOrigins: [\'*.localhost\']' nessa versão
  // (14.2.35): o matcher de wildcard do Next (block-cross-site.js/
  // csrf-protection.js) trata QUALQUER segmento '*' como padrão inválido e
  // devolve bloqueio (403) em vez de liberar — bug/limitação confirmada lendo
  // o código-fonte do pacote, não erro de sintaxe. Pior: setar
  // allowedDevOrigins (mesmo "errado") muda o modo de "warn" pra "block" no
  // block-cross-site.js, transformando um aviso inofensivo em bloqueio real
  // dos chunks JS — foi isso que quebrou o teste local com subdomínio.
  // Sem essa chave, cai no modo "warn" (só loga no console, não bloqueia).
  // Reavaliar ao atualizar o Next: se uma versão futura corrigir o matcher de
  // wildcard, essa config volta a fazer sentido.

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
  async headers() {
    return [
      {
        source: '/:path*',
        headers: [
          { key: 'X-Content-Type-Options', value: 'nosniff' },
          { key: 'X-Frame-Options', value: 'DENY' },
          { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
          { key: 'Permissions-Policy', value: 'camera=(), microphone=(), geolocation=()' },
        ],
      },
      {
        source: '/robots.txt',
        headers: [
          { key: 'Content-Type', value: 'text/plain; charset=utf-8' },
          { key: 'Cache-Control', value: 'public, s-maxage=3600, stale-while-revalidate=86400' },
        ],
      },
      {
        source: '/sitemap.xml',
        headers: [
          { key: 'Content-Type', value: 'application/xml; charset=utf-8' },
          { key: 'Cache-Control', value: 'public, s-maxage=3600, stale-while-revalidate=86400' },
        ],
      },
      {
        source: '/manifest.webmanifest',
        headers: [
          { key: 'Content-Type', value: 'application/manifest+json; charset=utf-8' },
          { key: 'Cache-Control', value: 'public, s-maxage=300, stale-while-revalidate=3600' },
        ],
      },
      {
        source: '/:path(admin|plataforma|contador|cliente|login|entrar|cadastro|primeiro-acesso|reset-password|loja-nao-encontrada|loja-suspensa)/:rest*',
        headers: [{ key: 'X-Robots-Tag', value: 'noindex, nofollow, noarchive' }],
      },
    ]
  },
}

module.exports = nextConfig
