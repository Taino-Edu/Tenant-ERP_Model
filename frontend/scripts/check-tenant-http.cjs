// Integração HTTP local: build real do Next + API simulada, sem acessar produção.
const http = require('node:http')
const { spawn } = require('node:child_process')
const assert = require('node:assert/strict')
const path = require('node:path')
const listen = server => new Promise(resolve => server.listen(0, '127.0.0.1', resolve))
const delay = ms => new Promise(resolve => setTimeout(resolve, ms))
// http.request conserva o Host de teste; fetch/undici pode substituí-lo pela
// URL local, fazendo todos os cenários parecerem o domínio 127.0.0.1.
const localRequest = (url, options = {}) => new Promise((resolve, reject) => {
  const request = http.request(url, options, response => {
    const chunks = []
    response.on('data', chunk => chunks.push(chunk))
    response.on('end', () => resolve(new Response(
      options.method === 'HEAD' ? null : Buffer.concat(chunks),
      { status: response.statusCode, headers: response.headers },
    )))
  })
  request.setTimeout(15000, () => request.destroy(new Error('Timeout local')))
  request.on('error', reject)
  request.end()
})

async function main() {
  let previewBase
  const root = process.env.NEXT_PUBLIC_ROOT_DOMAIN || '3esysten.com.br'
  const api = http.createServer(async (req, res) => {
    const url = new URL(req.url, 'http://localhost')
    if (process.argv.includes('--serve') && url.pathname.startsWith('/preview/') && previewBase) {
      const host = url.pathname.endsWith('/missing') ? `missing.${root}` : `outage.${root}`
      const page = await localRequest(previewBase, { headers: { host } })
      res.writeHead(page.status, Object.fromEntries(page.headers))
      res.end(await page.text())
      return
    }
    const slug = url.searchParams.get('slug')
    res.setHeader('content-type', 'application/json')
    if (url.pathname === '/api/public/site-icons') {
      if (slug === 'missing') {
        res.writeHead(404).end(JSON.stringify({ errorCode: 'tenant_unavailable' }))
      } else if (slug === 'outage') {
        res.writeHead(503).end('{}')
      } else {
        res.end(JSON.stringify({ siteName: 'Loja de teste ativa' }))
      }
    } else res.end('[]')
  })
  await listen(api)
  const reservation = http.createServer()
  await listen(reservation)
  const port = reservation.address().port
  await new Promise(resolve => reservation.close(resolve))
  const child = spawn(process.execPath, [path.join(__dirname, 'start-standalone.js')], {
    cwd: path.join(__dirname, '..'), windowsHide: true,
    env: { ...process.env, PORT: String(port), HOSTNAME: '127.0.0.1',
      INTERNAL_API_URL: `http://127.0.0.1:${api.address().port}` },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  let logs = ''
  child.stdout.on('data', chunk => { logs += chunk })
  child.stderr.on('data', chunk => { logs += chunk })
  const base = `http://127.0.0.1:${port}`
  previewBase = base
  const request = (host, route, method = 'GET', extra = {}) => localRequest(base + route, {
    method, headers: { host, ...extra },
  })
  try {
    let ready = false
    for (let attempt = 0; attempt < 40; attempt++) {
      try { await request(root, '/robots.txt'); ready = true; break } catch { await delay(500) }
    }
    assert.ok(ready, logs)
    for (const route of ['/', '/login', '/reset-password?from=admin', '/privacidade', '/produtos']) {
      const res = await request(`missing.${root}`, route)
      assert.equal(res.status, 404, route)
      assert.match(res.headers.get('x-robots-tag'), /noindex/)
      assert.match(res.headers.get('cache-control'), /no-store/)
      const body = await res.text()
      assert.match(body, /Loja não encontrada/)
      assert.doesNotMatch(body, /<form|<input|<script/)
    }
    const rsc = await request(`missing.${root}`, '/login?_rsc=test', 'GET', { RSC: '1' })
    assert.equal(rsc.status, 404)
    const head = await request(`missing.${root}`, '/', 'HEAD')
    assert.equal(head.status, 404)
    assert.equal(await head.text(), '')
    const outage = await request(`outage.${root}`, '/')
    assert.equal(outage.status, 503)
    assert.equal(outage.headers.get('retry-after'), '60')
    assert.equal(outage.headers.get('x-robots-tag'), null)
    const active = await request(`active.${root}`, '/login')
    assert.equal(active.status, 200)
    assert.match(await active.text(), /Entrar no Painel/)
    assert.equal((await request(root, '/login')).status, 200)
    console.log('PASS: 10 verificações HTTP (404, RSC, HEAD, 503, loja ativa e domínio principal).')
    if (process.argv.includes('--serve')) {
      console.log(`Prévia local: http://127.0.0.1:${api.address().port}/preview/missing`)
      await new Promise(resolve => {
        process.once('SIGINT', resolve)
        process.once('SIGTERM', resolve)
      })
    }
  } finally {
    child.kill()
    api.close()
  }
}
main().catch(error => { console.error(error); process.exitCode = 1 })
