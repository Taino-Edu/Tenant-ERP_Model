'use client'
import { Fragment, ReactNode } from 'react'
import { ChevronRight } from 'lucide-react'
import clsx from 'clsx'

/** Papel da coluna no card do celular.
 *
 * Uma tabela de 8 colunas não cabe em 360px — nem com scroll lateral, porque
 * ninguém rola de lado pra ler cada linha. O caminho é reorganizar a MESMA
 * informação numa hierarquia vertical: o que identifica a linha vira título, o
 * número que decide a ação vira destaque, o resto vira rótulo/valor abaixo. É
 * o que essa prop declara, coluna a coluna.
 *
 * - `title`    → identificador da linha (nome do produto, do cliente)
 * - `trailing` → valor de destaque, alinhado à direita do título (preço, total)
 * - `meta`     → chips na linha de apoio (categoria, status, data)
 * - `field`    → par rótulo/valor no corpo do card (o padrão)
 * - `hidden`   → só faz sentido na tabela (ex. índice "#", coluna de ação
 *                duplicada), fica fora do card
 */
export type MobileRole = 'title' | 'trailing' | 'meta' | 'field' | 'hidden'

export interface Column<T> {
  /** Identificador estável da coluna (usado como key do React). */
  key: string
  /** Cabeçalho na tabela e rótulo no card (quando `mobile: 'field'`). */
  header: string
  cell: (row: T, index: number) => ReactNode
  align?: 'left' | 'right' | 'center'
  mobile?: MobileRole
  /** Classe extra na `<td>`. */
  className?: string
  /** Classe extra na `<th>`. */
  headerClassName?: string
}

interface DataTableProps<T> {
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T, index: number) => string
  /** Linha inteira clicável — vira `<button>` no card do mobile (alvo grande,
   * do jeito que se espera de uma lista tocável) e mantém a `<tr>` clicável no
   * desktop. */
  onRowClick?: (row: T) => void
  /** Ações por linha (botões). No card ficam num rodapé próprio, largura
   * cheia — não espremidos num canto de 24px. */
  rowActions?: (row: T) => ReactNode
  /** Destaque por linha (ex.: a competência atual em Fechamento). Aplicado
   * tanto na `<tr>` quanto no card — sinal de estado não pode existir só num
   * dos dois layouts. */
  rowClassName?: (row: T) => string | undefined
  /** Estado vazio. Renderizado uma vez, sem tabela em volta. */
  empty?: ReactNode
  /** Rodapé da tabela (totais). No mobile vira um card destacado no fim. */
  footer?: ReactNode
  className?: string
  /** Largura mínima da tabela no desktop — só afeta a versão `<table>`. */
  minWidth?: string
  /** Envolve APENAS a tabela do desktop num `.card`.
   *
   * Existe porque o padrão `<div class="card"><DataTable/></div>` produz, no
   * celular, card dentro de card: duas bordas, dois fundos iguais e ~40px de
   * largura desperdiçados numa tela de 375px (medido: 301px de card útil em vez
   * de 343px). No celular quem já é o contêiner são os próprios cards da lista.
   * Use isto quando o `.card` em volta era só moldura da tabela; mantenha o
   * contêiner externo quando ele tem cabeçalho/título próprio, que aí é um
   * agrupamento com significado e não moldura redundante. */
  desktopCard?: boolean
}

const ALIGN = { left: 'text-left', right: 'text-right', center: 'text-center' } as const

/**
 * Tabela responsiva padrão do admin.
 *
 * Desktop (>= sm): `<table>` semântica de sempre.
 * Mobile  (<  sm): a mesma lista renderizada como cards empilhados.
 *
 * As duas saem do MESMO array de colunas, então não existe o risco clássico de
 * manter duas marcações paralelas e uma delas ficar defasada quando um campo
 * novo entra (era o que já começava a acontecer entre a tabela e os cards
 * escritos à mão em financeiro/ e lgpd/).
 *
 * A troca é por CSS (`hidden sm:block` / `sm:hidden`), não por JS de media
 * query: o HTML do servidor já vem com as duas versões e o navegador escolhe,
 * então não há flash de tabela no primeiro paint do celular.
 */
