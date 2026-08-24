import type { CapitalGiroDto, EstoqueInteligenteDto, FinanceiroDto } from '@/lib/api'

export type InsightSeverity = 'urgent' | 'attention' | 'opportunity' | 'positive'

export interface FinancialInsight {
  id: string
  severity: InsightSeverity
  category: 'Caixa' | 'Resultado' | 'Estoque' | 'Dados'
  title: string
  detail: string
  rationale: string
  impactValue?: number
  actionLabel: string
  href: string
}

export interface FinancialInsightSummary {
  insights: FinancialInsight[]
  revenueVariation: number | null
  resultVariation: number | null
  lowMovementCapital: number
  urgentCount: number
  attentionCount: number
}

interface InsightInput {
  current: FinanceiroDto
  previous: FinanceiroDto
  capital: CapitalGiroDto
  stock: EstoqueInteligenteDto
}

const severityOrder: Record<InsightSeverity, number> = {
  urgent: 0,
  attention: 1,
  opportunity: 2,
  positive: 3,
}

function variation(current: number, previous: number) {
  if (previous === 0) return null
  return ((current - previous) / Math.abs(previous)) * 100
}

export function buildFinancialInsights({ current, previous, capital, stock }: InsightInput): FinancialInsightSummary {
  const insights: FinancialInsight[] = []
  const revenueVariation = variation(current.receita, previous.receita)
  const resultVariation = variation(current.resultadoLiquido, previous.resultadoLiquido)
  const lowMovementCapital = stock.produtos
    .filter(product => product.situacao === 'excesso' || product.situacao === 'sem_movimento')
    .reduce((total, product) => total + product.valorEstoque, 0)

  if (current.resultadoLiquido < 0) insights.push({
    id: 'negative-result', severity: 'urgent', category: 'Resultado', title: 'O período está fechando no negativo',
    detail: 'As receitas não cobriram CMV, despesas e resultado financeiro no intervalo selecionado.',
    rationale: 'Resultado líquido = receita líquida − CMV − despesas operacionais + resultado financeiro − impostos sobre lucro.',
    impactValue: Math.abs(current.resultadoLiquido), actionLabel: 'Simular equilíbrio', href: '/admin/financeiro/ponto-de-equilibrio',
  })
  if (capital.vencidoPagar > 0) insights.push({
    id: 'overdue-payables', severity: 'urgent', category: 'Caixa', title: 'Existem pagamentos vencidos',
    detail: 'Regularize ou renegocie os títulos para reduzir multas e pressão imediata no caixa.',
    rationale: 'Soma somente saídas com saldo pendente cuja data de vencimento já passou.',
    impactValue: capital.vencidoPagar, actionLabel: 'Ver contas', href: '/admin/contas-receber',
  })
  if (capital.vencidoReceber > 0) insights.push({
    id: 'overdue-receivables', severity: 'urgent', category: 'Caixa', title: 'Há dinheiro vencido para receber',
    detail: 'Priorize a cobrança dos clientes em atraso antes de buscar mais capital para a operação.',
    rationale: 'Soma crediários e outras entradas ainda abertas depois da data de vencimento.',
    impactValue: capital.vencidoReceber, actionLabel: 'Iniciar cobrança', href: '/admin/crediario',
  })
  if (stock.produtosRiscoRuptura > 0) insights.push({
    id: 'stockout', severity: 'urgent', category: 'Estoque', title: 'Produtos correm risco de faltar',
    detail: `${stock.produtosRiscoRuptura} produto${stock.produtosRiscoRuptura === 1 ? '' : 's'} zerado${stock.produtosRiscoRuptura === 1 ? '' : 's'}, abaixo do mínimo ou com até 14 dias de cobertura.`,
    rationale: 'A cobertura divide o saldo atual pela venda média diária do período.',
    actionLabel: 'Planejar reposição', href: '/admin/financeiro/estoque-inteligente',
  })
  if (capital.vencePagar7Dias > 0) insights.push({
    id: 'next-payables', severity: 'attention', category: 'Caixa', title: 'Prepare o caixa dos próximos 7 dias',
    detail: 'Há compromissos próximos que precisam entrar no planejamento de recebimentos.',
    rationale: 'Soma contas a pagar ainda abertas com vencimento entre hoje e os próximos sete dias.',
    impactValue: capital.vencePagar7Dias, actionLabel: 'Projetar o caixa', href: '/admin/financeiro/projecao-caixa',
  })
  if (current.lancamentosNaoClassificados > 0) insights.push({
    id: 'unclassified', severity: 'attention', category: 'Dados', title: 'A DRE possui lançamentos sem classificação',
    detail: 'Classifique-os para que o lucro operacional e o resultado financeiro sejam confiáveis.',
    rationale: 'Conta lançamentos que ainda não informam se são CMV, despesa, investimento, imposto ou resultado financeiro.',
    impactValue: current.lancamentosNaoClassificados, actionLabel: 'Classificar lançamentos', href: '/admin/contas-receber',
  })
  if (stock.produtosSemCusto > 0) insights.push({
    id: 'missing-cost', severity: 'attention', category: 'Dados', title: 'Produtos sem custo distorcem a rentabilidade',
    detail: `${stock.produtosSemCusto} produto${stock.produtosSemCusto === 1 ? '' : 's'} com estoque não possui custo válido.`,
    rationale: 'Sem custo, margem, markup, capital imobilizado e GMROI ficam incompletos ou subestimados.',
    actionLabel: 'Corrigir custos', href: '/admin/estoque',
  })
  if (current.receita > 0 && current.margemPercent < 20) insights.push({
    id: 'low-margin', severity: 'attention', category: 'Resultado', title: 'A margem bruta merece revisão',
    detail: `A margem do período está em ${current.margemPercent.toFixed(1)}%. Revise preço e custo por produto antes de aplicar descontos.`,
    rationale: 'O limite de 20% é um sinal operacional de atenção, não uma meta universal para todos os negócios.',
    impactValue: current.margem, actionLabel: 'Analisar preços', href: '/admin/financeiro/rentabilidade',
  })
  if (lowMovementCapital > 0) insights.push({
    id: 'low-movement', severity: 'opportunity', category: 'Estoque', title: 'Capital pode ser liberado do estoque',
    detail: 'Produtos sem movimento ou com mais de 90 dias de cobertura concentram recursos que poderiam voltar ao caixa.',
    rationale: 'Valor calculado pelo custo atual dos produtos classificados como excesso ou sem movimento.',
    impactValue: lowMovementCapital, actionLabel: 'Revisar produtos', href: '/admin/financeiro/estoque-inteligente',
  })
  if (revenueVariation !== null && revenueVariation <= -10) insights.push({
    id: 'revenue-down', severity: 'attention', category: 'Resultado', title: 'A receita caiu contra o período anterior',
    detail: `A redução foi de ${Math.abs(revenueVariation).toFixed(1)}% em intervalos com a mesma quantidade de dias.`,
    rationale: 'Compara a receita do período selecionado com o intervalo imediatamente anterior de igual duração.',
    actionLabel: 'Investigar vendas', href: '/admin/financeiro',
  })
  if (revenueVariation !== null && revenueVariation >= 10) insights.push({
    id: 'revenue-up', severity: 'positive', category: 'Resultado', title: 'A receita avançou no período',
    detail: `O crescimento foi de ${revenueVariation.toFixed(1)}% contra o intervalo anterior de igual duração.`,
    rationale: 'O crescimento de receita deve ser lido junto da margem e do resultado líquido.',
    actionLabel: 'Ver composição', href: '/admin/financeiro',
  })
  if (current.resultadoLiquido > 0) insights.push({
    id: 'positive-result', severity: 'positive', category: 'Resultado', title: 'A operação gerou resultado positivo',
    detail: 'Depois dos custos e lançamentos classificados, o período apresenta sobra operacional e financeira.',
    rationale: 'A confirmação depende de todos os custos e despesas estarem cadastrados e corretamente classificados.',
    impactValue: current.resultadoLiquido, actionLabel: 'Conferir a DRE', href: '/admin/financeiro',
  })

  insights.sort((a, b) => severityOrder[a.severity] - severityOrder[b.severity] || (b.impactValue ?? 0) - (a.impactValue ?? 0))

  return {
    insights,
    revenueVariation,
    resultVariation,
    lowMovementCapital,
    urgentCount: insights.filter(item => item.severity === 'urgent').length,
    attentionCount: insights.filter(item => item.severity === 'attention').length,
  }
}

export function previousPeriod(inicio: string, fim: string) {
  const start = new Date(`${inicio}T12:00:00`)
  const end = new Date(`${fim}T12:00:00`)
  const durationDays = Math.max(1, Math.round((end.getTime() - start.getTime()) / 86_400_000) + 1)
  const previousEnd = new Date(start)
  previousEnd.setDate(previousEnd.getDate() - 1)
  const previousStart = new Date(previousEnd)
  previousStart.setDate(previousStart.getDate() - durationDays + 1)
  const format = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
  return { inicio: format(previousStart), fim: format(previousEnd) }
}
