import { expect, test } from '@playwright/test'
import { NextRequest } from 'next/server'
import { middleware } from '../middleware'
import { isPublicTenantPage, tenantErrorResponse, tenantPageStatus } from '../lib/tenantPageGuard'

const root = process.env.NEXT_PUBLIC_ROOT_DOMAIN || '3esysten.com.br'
const shop = `loja.${root}`
const json = (body: unknown, status = 200) => async () => new Response(JSON.stringify(body), { status })

test('domínio principal, www e domínio próprio não consultam slug', async () => {
  for (const host of [null, root, `www.${root}`, 'outra-loja.example']) {
    expect(await tenantPageStatus(host, async () => { throw new Error('Não deve consultar') })).toBe(200)
  }
})

test('loja ativa passa; consulta interna não recebe cookies e não usa cache compartilhado', async () => {
  expect(await tenantPageStatus(shop.toUpperCase(), async (url, options) => {
    expect(String(url)).toContain('/api/public/site-icons?slug=loja')
    expect(options?.cache).toBe('no-store')
    expect(options?.redirect).toBe('error')
    expect(options?.headers).toBeUndefined()
    expect(options?.signal).toBeTruthy()
    return new Response(JSON.stringify({ siteName: 'Loja ativa' }))
  })).toBe(200)
})

test('somente 404 explícito de loja ausente/inativa vira página não encontrada', async () => {
  expect(await tenantPageStatus(shop, json({ errorCode: 'tenant_unavailable' }, 404))).toBe(404)
})

for (const status of [403, 404, 429, 500, 502, 503]) {
  test(`erro genérico ${status} preserva a disponibilidade da loja`, async () => {
    expect(await tenantPageStatus(shop, json({}, status))).toBe(200)
  })
}

test('timeout, JSON inválido e resposta incompleta não derrubam loja válida', async () => {
  expect(await tenantPageStatus(shop, async () => { throw new Error('timeout') })).toBe(200)
  expect(await tenantPageStatus(shop, async () => new Response('<html>proxy</html>'))).toBe(200)
  expect(await tenantPageStatus(shop, json({}))).toBe(200)
})

test('resultado de uma loja não vaza para outra nem fica em cache negativo', async () => {
  expect(await tenantPageStatus(shop, json({ errorCode: 'tenant_unavailable' }, 404))).toBe(404)
  expect(await tenantPageStatus(`outra.${root}`, json({ siteName: 'Outra' }))).toBe(200)
  expect(await tenantPageStatus(shop, json({ siteName: 'Reativada' }))).toBe(200)
})

test('abrange vitrine, login, recuperação, páginas legais e navegação RSC', () => {
  for (const path of ['/', '/login', '/reset-password', '/privacidade', '/produtos/123', '/entrar']) {
    expect(isPublicTenantPage(path)).toBe(true)
  }
  for (const path of ['/api', '/api/auth/login', '/_next/static/a.js', '/uploads/a.png', '/admin', '/admin/comanda', '/cliente', '/contador', '/plataforma', '/hubs/comanda', '/health', '/mcp', '/sw.js', '/robots.txt', '/sitemap.xml']) {
    expect(isPublicTenantPage(path)).toBe(false)
  }
})

test('404 tem status real, noindex e nenhum formulário ou script', async () => {
  const response = tenantErrorResponse(404)
  expect(response.status).toBe(404)
  expect(response.headers.get('x-robots-tag')).toContain('noindex')
  expect(response.headers.get('cache-control')).toContain('no-store')
  const html = await response.text()
  expect(html).toContain('Loja não encontrada')
  expect(html).not.toMatch(/<script|<form|<input/)
})

test('503 permite nova tentativa sem noindex e HEAD não envia HTML', async () => {
  const response = tenantErrorResponse(503)
  expect(response.status).toBe(503)
  expect(response.headers.get('retry-after')).toBe('60')
  expect(response.headers.has('x-robots-tag')).toBe(false)
  expect(await response.text()).toContain('Tentar novamente')
  expect(await tenantErrorResponse(404, true).text()).toBe('')
})

test('middleware só reescreve a raiz institucional, preservando demais páginas', async () => {
  const response = await middleware(new NextRequest(`https://${root}/`, { headers: { host: root } }))
  expect(response.headers.get('x-middleware-rewrite')).toBe(`https://${root}/institucional`)
  for (const path of ['/parceiros', '/login', '/privacidade']) {
    const next = await middleware(new NextRequest(`https://${root}${path}`, { headers: { host: root } }))
    expect(next.headers.get('x-middleware-rewrite')).toBeNull()
  }
})