export default function DataTable<T>({
  columns, rows, rowKey, onRowClick, rowActions, rowClassName, empty, footer, className, minWidth, desktopCard,
}: DataTableProps<T>) {
  if (rows.length === 0 && empty) return <>{empty}</>

  const titleCols    = columns.filter(c => c.mobile === 'title')
  const trailingCols = columns.filter(c => c.mobile === 'trailing')
  const metaCols     = columns.filter(c => c.mobile === 'meta')
  const fieldCols    = columns.filter(c => c.mobile === 'field' || c.mobile === undefined)

  return (
    <div className={className}>
      {/* ── Desktop: tabela ─────────────────────────────────────────────── */}
      <div className={clsx('table-scroll hidden sm:block', desktopCard && 'card !p-0 overflow-hidden')}>
        <table className="w-full text-sm" style={minWidth ? { minWidth } : undefined}>
          <thead className="bg-surface-800">
            <tr>
              {columns.map(c => (
                <th
                  key={c.key}
                  scope="col"
                  className={clsx(
                    'whitespace-nowrap px-4 py-2.5 text-xs font-semibold uppercase tracking-wider text-gray-500',
                    ALIGN[c.align ?? 'left'],
                    c.headerClassName,
                  )}
                >
                  {c.header}
                </th>
              ))}
              {rowActions && <th scope="col" className="px-4 py-2.5" />}
            </tr>
          </thead>
          <tbody className="divide-y divide-surface-500">
            {rows.map((row, i) => (
              <tr
                key={rowKey(row, i)}
                onClick={onRowClick ? () => onRowClick(row) : undefined}
                className={clsx(
                  'transition-colors hover:bg-surface-500/20',
                  onRowClick && 'cursor-pointer',
                  rowClassName?.(row),
                )}
              >
                {columns.map(c => (
                  <td key={c.key} className={clsx('px-4 py-2.5', ALIGN[c.align ?? 'left'], c.className)}>
                    {c.cell(row, i)}
                  </td>
                ))}
                {rowActions && (
                  <td className="px-4 py-2.5 text-right" onClick={e => e.stopPropagation()}>
                    <div className="flex justify-end gap-1">{rowActions(row)}</div>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
        {footer && <div className="border-t border-surface-500 px-4 py-3">{footer}</div>}
      </div>

      {/* ── Mobile: cards ───────────────────────────────────────────────── */}
      <ul className="space-y-2 sm:hidden">
        {rows.map((row, i) => {
          const body = (
            <>
              {(titleCols.length > 0 || trailingCols.length > 0) && (
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 flex-1 font-semibold text-white">
                    {titleCols.map(c => <Fragment key={c.key}>{c.cell(row, i)}</Fragment>)}
                  </div>
                  <div className="shrink-0 text-right font-mono font-bold">
                    {trailingCols.map(c => <Fragment key={c.key}>{c.cell(row, i)}</Fragment>)}
                  </div>
                  {onRowClick && <ChevronRight className="mt-0.5 h-4 w-4 shrink-0 text-gray-600" />}
                </div>
              )}

              {/* Cada chip precisa ser um ELEMENTO, não um Fragment: quando a
                  `cell` devolve string crua ela vira um nó de texto, e `gap` de
                  flexbox não se aplica a nó de texto — dois chips seguidos
                  saíam grudados ("Mensalidade10/08/2026"). O <span> os torna
                  itens flex de verdade; o separador "·" entra entre eles pra
                  leitura não depender só do espaço. */}
              {metaCols.length > 0 && (
                <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-gray-400">
                  {metaCols.map((c, ci) => (
                    <span key={c.key} className="flex items-center gap-2">
                      {ci > 0 && <span aria-hidden className="text-gray-600">·</span>}
                      {c.cell(row, i)}
                    </span>
                  ))}
                </div>
              )}

              {fieldCols.length > 0 && (
                <dl className="space-y-1 border-t border-surface-600 pt-2">
                  {fieldCols.map(c => (
                    <div key={c.key} className="field-row">
                      <dt>{c.header}</dt>
                      <dd>{c.cell(row, i)}</dd>
                    </div>
                  ))}
                </dl>
              )}
            </>
          )

          return (
            <li key={rowKey(row, i)} className={clsx('card space-y-2 !p-3', rowClassName?.(row))}>
              {onRowClick ? (
                <button onClick={() => onRowClick(row)} className="w-full space-y-2 text-left">
                  {body}
                </button>
              ) : body}
              {rowActions && (
                <div className="flex flex-wrap gap-2 border-t border-surface-600 pt-2">
                  {rowActions(row)}
                </div>
              )}
            </li>
          )
        })}
        {footer && <li className="card !p-3">{footer}</li>}
      </ul>
    </div>
  )
}
