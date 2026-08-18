import { expect, test, type Page } from '@playwright/test'

// Classe do Tailwind que não é gerada não quebra nada que se possa detectar:
// o TypeScript não conhece Tailwind, o ESLint não olha className, o build passa
// e os testes de conteúdo continuam verdes — o elemento simplesmente aparece
// sem cor. Foi o que aconteceu quando o tema do site público saiu de dentro de
// app/institucional/page.tsx para lib/institucional.ts, um diretório que não
// estava na lista `content` do tailwind.config: as seções perderam o fundo e o
// texto de apoio perdeu a cor, e o site foi para produção assim.
//
// Estes testes olham COR COMPUTADA. É a única camada capaz de perceber a
// diferença entre "a classe está no HTML" e "a classe existe no CSS".

const TRANSPARENTE = 'rgba(0, 0, 0, 0)'

async function abrir(page: Page, tema: 'claro' | 'escuro' = 'claro') {
  await page.route('**/api/**', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }))
  await page.addInitScript(preferido => {
    localStorage.setItem('institucional-theme', preferido)
  }, tema === 'escuro' ? 'dark' : 'light')
  await page.goto('/institucional')
  await page.waitForLoadState('networkidle')
}

const corDe = (page: Page, seletor: string, prop: 'color' | 'backgroundColor') =>
  page.locator(seletor).first().evaluate(
    (el, p) => getComputedStyle(el)[p as 'color'], prop)

test.describe('Tema do site público', () => {
  test('as seções têm fundo próprio, não o branco herdado do body', async ({ page }) => {
    await abrir(page)
    // `theme.soft` — a faixa alternada que separa uma seção da seguinte.
    expect(await corDe(page, '#planos', 'backgroundColor')).not.toBe(TRANSPARENTE)
    expect(await corDe(page, '#contador', 'backgroundColor')).not.toBe(TRANSPARENTE)
  })

  test('o texto de apoio tem a cor do tema, não a cor herdada do título', async ({ page }) => {
    await abrir(page)
    const titulo = await corDe(page, '#recursos h2', 'color')
    const corpo = await corDe(page, '#recursos article p', 'color')
    expect(corpo).not.toBe(TRANSPARENTE)
    // `theme.body` existe justamente para não ser igual a `theme.heading`.
    expect(corpo).not.toBe(titulo)
  })

  test('o tema escuro pinta a página e os cards', async ({ page }) => {
    await abrir(page, 'escuro')
    await expect(page.locator('body')).toHaveClass(/institucional-dark/)

    const pagina = await corDe(page, 'main', 'backgroundColor')
    expect(pagina).not.toBe(TRANSPARENTE)

    // `theme.card` no escuro é um azul-marinho mais claro que o fundo: se a
    // classe não for gerada, o card some dentro da página.
    const card = await corDe(page, '#recursos article', 'backgroundColor')
    expect(card).not.toBe(TRANSPARENTE)
    expect(card).not.toBe(pagina)
  })

  test('o acento acompanha o tema em vez de ficar fixo', async ({ page }) => {
    await abrir(page)
    const claro = await corDe(page, '.octus-accent', 'color')
    await page.getByRole('button', { name: 'Ativar tema escuro' }).click()
    await page.waitForTimeout(400)
    const escuro = await corDe(page, '.octus-accent', 'color')
    expect(claro).not.toBe(escuro)
  })

  test('a tabela comparativa não sobra linha no rodapé', async ({ page }) => {
    await abrir(page)
    const ultima = page.locator('table tbody tr').last().locator('th, td')
    for (const celula of await ultima.all()) {
      expect(await celula.evaluate(el => getComputedStyle(el).borderBottomWidth)).toBe('0px')
    }
  })
})
