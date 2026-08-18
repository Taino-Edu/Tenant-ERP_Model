import { expect, test, type Page } from '@playwright/test'

// O atalho de WhatsApp da vitrine é um elemento fixo: se ele voltar a ficar
// sempre aberto, ou parar de sumir com modal e rodapé, o defeito é "tapa a
// informação" — visível para o visitante e invisível para quem só lê o diff.

const WHATSAPP = '5517999998888'

const siteConfig = {
  siteName: 'Loja Teste',
  heroSubtitle: 'Loja de teste',
  addressLine: 'Cidade — SP',
  contactPersonName: 'Marina',
  whatsappNumber: WHATSAPP,
  contactEmail: 'teste@example.com',
  logoUrl: null, faviconUrl: null, pwaIconUrl: null, adminIconUrl: null,
  navTorneiosLabel: '', navProdutosLabel: 'Produtos', navMercadoLabel: '', navPontosLabel: '',
  ctaVerEventosLabel: '', ctaVerTorneiosLabel: '', ctaVerProdutosLabel: 'Ver produtos',
  torneiosEyebrow: '', torneiosTitle: '', produtosEyebrow: 'Vitrine', produtosTitle: 'Em destaque',
  pontosEyebrow: '', pontosTitle: '', pontosParagraph: '', pontosFidelidadeAtivo: false,
  enabledModules: ['fiscal'],
  colorPrimary: '#3EC2F2', colorAccent: '#FFE45E', colorNavy: '#0C3D5A',
  colorBackground: '#EBF7FD', colorCard: '#FFFFFF',
}

async function mockStorefront(page: Page, overrides: Partial<typeof siteConfig> = {}) {
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname.toLowerCase()
    if (path.includes('site-config')) {
      await route.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify({ ...siteConfig, ...overrides }),
      })
      return
    }
    await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
  })
}

const fab = (page: Page) => page.locator('.js-wa-fab')
const fabLink = (page: Page) => fab(page).getByRole('link')

test.describe('Atalho de WhatsApp da vitrine', () => {
  test('abre a conversa já identificando de onde o visitante veio', async ({ page }) => {
    await mockStorefront(page)
    await page.goto('/')

    const href = await fabLink(page).getAttribute('href')
    expect(href).toContain(`https://wa.me/${WHATSAPP}`)
    // Sem a mensagem pronta o atendimento recebe uma conversa em branco.
    expect(decodeURIComponent(href ?? '')).toContain('Vim pelo site da Loja Teste')
    await expect(fabLink(page)).toHaveAttribute('aria-label', /Marina.*WhatsApp/)
  })

  test('a dica explica o botão uma vez por sessão e pode ser fechada', async ({ page }) => {
    await mockStorefront(page)
    await page.goto('/')

    const dica = fab(page).getByText('Precisa de ajuda para escolher?')
    await expect(dica).toBeVisible({ timeout: 6000 })
    await expect(fab(page)).toContainText('Marina responde pelo WhatsApp')

    await fab(page).getByRole('button', { name: 'Fechar aviso' }).click()
    await expect(dica).toHaveCount(0)

    // Recarregar mantém o sessionStorage: a dica não insiste a cada página.
    await page.reload()
    await page.waitForTimeout(4000)
    await expect(fab(page).getByText('Precisa de ajuda para escolher?')).toHaveCount(0)
  })

  test('some quando o rodapé aparece, porque ele já traz o mesmo número', async ({ page }) => {
    await mockStorefront(page)
    await page.goto('/')
    await expect(fabLink(page)).toBeVisible()

    // `.first()`: a vitrine tem o rodapé próprio (com o número) e, abaixo dele,
    // o rodapé global de links legais do RootLayout. O observado é o primeiro —
    // e como o global vem depois, o botão segue escondido até o fim da página.
    await page.locator('footer').first().scrollIntoViewIfNeeded()
    await expect(fab(page)).toHaveClass(/opacity-0/)
    await expect(fab(page)).toHaveClass(/pointer-events-none/)
  })

  test('não renderiza quando a loja não configurou WhatsApp', async ({ page }) => {
    await mockStorefront(page, { whatsappNumber: '' })
    await page.goto('/')
    await expect(page.getByRole('heading').first()).toBeVisible()
    await expect(fab(page)).toHaveCount(0)
  })
})
