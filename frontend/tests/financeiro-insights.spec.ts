import { expect, test } from '@playwright/test'
import { buildFinancialInsights, previousPeriod } from '../components/admin/financeiro/financial-insights'
import { DEFAULT_SITE_CONFIG } from '../contexts/SiteConfigContext'
import type { CapitalGiroDto, EstoqueInteligenteDto, FinanceiroDto } from '../lib/api'

const financeiro = (overrides: Partial<FinanceiroDto> = {}) => ({
  receita: 10_000,
  margem: 4_000,
  margemPercent: 40,
  resultadoLiquido: 1_000,
  lancamentosNaoClassificados: 0,
  ...overrides,
} as FinanceiroDto)

const capital = (overrides: Partial<CapitalGiroDto> = {}) => ({
  vencidoPagar: 0,
  vencidoReceber: 0,
  vencePagar7Dias: 0,
  comprasEstoquePeriodo: 1_000,
  ...overrides,
} as CapitalGiroDto)

const estoque = (overrides: Partial<EstoqueInteligenteDto> = {}) => ({
  produtosRiscoRuptura: 0,
  produtosSemCusto: 0,
  produtos: [],
  ...overrides,
} as EstoqueInteligenteDto)

test('prioriza riscos urgentes antes das oportunidades e informa o impacto', () => {
  const result = buildFinancialInsights({
    current: financeiro({ resultadoLiquido: -800 }),
    previous: financeiro(),
    capital: capital({ vencidoPagar: 500, vencidoReceber: 300 }),
    stock: estoque({
      produtosRiscoRuptura: 2,
      produtos: [{ situacao: 'excesso', valorEstoque: 2_000 }] as EstoqueInteligenteDto['produtos'],
    }),
  })

  expect(result.urgentCount).toBe(4)
  expect(result.lowMovementCapital).toBe(2_000)
  expect(result.insights.slice(0, 4).every(item => item.severity === 'urgent')).toBe(true)
  expect(result.insights.find(item => item.id === 'low-movement')?.severity).toBe('opportunity')
})

test('compara períodos equivalentes sem inventar percentual sobre base zero', () => {
  expect(previousPeriod('2026-08-01', '2026-08-24')).toEqual({ inicio: '2026-07-08', fim: '2026-07-31' })

  const result = buildFinancialInsights({
    current: financeiro({ receita: 2_000 }),
    previous: financeiro({ receita: 0 }),
    capital: capital(),
    stock: estoque(),
  })

  expect(result.revenueVariation).toBeNull()
  expect(result.insights.some(item => item.id === 'revenue-up' || item.id === 'revenue-down')).toBe(false)
})

test('expõe a razão de cada recomendação para a ajuda contextual', () => {
  const result = buildFinancialInsights({
    current: financeiro({ margemPercent: 15, lancamentosNaoClassificados: 250 }),
    previous: financeiro(),
    capital: capital({ vencePagar7Dias: 900 }),
    stock: estoque({ produtosSemCusto: 1 }),
  })

  expect(result.attentionCount).toBe(4)
  expect(result.insights.filter(item => item.severity === 'attention').every(item => item.rationale.length > 20)).toBe(true)
})

test('a central exibe prioridades, comparação e explicação no mobile', async ({ page }) => {
  await page.setViewportSize({ width: 393, height: 852 })
  await page.context().addCookies([
    { name: 'userRole', value: 'Admin', url: 'http://localhost' },
    { name: 'userName', value: 'Teste', url: 'http://localhost' },
    { name: 'userId', value: '00000000-0000-0000-0000-000000000001', url: 'http://localhost' },
  ])
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname
    if (path.endsWith('/site-config')) return route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(DEFAULT_SITE_CONFIG),
    })
    if (path.endsWith('/notifications/unread-count')) return route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ count: 0 }),
    })
    if (path.endsWith('/timers')) return route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify([]),
    })
    if (path.endsWith('/capital-giro')) return route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(capital({ vencidoReceber: 300 })),
    })
    if (path.endsWith('/estoque-inteligente')) return route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(estoque({ produtosRiscoRuptura: 1 })),
    })
    if (path.endsWith('/analytics/financeiro')) return route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(financeiro()),
    })
    return route.fulfill({ contentType: 'application/json', body: JSON.stringify({}) })
  })

  await page.goto('/admin/financeiro/insights')
  await page.waitForTimeout(500)
  const cookieButton = page.getByRole('button', { name: 'Recusar opcionais' })
  await cookieButton.waitFor({ state: 'visible', timeout: 2_000 }).then(() => cookieButton.click()).catch(() => {})

  await expect(page.getByRole('heading', { name: 'Insights Financeiros' })).toBeVisible()
  await expect(page.getByText('40.0%', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Urgente', exact: true }).click()
  await expect(page.getByText('Há dinheiro vencido para receber')).toBeVisible()
  await page.getByRole('button', { name: 'Entenda: Por que apareceu: Há dinheiro vencido para receber' }).click()
  await expect(page.getByRole('dialog', { name: 'Por que apareceu: Há dinheiro vencido para receber' })).toContainText('Soma crediários e outras entradas')
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true)
})
