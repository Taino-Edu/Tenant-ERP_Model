import { expect, test, type Page } from '@playwright/test'

const tenantBase = process.env.QA_TENANT_URL
const tenantSlug = process.env.QA_TENANT_SLUG
const adminEmail = process.env.QA_ADMIN_EMAIL
const adminPassword = process.env.QA_ADMIN_PASSWORD
const platformBase = process.env.QA_ROOT_URL ?? 'http://localhost:3000'

test.describe('QA funcional — restaurante, comanda e contador', () => {
  test.skip(!tenantBase || !tenantSlug || !adminEmail || !adminPassword,
    'Defina QA_TENANT_URL, QA_TENANT_SLUG, QA_ADMIN_EMAIL e QA_ADMIN_PASSWORD.')

  test('fluxo completo com permissões e persistência', async ({ browser }) => {
    test.setTimeout(120_000)
    const suffix = Date.now()
    const errors: string[] = []

    const watch = (page: Page) => {
      page.on('pageerror', error => errors.push(`pageerror: ${error.message}`))
      page.on('console', message => {
        if (message.type() === 'error') errors.push(`console: ${message.text()}`)
      })
      page.on('response', response => {
        if (response.status() >= 500) errors.push(`http ${response.status()}: ${response.url()}`)
        if (response.status() === 404) errors.push(`http 404: ${response.url()}`)
      })
    }

    const adminContext = await browser.newContext()
    const admin = await adminContext.newPage()
    watch(admin)

    await admin.goto(`${tenantBase}/login`)
    await expect(admin.getByText('Painel de Gestão')).toBeVisible()
    await admin.getByLabel('E-mail').fill(adminEmail!)
    await admin.getByLabel('Senha', { exact: true }).fill(adminPassword!)
    await admin.getByRole('button', { name: 'Entrar no Painel' }).click()
    await expect(admin).toHaveURL(/\/admin\/comanda/)
    await expect(admin.getByRole('heading', { name: 'Comanda' })).toBeVisible()

    const productResponse = await adminContext.request.post(`${tenantBase}/api/product`, {
      data: {
        name: `Produto QA ${suffix}`, category: 'QA', barcode: `789${suffix}`.slice(0, 13),
        priceInCents: 1590, costPriceInCents: 700, stockQuantity: 12, minimumStock: 3,
        isActive: true, showOnSite: false, showOnMarketplace: false,
      },
    })
    expect(productResponse.ok(), await productResponse.text()).toBeTruthy()
    const product = await productResponse.json()

    const customerResponse = await adminContext.request.post(`${tenantBase}/api/user`, {
      data: {
        name: `Cliente QA ${suffix}`, email: `cliente.qa.${suffix}@example.test`,
        password: 'QaCliente@2026', role: 'Customer',
      },
    })
    expect(customerResponse.ok(), await customerResponse.text()).toBeTruthy()
    const customer = await customerResponse.json()

    const comandaResponse = await adminContext.request.post(`${tenantBase}/api/comanda/admin-open`, {
      data: { userId: customer.id, tableIdentifier: 'Mesa-QA' },
    })
    expect(comandaResponse.ok(), await comandaResponse.text()).toBeTruthy()

    await admin.reload()
    const card = admin.locator('.card').filter({ hasText: `Cliente QA ${suffix}` }).first()
    await expect(card).toBeVisible()
    await card.getByRole('button', { name: 'Ver itens' }).click()
    const comentario = `Sem cebola — QA ${suffix}`
    await card.getByPlaceholder(/sem cebola/i).fill(comentario)
    await card.getByTitle('Salvar comentário').click()
    await expect(admin.getByText('Comentário salvo.')).toBeVisible()

    const dashboardResponse = await adminContext.request.get(`${tenantBase}/api/comanda/dashboard`)
    expect(dashboardResponse.ok()).toBeTruthy()
    const comandas = await dashboardResponse.json()
    expect(comandas.find((c: { userId: string }) => c.userId === customer.id)?.notes).toBe(comentario)

    const contadorContext = await browser.newContext()
    const contador = await contadorContext.newPage()
    watch(contador)
    const contadorEmail = `contador.qa.${suffix}@example.test`
    await contador.goto(`${platformBase}/contador/cadastro`)
    await contador.getByLabel('Nome completo').fill(`Contador QA ${suffix}`)
    await contador.getByLabel('E-mail').fill(contadorEmail)
    await contador.getByLabel('Slug da loja').fill(tenantSlug!)
    await contador.getByLabel('Senha', { exact: true }).fill('QaContador@2026')
    await contador.getByLabel('Confirmar senha').fill('QaContador@2026')
    await contador.getByRole('button', { name: /Criar conta/ }).click()
    await expect(contador).toHaveURL(/\/contador$/)
    await expect(contador.getByText('Aguardando aprovação')).toBeVisible()

    const solicitacoesResponse = await adminContext.request.get(`${tenantBase}/api/fiscal/contador/solicitacoes`)
    expect(solicitacoesResponse.ok(), await solicitacoesResponse.text()).toBeTruthy()
    const solicitacoes = await solicitacoesResponse.json()
    const solicitacao = solicitacoes.find((s: { email: string }) => s.email === contadorEmail)
    expect(solicitacao).toBeTruthy()
    const approveResponse = await adminContext.request.post(
      `${tenantBase}/api/fiscal/contador/solicitacoes/${solicitacao.linkId}/aprovar`)
    expect(approveResponse.ok(), await approveResponse.text()).toBeTruthy()

    await contador.reload()
    await contador.getByRole('button', { name: new RegExp(tenantSlug!) }).click()
    // A classificação fiscal do estoque agora fica numa aba do cliente.
    await contador.getByRole('button', { name: 'Estoque e NCM' }).click()
    await expect(contador.getByText('Estoque e classificação fiscal')).toBeVisible()
    const productRow = contador.locator('.rounded-xl').filter({ hasText: `Produto QA ${suffix}` }).first()
    await expect(productRow).toContainText('Estoque: 12')
    await productRow.getByRole('button').first().click()
    await productRow.getByPlaceholder('NCM (8 dígitos)').fill('1905.90.90')
    await productRow.getByPlaceholder('CEST (7 dígitos)').fill('17.062.00')
    await productRow.getByPlaceholder('Federal %').fill('12,34')
    await productRow.getByPlaceholder('Estadual %').fill('18')
    await productRow.getByPlaceholder('Municipal %').fill('0')
    await productRow.getByPlaceholder(/Fonte/).fill('Tabela QA 2026')
    await productRow.getByRole('button', { name: 'Salvar dados fiscais' }).click()
    await expect(contador.getByText(/Classificação fiscal.*atualizada/)).toBeVisible()

    // O ID do tenant não vem no cadastro do produto; usa a lista autorizada do portal.
    const clientesResponse = await contadorContext.request.get(`${platformBase}/api/contador-portal/clientes`)
    expect(clientesResponse.ok()).toBeTruthy()
    const clientes = await clientesResponse.json()
    const tenantId = clientes.find((c: { slug: string }) => c.slug === tenantSlug)?.tenantId
    expect(tenantId).toBeTruthy()
    const fiscalResponse = await contadorContext.request.get(
      `${platformBase}/api/contador-portal/clientes/${tenantId}/produtos`)
    expect(fiscalResponse.ok()).toBeTruthy()
    const fiscalProducts = await fiscalResponse.json()
    const fiscalProduct = fiscalProducts.find((p: { id: string }) => p.id === product.id)
    expect(fiscalProduct).toMatchObject({
      ncm: '19059090', cest: '1706200', percentualTributosFederais: 12.34,
      percentualTributosEstaduais: 18, percentualTributosMunicipais: 0,
      fonteTributos: 'Tabela QA 2026', stockQuantity: 12,
    })

    const stockMutation = await contadorContext.request.patch(
      `${platformBase}/api/product/${product.id}/stock`, { data: { delta: 99 } })
    expect(stockMutation.status()).toBe(403)
    const crossTenant = await contadorContext.request.get(
      `${platformBase}/api/contador-portal/clientes/00000000-0000-0000-0000-000000000099/produtos`)
    expect(crossTenant.status()).toBe(403)

    // Evita esconder falhas reais de runtime. 401/403 esperados não entram aqui.
    expect(errors).toEqual([])
    await adminContext.close()
    await contadorContext.close()
  })
})
