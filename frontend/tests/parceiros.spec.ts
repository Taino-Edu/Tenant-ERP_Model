import { expect, test, type Page } from '@playwright/test'

// A /parceiros tem duas partes que quebram em silêncio: o simulador (uma conta
// errada continua renderizando um número bonito) e as telas do sistema (as
// barras do gráfico já sumiram uma vez por altura percentual sem pai com
// altura definida — a página não acusou nada, só ficou um retângulo vazio).

async function abrirParceiros(page: Page) {
  await page.route('**/api/**', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }))
  await page.goto('/parceiros')
}

test.describe('Programa de Afiliados', () => {
  test('o simulador calcula sobre a tabela real de planos', async ({ page }) => {
    await abrirParceiros(page)
    const painel = page.locator('#simulador')

    // Plano Rio (padrão, em destaque): R$ 269/mês e R$ 538 de implantação.
    // 3 indicações → 30% de 538 x3 = 484,20 e 5% de 269 x3 = 40,35/mês.
    await expect(painel).toContainText('R$ 484,20')
    await expect(painel).toContainText('R$ 40,35')
    await expect(painel).toContainText('R$ 968,40') // 484,20 + 40,35 x 12

    // Centavos obrigatórios: "R$ 484,2" já foi para produção uma vez.
    await expect(painel).not.toContainText(/R\$ \d+,\d(?!\d)/)
  })

  test('o plano sem taxa de implantação não promete comissão de implantação', async ({ page }) => {
    await abrirParceiros(page)
    await page.locator('#simulador').getByText('Mar', { exact: true }).click()

    const painel = page.locator('#simulador')
    await expect(painel).toContainText('R$ 0,00')
    await expect(painel).toContainText('implantação gratuita')
  })

  test('as telas do sistema trocam pelas abas e o gráfico tem barras', async ({ page }) => {
    await abrirParceiros(page)

    await expect(page.getByRole('tab', { name: 'PDV e caixa' })).toHaveAttribute('aria-selected', 'true')
    await expect(page.locator('#painel-pdv')).toContainText('Finalizar e emitir NFC-e')

    await page.getByRole('tab', { name: 'Relatórios' }).click()
    await expect(page.getByRole('tab', { name: 'Relatórios' })).toHaveAttribute('aria-selected', 'true')

    // O gráfico é feito de alturas percentuais: se o pai perder a altura
    // definida, todas as barras colapsam para zero sem erro nenhum.
    const alturas = await page.locator('#painel-relatorios [style*="height"]').evaluateAll(
      barras => barras.map(barra => barra.getBoundingClientRect().height))
    expect(alturas.length).toBeGreaterThan(0)
    expect(Math.min(...alturas)).toBeGreaterThan(10)
  })

  test('a candidatura identifica a origem do lead', async ({ page }) => {
    await abrirParceiros(page)
    const enviados: Record<string, unknown>[] = []
    await page.route('**/api/leads', async route => {
      enviados.push(route.request().postDataJSON())
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{"message":"ok"}' })
    })

    await page.locator('#candidatura').getByPlaceholder('Nome completo ou razão social').fill('Contador Teste')
    await page.locator('#candidatura').getByPlaceholder('(17) 99999-9999').fill('17999998888')
    await page.locator('#candidatura').getByRole('checkbox').check()
    await page.getByRole('button', { name: /Pedir meu convite/ }).click()

    await expect(page.locator('#candidatura')).toContainText('Candidatura recebida')
    expect(enviados).toHaveLength(1)
    // `campaign` separa a fila do CRM; `kind` é o que faz o backend gravar a
    // finalidade certa no registro de privacidade — candidato a parceiro não é
    // possível cliente da plataforma.
    expect(enviados[0]).toMatchObject({ campaign: 'afiliados', kind: 'Afiliados' })
    expect(String(enviados[0].mensagem)).toContain('Programa de Afiliados')
  })
})
