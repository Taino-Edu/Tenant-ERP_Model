// =============================================================================
// auditFormat.ts — Interpreta o JSON de AuditLog.Details pra exibição legível.
// O backend grava formatos distintos dependendo da origem do registro (ver
// AuditService.BuildDetails e AuditSaveChangesInterceptor): um bloco de
// contexto de sessão (context.userAgent/geo), um diff de alteração
// (alteracoes[]), ou um snapshot de exclusão (snapshotExcluido). Isso
// centraliza a leitura desses formatos pra não espalhar JSON.parse pela UI.
// =============================================================================

export interface AuditUserAgentInfo {
  raw?: string | null
  os?: string | null
  osVersion?: string | null
  browser?: string | null
  browserVersion?: string | null
  device?: string | null
  isMobile?: boolean
}

export interface AuditGeoInfo {
  country?: string | null
  city?: string | null
}

export interface AuditContext {
  userAgent?: AuditUserAgentInfo
  geo?: AuditGeoInfo
}

export interface AuditChange {
  campo: string
  de: unknown
  para: unknown
}

export interface ParsedAuditDetails {
  context: AuditContext | null
  alteracoes: AuditChange[] | null
  snapshotExcluido: Record<string, unknown> | null
  message: string | null
  raw: Record<string, unknown> | null
}

const EMPTY: ParsedAuditDetails = { context: null, alteracoes: null, snapshotExcluido: null, message: null, raw: null }

export function parseAuditDetails(details: string | null | undefined): ParsedAuditDetails {
  if (!details) return EMPTY
  let obj: unknown
  try {
    obj = JSON.parse(details)
  } catch {
    return { ...EMPTY, message: details }
  }
  if (!obj || typeof obj !== 'object') return EMPTY
  const o = obj as Record<string, unknown>
  return {
    context:          (o.context as AuditContext) ?? null,
    alteracoes:       Array.isArray(o.alteracoes) ? (o.alteracoes as AuditChange[]) : null,
    snapshotExcluido: (o.snapshotExcluido as Record<string, unknown>) ?? null,
    message:          typeof o.message === 'string' ? o.message : typeof o.email === 'string' ? o.email as string : null,
    raw:              o,
  }
}

const FIELD_LABELS: Record<string, string> = {
  PriceInCents: 'Preço', CostPriceInCents: 'Preço de custo', DiscountPriceInCents: 'Preço promocional',
  StockQuantity: 'Estoque', MinimumStock: 'Estoque mínimo',
  Name: 'Nome', Email: 'E-mail', Role: 'Papel', IsActive: 'Ativo', IsFeatured: 'Destaque',
  ShowOnSite: 'Exibir no site', ShowOnMarketplace: 'Exibir no marketplace',
  UpdatedAt: 'Atualizado em', CreatedAt: 'Criado em',
  PasswordHash: 'Senha', RefreshToken: 'Token de sessão', PasswordResetToken: 'Token de redefinição de senha',
  TotalCents: 'Total', ValorTotalCents: 'Valor total',
}

export function fieldLabel(campo: string): string {
  return FIELD_LABELS[campo] ?? campo
}

function isCentsField(campo: string): boolean {
  return /cents$/i.test(campo)
}

function isDateField(value: unknown): value is string {
  return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)
}

export function formatAuditValue(campo: string, value: unknown): string {
  if (value === '[REDACTED]') return 'Oculto'
  if (value === null || value === undefined) return '—'
  if (isCentsField(campo)) {
    const n = Number(value)
    if (!Number.isNaN(n)) return (n / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
  }
  if (typeof value === 'boolean') return value ? 'Sim' : 'Não'
  if (isDateField(value)) {
    return new Date(value).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
  }
  return String(value)
}

/** Resumo curto de uma linha de log pra caber numa célula de tabela. */
export function summarizeAuditDetails(details: string | null | undefined): string {
  const parsed = parseAuditDetails(details)

  if (parsed.alteracoes && parsed.alteracoes.length > 0) {
    const relevantes = parsed.alteracoes.filter(c => c.campo !== 'UpdatedAt')
    const first = relevantes[0] ?? parsed.alteracoes[0]
    const extra = parsed.alteracoes.length > 1 ? ` (+${parsed.alteracoes.length - 1})` : ''
    return `${fieldLabel(first.campo)}: ${formatAuditValue(first.campo, first.de)} → ${formatAuditValue(first.campo, first.para)}${extra}`
  }

  if (parsed.snapshotExcluido) return 'Registro excluído'

  if (parsed.message) return parsed.message

  if (parsed.context?.userAgent || parsed.context?.geo) {
    const bits: string[] = []
    const ua = parsed.context.userAgent
    if (ua?.browser) bits.push(ua.browser)
    if (ua?.os) bits.push(ua.os)
    const geo = parsed.context.geo
    if (geo?.city) bits.push(`${geo.city}${geo.country ? `, ${geo.country}` : ''}`)
    else if (geo?.country) bits.push(geo.country)
    return bits.length > 0 ? bits.join(' · ') : '—'
  }

  return '—'
}
