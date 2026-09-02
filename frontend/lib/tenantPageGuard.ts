import { extractSlug, INTERNAL_API_URL } from './serverSiteConfig'

// Somente páginas públicas. Não interceptar API, arquivos ou operação logada.
export function isPublicTenantPage(path: string): boolean {
  if (/\.[^/]+$/.test(path)) return false
  return !/^\/(?:api|_next|uploads|hubs|health|mcp|admin|plataforma|contador|cliente)(?:\/|$)/.test(path)
}

export async function tenantPageStatus(
  host: string | null,
  fetcher: typeof fetch = fetch,
): Promise<200 | 404 | 503> {
  const slug = extractSlug(host)
  // Domínio principal e domínios próprios não são resolvidos por slug.
  if (!slug) return 200
  try {
    const response = await fetcher(
      `${INTERNAL_API_URL}/api/public/site-icons?slug=${encodeURIComponent(slug.toLowerCase())}`,
      { cache: 'no-store', redirect: 'error', signal: AbortSignal.timeout(2000) },
    )
    const data = await response.json()
    // Só um erro de negócio explícito autoriza 404. Um proxy/endpoint ausente,
    // timeout ou 5xx nunca deve retirar uma loja válida do índice.
    if (response.status === 404 && data?.errorCode === 'tenant_unavailable') return 404
    if (response.ok && typeof data?.siteName === 'string' && data.siteName.trim()) return 200
  } catch {
    // Disponibilidade primeiro: a API pode estar saudável para o navegador,
    // mas indisponível pelo endereço interno do container. Sem uma confirmação
    // explícita de negócio, nunca derrubar uma loja válida com falso 503.
    return 200
  }
  // Status genérico, proxy HTML ou JSON incompleto também não provam que a
  // loja deixou de existir. Mantém o comportamento anterior até a API voltar.
  return 200
}

export function tenantErrorResponse(status: 404 | 503, head = false): Response {
  const missing = status === 404
  const title = missing ? 'Loja não encontrada' : 'Loja temporariamente indisponível'
  const message = missing
    ? 'Este endereço não corresponde a uma loja ativa. Confira o link ou fale com a loja.'
    : 'Não conseguimos consultar a loja agora. Aguarde um momento e tente novamente.'
  const headers: Record<string, string> = {
    'Content-Type': 'text/html; charset=utf-8',
    'Cache-Control': 'private, no-store',
    'X-Content-Type-Options': 'nosniff',
    'X-Frame-Options': 'DENY',
    'Referrer-Policy': 'no-referrer',
    'Content-Security-Policy': "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'",
  }
  if (missing) headers['X-Robots-Tag'] = 'noindex, nofollow, noarchive'
  else headers['Retry-After'] = '60'
  // Sem scripts, formulário ou interpolação de host/dados externos. Funciona
  // antes da hidratação e conserva o status real inclusive sem JavaScript.
  return new Response(head ? null : `<!doctype html><html lang="pt-BR"><head>
    <meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
    <title>${title} — Octus</title><style>
    :root{color-scheme:light dark}*{box-sizing:border-box}body{margin:0;min-height:100vh;display:grid;place-items:center;padding:24px;background:#10141d;color:#edf5fa;font:16px/1.6 system-ui,sans-serif}
    main{width:100%;max-width:480px;border:1px solid #304452;border-radius:24px;padding:32px}p{color:#bbcbd7}h1{font-size:26px;line-height:1.3}.brand{color:#3ec2f2;font-weight:700}a{color:#60d5fc;display:inline-block;padding:12px 0}
    @media(prefers-color-scheme:light){body{background:#ebf7fd;color:#0c3d5a}main{background:white;border-color:#c4dfe9}p{color:#475569}.brand,a{color:#066d89}}
    </style></head><body><main><div class="brand">Octus · 3E Systen</div><h1>${title}</h1><p>${message}</p>
    ${missing ? '' : '<a href="">Tentar novamente</a>'}</main></body></html>`, { status, headers })
}
