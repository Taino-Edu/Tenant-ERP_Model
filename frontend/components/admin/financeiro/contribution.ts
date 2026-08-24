export interface ContributionInput {
  revenue: number
  cmv: number
  salesTax: number
  fixedExpenses: number
  cardFeePercent: number
  commissionPercent: number
  freightPercent: number
  discountPercent: number
}

export interface ContributionResult {
  discountedRevenue: number
  knownTaxPercent: number
  additionalVariablePercent: number
  totalVariablePercent: number
  variableExpenses: number
  contributionMargin: number
  contributionMarginPercent: number | null
  breakEvenRevenue: number | null
  safetyMargin: number | null
  safetyMarginPercent: number | null
  projectedOperatingResult: number
  maxDiscountContributionPercent: number | null
  maxDiscountBreakEvenPercent: number | null
}

const clampPercent = (value: number) => Math.min(100, Math.max(0, Number.isFinite(value) ? value : 0))

export function calculateContribution(input: ContributionInput): ContributionResult {
  const revenue = Math.max(0, input.revenue)
  const cmv = Math.max(0, input.cmv)
  const fixedExpenses = Math.max(0, input.fixedExpenses)
  const knownTaxPercent = revenue > 0 ? clampPercent((Math.max(0, input.salesTax) / revenue) * 100) : 0
  const additionalVariablePercent = clampPercent(
    clampPercent(input.cardFeePercent) + clampPercent(input.commissionPercent) + clampPercent(input.freightPercent),
  )
  const totalVariablePercent = clampPercent(knownTaxPercent + additionalVariablePercent)
  const discountPercent = clampPercent(input.discountPercent)
  const discountedRevenue = revenue * (1 - discountPercent / 100)
  const variableExpenses = discountedRevenue * (totalVariablePercent / 100)
  const contributionMargin = discountedRevenue - cmv - variableExpenses
  const contributionMarginPercent = discountedRevenue > 0 ? (contributionMargin / discountedRevenue) * 100 : null
  const breakEvenRevenue = contributionMarginPercent !== null && contributionMarginPercent > 0
    ? fixedExpenses / (contributionMarginPercent / 100)
    : null
  const safetyMargin = breakEvenRevenue === null ? null : discountedRevenue - breakEvenRevenue
  const safetyMarginPercent = safetyMargin === null || discountedRevenue <= 0 ? null : (safetyMargin / discountedRevenue) * 100
  const netRevenueFactor = 1 - totalVariablePercent / 100
  const maxDiscountContributionPercent = revenue > 0 && netRevenueFactor > 0
    ? clampPercent((1 - cmv / (revenue * netRevenueFactor)) * 100)
    : null
  const maxDiscountBreakEvenPercent = revenue > 0 && netRevenueFactor > 0
    ? clampPercent((1 - (cmv + fixedExpenses) / (revenue * netRevenueFactor)) * 100)
    : null

  return {
    discountedRevenue,
    knownTaxPercent,
    additionalVariablePercent,
    totalVariablePercent,
    variableExpenses,
    contributionMargin,
    contributionMarginPercent,
    breakEvenRevenue,
    safetyMargin,
    safetyMarginPercent,
    projectedOperatingResult: contributionMargin - fixedExpenses,
    maxDiscountContributionPercent,
    maxDiscountBreakEvenPercent,
  }
}
