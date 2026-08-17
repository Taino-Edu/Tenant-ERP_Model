import { expect, test } from '@playwright/test'

const invitation = {
  id: '8fd0e08a-8bb8-47b7-99a8-e76d2dc82d9c', name: 'Maria Indicadora', email: 'maria@example.com',
  partnerKind: 'Parceiro de indicação', setupCommissionPercent: 30, monthlyCommissionPercent: 5,
  paymentGraceDays: 5, contractVersion: '2026-08-17-v2', contractText: 'Regulamento de teste',
  expiresAt: '2026-08-24T12:00:00Z', sentAt: null, acceptedAt: null, revokedAt: null,
  signatureCodeSentAt: null, signedDocumentAvailable: false, status: 'Pendente', inviteUrl: null,
}

test('explica por que o aceite não pode continuar', async ({ page }) => {
  await page.route('**/api/public/referral-invitations/test-token', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(invitation) }))

  await page.goto('/parceiros/convite?token=test-token')
  await page.getByRole('button', { name: 'Enviar código para assinar' }).click()
  await expect(page.getByRole('alert').filter({ hasText: 'Informe um CPF com 11 dígitos.' })).toBeVisible()
})

test('confirma o email, assina e oferece o PDF', async ({ page }) => {
  await page.route('**/api/public/referral-invitations/test-token', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(invitation) }))

  let acceptanceBody: Record<string, unknown> | null = null
  await page.route('**/api/public/referral-invitations/test-token/request-signature', async route => {
    acceptanceBody = route.request().postDataJSON()
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ email: 'ma***@example.com', expiresAt: '2026-08-17T20:00:00Z' }) })
  })
  await page.route('**/api/public/referral-invitations/test-token/confirm-signature', async route => {
    expect(route.request().postDataJSON()).toEqual({ code: '123456' })
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ signedDocumentAvailable: true }) })
  })

  await page.goto('/parceiros/convite?token=test-token')
  await page.getByLabel('CPF').fill('123.456.789-01')
  await page.getByLabel('Telefone').fill('(11) 99999-9999')
  await page.getByRole('checkbox').check()
  await page.getByRole('button', { name: 'Enviar código para assinar' }).click()
  await expect(page.getByRole('heading', { name: 'Confirme seu e-mail' })).toBeVisible()
  await page.getByLabel('Código de confirmação').fill('123456')
  await page.getByRole('button', { name: 'Confirmar e assinar' }).click()

  await expect(page.getByRole('heading', { name: 'Parceria assinada' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Baixar documento assinado' })).toHaveAttribute('href', /signed-document$/)
  expect(acceptanceBody).toMatchObject({ name: 'Maria Indicadora', email: 'maria@example.com', document: '12345678901', acceptedTerms: true })
})
