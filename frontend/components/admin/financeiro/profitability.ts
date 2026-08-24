import type { TopProductFinDto } from '@/lib/api'

export interface ProfitabilityRow extends TopProductFinDto {
  averagePrice: number
  averageCost: number
  grossMarginPercent: number | null
  markupPercent: number | null
  suggestedPrice: number | null
  priceGap: number | null
  hasCost: boolean
}

export function calculateProfitability(product: TopProductFinDto, targetMarginPercent: number): ProfitabilityRow {
  const averagePrice = product.qtd > 0 ? product.receita / product.qtd : 0
  const averageCost = product.qtd > 0 ? product.custo / product.qtd : 0
  const hasCost = averageCost > 0
  const grossMarginPercent = averagePrice > 0 && hasCost
    ? ((averagePrice - averageCost) / averagePrice) * 100
    : null
  const markupPercent = hasCost
    ? ((averagePrice - averageCost) / averageCost) * 100
    : null
  const safeTarget = Math.min(95, Math.max(1, targetMarginPercent))
  const suggestedPrice = hasCost ? averageCost / (1 - safeTarget / 100) : null

  return {
    ...product,
    averagePrice,
    averageCost,
    grossMarginPercent,
    markupPercent,
    suggestedPrice,
    priceGap: suggestedPrice === null ? null : suggestedPrice - averagePrice,
    hasCost,
  }
}
