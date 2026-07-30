import { expect, test } from '@playwright/test'

test.describe('Smoke HTTP de produção', () => {
  test('página de termos responde e contém o conteúdo principal', async ({ request }) => {
    const response = await request.get('/termos', { timeout: 15_000 })

    expect(response.ok()).toBeTruthy()
    await expect(response.text()).resolves.toContain('Termos de Uso')
  })

  test('página de privacidade responde e contém o conteúdo principal', async ({ request }) => {
    const response = await request.get('/privacidade', { timeout: 15_000 })

    expect(response.ok()).toBeTruthy()
    await expect(response.text()).resolves.toContain('Política de Privacidade')
  })
})
