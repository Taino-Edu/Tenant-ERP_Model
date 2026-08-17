import { expect, test } from '@playwright/test'

const invitation = {
  id: '8fd0e08a-8bb8-47b7-99a8-e76d2dc82d9c',
  name: 'Maria Vendedora',
  email: 'maria@example.com',
  partnerKind: 'Vendedor',
  setupCommissionPercent: 30,
  monthlyCommissionPercent: 5,
  paymentGraceDays: 5,
  contractVersion: '2026-08',
  contractText: 'Regulamento de teste',
  expiresAt: '2026-08-24T12:00:00Z',
  sentAt: null,
  acceptedAt: null,
  revokedAt: null,
  status: 'Pendente',
  inviteUrl: null,
}

test('explica por que o aceite não pode continuar', async ({ page }) => {
  await page.route('**/api/public/referral-invitations/test-token', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(invitation) }))

  await page.goto('/parceiros/convite?token=test-token')
  await page.getByRole('button', { name: 'Aceitar e concluir cadastro' }).click()

  await expect(page.getByRole('alert').filter({ hasText: 'Informe um CPF com 11 dígitos.' })).toBeVisible()
})

test('envia o cadastro e confirma o aceite do convite', async ({ page }) => {
  await page.route('**/api/public/referral-invitations/test-token', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(invitation) }))

  let acceptedBody: Record<string, unknown> | null = null
  await page.route('**/api/public/referral-invitations/test-token/accept', async route => {
    acceptedBody = route.request().postDataJSON()
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ message: 'Cadastro e aceite registrados com sucesso.' }) })
  })

  await page.goto('/parceiros/convite?token=test-token')
  await page.getByLabel('CPF').fill('123.456.789-01')
  await page.getByLabel('Telefone').fill('(11) 99999-9999')
  await page.getByRole('checkbox').check()
  await page.getByRole('button', { name: 'Aceitar e concluir cadastro' }).click()

  await expect(page.getByRole('heading', { name: 'Parceria registrada' })).toBeVisible()
  expect(acceptedBody).toMatchObject({
    name: 'Maria Vendedora',
    email: 'maria@example.com',
    document: '12345678901',
    acceptedTerms: true,
  })
})
