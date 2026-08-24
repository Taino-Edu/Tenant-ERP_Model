import type { AgendaCaixaDiaDto } from '@/lib/api'

export interface CashProjectionDay extends AgendaCaixaDiaDto {
  geracaoEsperada: number
  saldoProjetado: number
  abaixoReserva: boolean
}

export interface CashProjectionResult {
  dias: CashProjectionDay[]
  saldoFinal: number
  menorSaldo: number
  primeiraDataRisco: string | null
  necessidadeCaixa: number
}

export function calculateCashProjection(
  days: AgendaCaixaDiaDto[],
  initialBalance: number,
  dailyExpectedGeneration: number,
  minimumReserve: number,
): CashProjectionResult {
  let balance = Number.isFinite(initialBalance) ? initialBalance : 0
  const generation = Number.isFinite(dailyExpectedGeneration) ? dailyExpectedGeneration : 0
  const reserve = Math.max(0, Number.isFinite(minimumReserve) ? minimumReserve : 0)
  let minimum = balance
  let firstRisk: string | null = balance < reserve ? days[0]?.data ?? null : null

  const projectedDays = days.map(day => {
    balance += day.saldoLiquido + generation
    minimum = Math.min(minimum, balance)
    const belowReserve = balance < reserve
    if (belowReserve && firstRisk === null) firstRisk = day.data
    return { ...day, geracaoEsperada: generation, saldoProjetado: balance, abaixoReserva: belowReserve }
  })

  return {
    dias: projectedDays,
    saldoFinal: balance,
    menorSaldo: minimum,
    primeiraDataRisco: firstRisk,
    necessidadeCaixa: Math.max(0, reserve - minimum),
  }
}
