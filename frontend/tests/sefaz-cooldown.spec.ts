import { expect, test } from '@playwright/test'
import { formatCountdown, secondsUntil } from '../lib/sefaz'

test.describe('proteção de cooldown da SEFAZ', () => {
  test('calcula a espera sem permitir valor negativo', () => {
    const now = Date.parse('2026-08-11T12:00:00Z')
    expect(secondsUntil('2026-08-11T13:05:00Z', now)).toBe(3900)
    expect(secondsUntil('2026-08-11T11:00:00Z', now)).toBe(0)
    expect(secondsUntil(undefined, now)).toBe(0)
  })

  test('formata a contagem regressiva para a interface', () => {
    expect(formatCountdown(3900)).toBe('01:05:00')
    expect(formatCountdown(9)).toBe('00:00:09')
  })
})
