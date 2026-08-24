import { expect, test } from '@playwright/test'
import { calculateCashProjection } from '../components/admin/financeiro/cash-projection'
import type { AgendaCaixaDiaDto } from '../lib/api'

const days: AgendaCaixaDiaDto[] = [
  { data: '2026-08-24T00:00:00', receberCrediario: 0, receberOutros: 0, pagar: 500, saldoLiquido: -500 },
  { data: '2026-08-25T00:00:00', receberCrediario: 100, receberOutros: 0, pagar: 0, saldoLiquido: 100 },
  { data: '2026-08-26T00:00:00', receberCrediario: 0, receberOutros: 300, pagar: 0, saldoLiquido: 300 },
]

test('identifica primeiro risco e necessidade para preservar a reserva', () => {
  const result = calculateCashProjection(days, 200, 0, 100)

  expect(result.dias.map(day => day.saldoProjetado)).toEqual([-300, -200, 100])
  expect(result.menorSaldo).toBe(-300)
  expect(result.primeiraDataRisco).toBe(days[0].data)
  expect(result.necessidadeCaixa).toBe(400)
})

test('inclui geração diária apenas quando informada pelo lojista', () => {
  const result = calculateCashProjection(days, 200, 150, 100)

  expect(result.dias.map(day => day.saldoProjetado)).toEqual([-150, 100, 550])
  expect(result.dias.every(day => day.geracaoEsperada === 150)).toBe(true)
  expect(result.necessidadeCaixa).toBe(250)
})

test('não acusa risco quando todos os dias preservam a reserva', () => {
  const result = calculateCashProjection(days, 1_000, 0, 100)

  expect(result.primeiraDataRisco).toBeNull()
  expect(result.necessidadeCaixa).toBe(0)
  expect(result.saldoFinal).toBe(900)
})
