import { expect, test } from '@playwright/test'
import { calculateProfitability } from '../components/admin/financeiro/profitability'
import { DEFAULT_SITE_CONFIG } from '../contexts/SiteConfigContext'

const product = {
  nome: 'Produto teste', categoria: 'Teste', qtd: 2, qtdComandas: 2, qtdAvulsa: 0,
  receita: 40, receitaComandas: 40, receitaAvulsa: 0, custo: 16, margem: 24,
}

test.describe('calculos de rentabilidade', () => {
  test('distingue margem de markup e calcula o preco da meta', () => {
    const result = calculateProfitability(product, 40)

    expect(result.averagePrice).toBe(20)
    expect(result.averageCost).toBe(8)
    expect(result.grossMarginPercent).toBeCloseTo(60)
    expect(result.markupPercent).toBeCloseTo(150)
    expect(result.suggestedPrice).toBeCloseTo(13.3333)
  })

  test('nao inventa rentabilidade quando o custo esta ausente', () => {
    const result = calculateProfitability({ ...product, custo: 0, margem: 40 }, 40)

    expect(result.hasCost).toBe(false)
    expect(result.grossMarginPercent).toBeNull()
    expect(result.markupPercent).toBeNull()
    expect(result.suggestedPrice).toBeNull()
  })
})

test('a subpagina explica os indicadores pelo botao de ajuda', async ({ page }) => {
  await page.setViewportSize({ width: 393, height: 852 })
  await page.context().addCookies([
    { name: 'userRole', value: 'Admin', url: 'http://localhost' },
    { name: 'userName', value: 'Teste', url: 'http://localhost' },
    { name: 'userId', value: '00000000-0000-0000-0000-000000000001', url: 'http://localhost' },
  ])
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname
    if (path.endsWith('/site-config')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify(DEFAULT_SITE_CONFIG) })
    if (path.endsWith('/notifications/unread-count')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ count: 0 }) })
    if (path.endsWith('/timers')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify([]) })
    if (!path.endsWith('/analytics/financeiro')) return route.fulfill({ contentType: 'application/json', body: JSON.stringify({}) })
    return route.fulfill({ contentType: 'application/json', body: JSON.stringify({
      receitaBruta: 40, deducoes: 0, receita: 40, impostosSobreVendas: 0,
      receitaLiquidaDre: 40, receitaComandas: 40, receitaAvulsa: 0, custo: 16,
      margem: 24, margemPercent: 60, despesasOperacionais: 0,
      resultadoOperacional: 24, resultadoFinanceiro: 0, impostosSobreLucro: 0,
      resultadoLiquido: 24, lancamentosNaoClassificados: 0, despesasPorCategoria: [],
      crediarios: 0, recebidoCrediario: 0, diaDia: [], pagamentosPorForma: [],
      pagamentosCrediarioPeriodo: [], topProdutos: [product],
    }) })
  })

  await page.goto('/admin/financeiro/rentabilidade')
  const cookieButton = page.getByRole('button', { name: 'Recusar opcionais' })
  await cookieButton.waitFor({ state: 'visible', timeout: 2_000 }).then(() => cookieButton.click()).catch(() => {})

  await expect(page.getByRole('heading', { name: 'Preço e Rentabilidade' })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true)
  await page.getByRole('button', { name: 'Entenda: Margem bruta', exact: true }).click()
  await expect(page.getByRole('dialog', { name: 'Margem bruta' })).toContainText('(receita - CMV) / receita x 100')
})
