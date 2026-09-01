import { NextRequest, NextResponse } from 'next/server'
import { isPublicTenantPage, tenantErrorResponse, tenantPageStatus } from './lib/tenantPageGuard'

// Domínio raiz da plataforma (ex: "2esysten.com.br") — quando o visitante bate
// exatamente nele (ou em "www."), mostra a página institucional em vez da
// vitrine de loja. Qualquer subdomínio (loja.2esysten.com.br) continua caindo
// na vitrine normalmente, resolvida por tenant no backend.
const ROOT_DOMAIN = (process.env.NEXT_PUBLIC_ROOT_DOMAIN || '3esysten.com.br').toLowerCase()

export async function middleware(request: NextRequest) {
  const hostname = (request.headers.get('host') || '').split(':')[0].toLowerCase()
  const isRootDomain = hostname === ROOT_DOMAIN || hostname === `www.${ROOT_DOMAIN}`

  if (isRootDomain && request.nextUrl.pathname === '/') {
    const response = NextResponse.rewrite(new URL('/institucional', request.url))

    // A landing é conteúdo de marketing igual para todo visitante: nenhum dado
    // de sessão entra no HTML (a página é 'use client' e busca tudo depois da
    // hidratação). Mesmo assim o Next a renderiza sob demanda e emite
    // `no-store`, porque o layout raiz chama headers() para descobrir o Host e
    // montar favicon/título por loja — o que torna TODA rota do app dinâmica.
    //
    // O `no-store` obrigava cada visita a atravessar até a origem. Medido em
    // 20/08/2026: 840ms de primeiro byte, dos quais ~700ms eram só a viagem,
    // porque o Cloudflare serve este domínio de Miami/Newark enquanto a origem
    // está no Brasil. Um HIT no edge não faz essa viagem.
    //
    // `s-maxage` fala só com o proxy compartilhado (Cloudflare), não com o
    // navegador: uma alteração no site aparece para todo mundo em até 5 min, e
    // `stale-while-revalidate` deixa o edge servir a cópia velha enquanto busca
    // a nova, então nem a primeira visita depois do vencimento espera.
    //
    // Só o domínio raiz passa por aqui. Vitrine de loja continua intocada — lá
    // o `/` é outro conteúdo, por tenant, e cachear sem análise seria risco.
    response.headers.set(
      'Cache-Control',
      'public, s-maxage=300, stale-while-revalidate=86400',
    )
    return response
  }

  if ((request.method === 'GET' || request.method === 'HEAD') && isPublicTenantPage(request.nextUrl.pathname)) {
    const status = await tenantPageStatus(request.headers.get('host'))
    if (status !== 200) return tenantErrorResponse(status, request.method === 'HEAD')
  }
  return NextResponse.next()
}

export const config = {
  matcher: ['/((?!api/|_next/|uploads/|hubs/|health(?:/|$)|mcp(?:/|$)).*)'],
}
