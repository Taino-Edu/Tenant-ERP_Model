import { expect, test } from '@playwright/test'
import { resolveResetLoginPath } from '../lib/resetPassword'

test('convite da equipe direciona ao login administrativo', () => {
  expect(resolveResetLoginPath(null, 'platform')).toBe('/login')
  expect(resolveResetLoginPath('admin', null)).toBe('/login')
  expect(resolveResetLoginPath(null, null)).toBe('/entrar')
})
