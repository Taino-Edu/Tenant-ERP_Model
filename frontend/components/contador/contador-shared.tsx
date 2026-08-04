'use client'
// =============================================================================
// contador-shared.tsx — Formatação, download e blocos pequenos repetidos entre
// as abas do portal do contador. Mesmo papel do financeiro-shared.tsx no admin.
// =============================================================================
import { ReactNode } from 'react'
import { AlertTriangle, Info } from 'lucide-react'
import clsx from 'clsx'
import type { BadgeTone } from '@/components/admin/ui/Badge'

/** Centavos → "R$ 1.234,56". */
export const fmtCentavos = (cents: number) =>
  (cents / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

/** Reais (decimal) → "R$ 1.234,56". */
export const fmtReais = (valor: number) =>
  valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

export const fmtPercent = (valor: number, casas = 2) =>
  `${valor.toLocaleString('pt-BR', { minimumFractionDigits: casas, maximumFractionDigits: casas })}%`

/** Hoje no calendário de Brasília, em "yyyy-MM-dd" (formato do <input type="date">). */
export const brToday = () =>
  new Intl.DateTimeFormat('fr-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date())

/** "2026-08-04" → "04/08/2026". */
export const isoParaBr = (iso: string) => iso.split('-').reverse().join('/')

export function diasAte(dataIso?: string): number | null {
  if (!dataIso) return null
  return Math.ceil((new Date(dataIso).getTime() - Date.now()) / 86400000)
}

export function diasDesde(dataIso?: string): number | null {
  if (!dataIso) return null
  return Math.floor((Date.now() - new Date(dataIso).getTime()) / 86400000)
}

/** Dispara o download de um Blob já recebido da API. */
export function baixarBlob(data: Blob, nomeArquivo: string) {
  const url = URL.createObjectURL(data)
  const link = document.createElement('a')
  link.href = url
  link.download = nomeArquivo
  link.click()
  URL.revokeObjectURL(url)
}

/** Monta e baixa um CSV — BOM na frente pro Excel não comer os acentos. */
export function baixarCsv(nomeArquivo: string, linhas: string[]) {
  baixarBlob(
    new Blob(['﻿' + linhas.join('\r\n')], { type: 'text/csv;charset=utf-8' }),
    nomeArquivo,
  )
}

export const MESES = [
  'janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho',
  'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro',
]

export const STATUS_NOTA_TONE: Record<string, BadgeTone> = {
  Autorizada:             'success',
  AutorizadaContingencia: 'warning',
  PendenteEmissao:        'warning',
  Cancelada:              'danger',
  Rejeitada:              'danger',
}

/** Cabeçalho de seção dentro de uma aba — título, subtítulo e ações à direita. */
export function SecaoHeader({ icon: Icon, titulo, descricao, acoes }: {
  icon?: React.ComponentType<{ className?: string }>
  titulo: string
  descricao?: string
  acoes?: ReactNode
}) {
  return (
    <div className="flex items-start justify-between gap-3 flex-wrap">
      <div>
        <h3 className="font-bold text-white flex items-center gap-2">
          {Icon && <Icon className="w-4 h-4 text-brand-400" />}
          {titulo}
        </h3>
        {descricao && <p className="text-xs text-gray-500 mt-1">{descricao}</p>}
      </div>
      {acoes && <div className="flex items-center gap-2 flex-wrap">{acoes}</div>}
    </div>
  )
}

/** Faixa de aviso/ressalva. `tone="info"` pra contexto, `"warning"` pra pendência. */
export function Aviso({ tone = 'info', children }: { tone?: 'info' | 'warning'; children: ReactNode }) {
  const Icon = tone === 'warning' ? AlertTriangle : Info
  return (
    <div className={clsx(
      'flex items-start gap-2 rounded-xl border p-3 text-xs',
      tone === 'warning'
        ? 'border-amber-500/30 bg-amber-500/5 text-amber-400'
        : 'border-surface-500 bg-surface-800/40 text-gray-400',
    )}>
      <Icon className="w-4 h-4 shrink-0 mt-0.5" />
      <div className="space-y-1">{children}</div>
    </div>
  )
}

/** Par "De/Até" usado em toda aba que filtra por período. */
export function PeriodoFields({ inicio, fim, onInicio, onFim }: {
  inicio: string; fim: string
  onInicio: (v: string) => void; onFim: (v: string) => void
}) {
  return (
    <>
      <div>
        <label className="label" htmlFor="periodo-inicio">De</label>
        <input id="periodo-inicio" type="date" className="input" value={inicio} max={fim}
               onChange={e => onInicio(e.target.value)} />
      </div>
      <div>
        <label className="label" htmlFor="periodo-fim">Até</label>
        <input id="periodo-fim" type="date" className="input" value={fim} max={brToday()} min={inicio}
               onChange={e => onFim(e.target.value)} />
      </div>
    </>
  )
}

/** Linha "rótulo … valor" da DRE e dos quadros de apuração. */
export function LinhaValor({ label, valor, tone = 'neutral', destaque, indent, negativo }: {
  label: string
  valor: string
  tone?: 'neutral' | 'positivo' | 'negativo' | 'brand'
  /** Separador em cima + peso maior — pros subtotais. */
  destaque?: boolean
  indent?: boolean
  /** Envolve em parênteses, convenção contábil pra valor que subtrai. */
  negativo?: boolean
}) {
  const cor = {
    neutral:  'text-white',
    positivo: 'text-emerald-400',
    negativo: 'text-red-400',
    brand:    'text-brand-300',
  }[tone]

  return (
    <div className={clsx('flex justify-between gap-4', destaque && 'border-t border-surface-700 pt-2')}>
      <span className={clsx(destaque ? 'font-semibold text-gray-200' : 'text-gray-400', indent && 'pl-3')}>
        {label}
      </span>
      <strong className={clsx('font-mono whitespace-nowrap', cor, !destaque && 'font-normal')}>
        {negativo ? `(${valor})` : valor}
      </strong>
    </div>
  )
}
