import { expect, test } from '@playwright/test'
import { calculateContribution } from '../components/admin/financeiro/contribution'
import { DEFAULT_SITE_CONFIG } from '../contexts/SiteConfigContext'
import type { FinanceiroDto } from '../lib/api'

const base = {
  revenue: 10_000,
  cmv: 4_000,
  salesTax: 1_000,
  fixedExpenses: 3_000,
  cardFeePercent: 3,
  commissionPercent: 2,
  freightPercent: 0,
  discountPercent: 0,
}

test('calcula contribuição, equilíbrio e margem de segurança', () => {
  const result = calculateContribution(base)

  expect(result.knownTaxPercent).toBe(10)
  expect(result.totalVariablePercent).toBe(15)
  expect(result.contributionMargin).toBe(4_500)
  expect(result.contributionMarginPercent).toBeCloseTo(45)
  expect(result.breakEvenRevenue).toBeCloseTo(6_666.67, 2)
  expect(result.safetyMargin).toBeCloseTo(3_333.33, 2)
  expect(result.projectedOperatingResult).toBe(1_500)
})

test('mostra quando o desconto derruba o resultado abaixo do equilíbrio', () => {
  const result = calculateContribution({ ...base, discountPercent: 20 })

  expect(result.discountedRevenue).toBe(8_000)
  expect(result.variableExpenses).toBe(1_200)
  expect(result.contributionMargin).toBe(2_800)
  expect(result.projectedOperatingResult).toBe(-200)
  expect(result.maxDiscountContributionPercent).toBeCloseTo(52.94, 2)
  expect(result.maxDiscountBreakEvenPercent).toBeCloseTo(17.65, 2)
})

test('não inventa ponto de equilíbrio sem receita ou contribuição positiva', () => {
  const noRevenue = calculateContribution({ ...base, revenue: 0 })
  const noContribution = calculateContribution({ ...base, cmv: 9_000 })

  expect(noRevenue.contributionMarginPercent).toBeNull()
  expect(noRevenue.breakEvenRevenue).toBeNull()
  expect(noContribution.contributionMargin).toBeLessThan(0)
  expect(noContribution.breakEvenRevenue).toBeNull()
})

test('o simulador alerta quando o desconto cruza o equilíbrio no mobile', async ({ page }) => {
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
    if (path.endsWith('/financial-config')) return route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ cardFeePercent: 0, commissionPercent: 0, freightPercent: 0, expectedDailyNetCash: 0, minimumCashReserve: 0, updatedAt: new Date(0).toISOString() }),
    })
    if (path.endsWith('/analytics/financeiro')) return route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        receita: 10_000,
        custo: 4_000,
        impostosSobreVendas: 1_000,
        despesasOperacionais: 3_000,
        lancamentosNaoClassificados: 0,
      } as FinanceiroDto),
    })
    return route.fulfill({ contentType: 'application/json', body: JSON.stringify({}) })
  })

  await page.goto('/admin/financeiro/ponto-de-equilibrio')
  await page.waitForTimeout(500)
  const cookieButton = page.getByRole('button', { name: 'Recusar opcionais' })
  await cookieButton.waitFor({ state: 'visible', timeout: 2_000 }).then(() => cookieButton.click()).catch(() => {})

  await expect(page.getByRole('heading', { name: 'Margem e Ponto de Equilíbrio' })).toBeVisible()
  await expect(page.getByText('50.0%', { exact: true })).toBeVisible()
  await page.getByLabel('Taxas de cartão').fill('3')
  await page.getByLabel('Comissões').fill('2')
  await page.getByLabel('Desconto simulado').fill('20')
  await expect(page.getByText('Com as premissas atuais, este desconto leva o período abaixo do ponto de equilíbrio.')).toBeVisible()
  await expect(page.getByText('R$ -200,00', { exact: true }).first()).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true)
})
