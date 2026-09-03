const { spawn } = require('node:child_process')
const { resolve } = require('node:path')

const projectRoot = resolve(__dirname, '..')
const port = 3100
const origin = `http://127.0.0.1:${port}`
const server = spawn(process.execPath, ['scripts/start-standalone.js'], {
  cwd: projectRoot,
  env: { ...process.env, HOSTNAME: '127.0.0.1', PORT: String(port) },
  stdio: ['ignore', 'pipe', 'pipe'],
  windowsHide: true,
})

let serverOutput = ''
server.stdout.on('data', chunk => { serverOutput += chunk.toString() })
server.stderr.on('data', chunk => { serverOutput += chunk.toString() })

const delay = milliseconds => new Promise(resolvePromise => setTimeout(resolvePromise, milliseconds))

async function fetchWithTimeout(path, timeout = 10_000) {
  return fetch(`${origin}${path}`, { signal: AbortSignal.timeout(timeout) })
}

async function waitForServer() {
  for (let attempt = 1; attempt <= 60; attempt++) {
    if (server.exitCode !== null) {
      throw new Error(`Servidor standalone encerrou antes do smoke.\n${serverOutput}`)
    }
    try {
      const response = await fetchWithTimeout('/termos', 3_000)
      if (response.ok) return
    } catch {
      // O processo ainda pode estar inicializando.
    }
    await delay(500)
  }
  throw new Error(`Servidor standalone não ficou disponível em 30s.\n${serverOutput}`)
}

async function assertPage(path, expectedText) {
  const response = await fetchWithTimeout(path)
  const body = await response.text()
  if (!response.ok || !body.includes(expectedText)) {
    throw new Error(
      `${path} falhou: HTTP ${response.status}; conteúdo esperado: "${expectedText}"`,
    )
  }
  console.log(`✓ ${path} — HTTP ${response.status}`)
}

async function assertHeader(path, header, expectedPart) {
  const response = await fetchWithTimeout(path)
  const value = response.headers.get(header) || ''
  if (!response.ok || !value.toLowerCase().includes(expectedPart.toLowerCase())) {
    throw new Error(`${path} falhou: ${header}="${value}"; esperado conter "${expectedPart}"`)
  }
  console.log(`✓ ${path} — ${header}: ${value}`)
}

async function assertSecurityTxt() {
  const response = await fetchWithTimeout('/.well-known/security.txt')
  const body = await response.text()
  const requiredFields = [
    'Contact: mailto:3esysten@gmail.com',
    'Expires: 2027-03-01T00:00:00Z',
    'Canonical: https://3esysten.com.br/.well-known/security.txt',
  ]
  if (
    !response.ok
    || !response.headers.get('content-type')?.includes('text/plain')
    || requiredFields.some(field => !body.includes(field))
  ) {
    throw new Error(`security.txt inválido: HTTP ${response.status}; body=${JSON.stringify(body)}`)
  }
  console.log(`✓ /.well-known/security.txt — RFC 9116 básico válido`)
}

async function main() {
  try {
    await waitForServer()
    await assertPage('/termos', 'Termos de Uso')
    await assertPage('/privacidade', 'Política de Privacidade')
    await assertPage('/institucional', 'Comece no seu ritmo')
    await assertHeader('/robots.txt', 'content-type', 'text/plain')
    await assertHeader('/robots.txt', 'cache-control', 's-maxage=3600')
    await assertHeader('/sitemap.xml', 'content-type', 'application/xml')
    await assertHeader('/sitemap.xml', 'cache-control', 's-maxage=3600')
    await assertSecurityTxt()
  } finally {
    server.kill()
  }
}

main().catch(error => {
  console.error(error)
  process.exitCode = 1
})
