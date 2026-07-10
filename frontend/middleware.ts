import { NextRequest, NextResponse } from 'next/server'

// Domínio raiz da plataforma (ex: "2esysten.com.br") — quando o visitante bate
// exatamente nele (ou em "www."), mostra a página institucional em vez da
// vitrine de loja. Qualquer subdomínio (loja.2esysten.com.br) continua caindo
// na vitrine normalmente, resolvida por tenant no backend.
const ROOT_DOMAIN = process.env.NEXT_PUBLIC_ROOT_DOMAIN || ''

export function middleware(request: NextRequest) {
  if (!ROOT_DOMAIN) return NextResponse.next()

  const hostname = (request.headers.get('host') || '').split(':')[0].toLowerCase()
  const isRootDomain = hostname === ROOT_DOMAIN || hostname === `www.${ROOT_DOMAIN}`

  if (isRootDomain) {
    return NextResponse.rewrite(new URL('/institucional', request.url))
  }

  return NextResponse.next()
}

export const config = {
  matcher: '/',
}
