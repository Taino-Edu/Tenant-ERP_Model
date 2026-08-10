import { expect, test } from '@playwright/test'

test.describe('Atalhos globais inteligentes', () => {
  test.beforeEach(async ({ context, page }) => {
    const appUrl = process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3000'
    await context.addCookies([
      { name: 'userRole', value: 'Admin', url: appUrl },
      { name: 'userName', value: 'Teste', url: appUrl },
    ])

    await page.route('**/api/**', async route => {
      const path = new URL(route.request().url()).pathname.toLowerCase()
      if (path.includes('site-config')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            siteName: 'Loja Teste',
            heroSubtitle: 'Loja de teste',
            addressLine: 'Cidade — SP',
            contactPersonName: 'Atendimento',
            whatsappNumber: '',
            contactEmail: 'teste@example.com',
            logoUrl: null,
            faviconUrl: null,
            pwaIconUrl: null,
            adminIconUrl: null,
            navTorneiosLabel: '',
            navProdutosLabel: 'Produtos',
            navMercadoLabel: '',
            navPontosLabel: '',
            ctaVerEventosLabel: '',
            ctaVerTorneiosLabel: '',
            ctaVerProdutosLabel: 'Ver produtos',
            torneiosEyebrow: '',
            torneiosTitle: '',
            produtosEyebrow: 'Vitrine',
            produtosTitle: 'Em destaque',
            pontosEyebrow: '',
            pontosTitle: '',
            pontosParagraph: '',
            pontosFidelidadeAtivo: false,
            enabledModules: ['fiscal', 'estoque', 'restaurante', 'ia'],
            colorPrimary: '#3EC2F2',
            colorAccent: '#FFE45E',
            colorNavy: '#0C3D5A',
            colorBackground: '#EBF7FD',
            colorCard: '#FFFFFF',
          }),
        })
        return
      }
      if (path.includes('unread')) {
        await route.fulfill({ status: 200, contentType: 'application/json', body: '{"count":0}' })
        return
      }
      if (path.includes('/timers')) {
        await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
        return
      }
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' })
    })

    await page.goto('/admin/primeiros-passos')
    await expect(page.getByRole('heading', { name: 'Primeiros Passos' })).toBeVisible()
  })

  test('? abre a ajuda somente fora de campos de digitação', async ({ page }) => {
    await page.keyboard.press('?')
    await expect(page.getByRole('heading', { name: 'Atalhos de teclado' })).toBeVisible()
    await page.keyboard.press('Escape')

    await page.evaluate(() => {
      const input = document.createElement('input')
      input.id = 'shortcut-writing-test'
      document.body.appendChild(input)
      input.focus()
    })
    await page.keyboard.type('pergunta?')

    await expect(page.locator('#shortcut-writing-test')).toHaveValue('pergunta?')
    await expect(page.getByRole('heading', { name: 'Atalhos de teclado' })).not.toBeVisible()
  })

  test('o Assistente de IA pausa todos os atalhos globais', async ({ page }) => {
    await expect(page.getByTitle(/Assistente IA/)).toBeVisible()
    await page.keyboard.press('a')
    const chat = page.getByPlaceholder(/Pergunte algo/)
    await expect(chat).toBeVisible()

    const originalUrl = page.url()
    await chat.fill('Qual é o estoque? preciso conferir geral')

    await expect(chat).toHaveValue('Qual é o estoque? preciso conferir geral')
    await expect(page).toHaveURL(originalUrl)
    await expect(page.getByRole('heading', { name: 'Atalhos de teclado' })).not.toBeVisible()
  })
})
