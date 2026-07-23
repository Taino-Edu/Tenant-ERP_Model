import { test, expect, type Page } from '@playwright/test'

/**
 * Pré-voo + caminho homologação → emissão → cupom, conforme seção 11 do plano
 * docs/PLANO-APLICACAO-MOTOR-FISCAL-GRATUITO-UI-UX.md.
 *
 * Requer o backend (porta 5000) e o frontend (porta 3000) já rodando — este
 * projeto não sobe webServer automático porque o backend precisa de migrations/
 * seed antes de aceitar requisições (ver .claude/launch.json > backend-dev).
 */

const ADMIN_EMAIL    = process.env.ADMIN_SEED_EMAIL    ?? 'admin@tenant-erp.local'
const ADMIN_PASSWORD = process.env.ADMIN_SEED_PASSWORD ?? 'SenhaForte@123'

async function login(page: Page) {
  await page.goto('/login')
  await page.getByPlaceholder('seu@email.com.br').fill(ADMIN_EMAIL)
  await page.getByPlaceholder('••••••••').fill(ADMIN_PASSWORD)
  await page.getByRole('button', { name: 'Entrar no Painel' }).click()
  await expect(page.getByRole('button', { name: 'Recolher menu' })).toBeVisible({ timeout: 15_000 })
}

test.describe('Fiscal — pré-voo (Visão Geral)', () => {
  test('mostra status, checklist e próxima ação consistentes com a configuração atual', async ({ page }) => {
    await login(page)
    await page.goto('/admin/fiscal')

    // O card de Visão Geral responde em poucos segundos (seção 4.1 do plano):
    // status geral, checklist de ativação e a próxima ação recomendada.
    const statusBadge = page.getByText(/^(Pronto para emitir|Requer atenção|Bloqueado)$/)
    await expect(statusBadge).toBeVisible({ timeout: 10_000 })

    await expect(page.getByRole('button', { name: 'Dados da empresa', exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Certificado digital', exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Regra fiscal padrão', exact: true })).toBeVisible()

    const status = await statusBadge.textContent()
    if (status !== 'Pronto para emitir') {
      // Bloqueado/RequerAtencao sempre vêm com uma próxima ação e ao menos uma
      // pendência explicando o motivo — nunca um estado "vazio" sem explicação.
      await expect(page.getByText('Próxima ação:')).toBeVisible()
      await expect(page.locator('ul li').first()).toBeVisible()
    }
  })

  test('o checklist "Produtos com NCM" leva para o cadastro de estoque', async ({ page }) => {
    await login(page)
    await page.goto('/admin/fiscal')
    await page.getByRole('button', { name: 'Produtos com NCM' }).click()
    await expect(page).toHaveURL(/\/admin\/estoque$/)
  })
})

test.describe('Fiscal — homologação → emissão → cupom', () => {
  // Este caminho faz uma chamada real à SEFAZ (ambiente de homologação) e por
  // isso exige um tenant com certificado A1, CSC e natureza fiscal padrão já
  // configurados (ver docs/GO-LIVE-FISCAL-2026-07-25.md) — não é possível nem
  // desejável fabricar esses segredos num teste automatizado. Rode com
  // RUN_FULL_FISCAL_E2E=1 contra um tenant de homologação real (ex: o ambiente
  // teste-fiscal.2esysten.com.br) e com pelo menos um produto com estoque.
  test.skip(!process.env.RUN_FULL_FISCAL_E2E, 'requer RUN_FULL_FISCAL_E2E=1 e um tenant de homologação configurado')

  test('emite NFC-e numa venda avulsa em dinheiro e abre o cupom', async ({ page }) => {
    await login(page)

    await page.goto('/admin/fiscal')
    await expect(page.getByText('Pronto para emitir')).toBeVisible({ timeout: 10_000 })

    await page.goto('/admin/venda-avulsa')
    await page.getByRole('button', { name: 'Nova Venda' }).click()

    // Etapa 1 (cliente) — sem selecionar cliente, "Pular" avança pra etapa 2.
    await page.getByRole('button', { name: /Pular|Próximo/ }).click()

    // Etapa 2 (produtos) — adiciona o primeiro produto com estoque disponível.
    await page.getByPlaceholder('Buscar produto, categoria ou código de barras…').fill('')
    await page.getByTitle('Adicionar').first().click()
    await page.getByRole('button', { name: 'Próximo' }).click()

    // Etapa 3 (pagamento) — Dinheiro não exige o grupo `card` do XML.
    await page.getByRole('button', { name: 'Dinheiro', exact: true }).click()
    await page.getByText('Emitir cupom fiscal (NFC-e) agora').click()
    await page.getByRole('button', { name: 'Confirmar venda' }).click()

    // Autorizada, Processando (retry em segundo plano) ou Contingência são os
    // três resultados aceitáveis descritos na seção 5 do plano — só "erro
    // silencioso sem explicação" seria uma falha real.
    await expect(page.getByText(/Autorizad|Processando|Contingência/i)).toBeVisible({ timeout: 20_000 })
  })
})
