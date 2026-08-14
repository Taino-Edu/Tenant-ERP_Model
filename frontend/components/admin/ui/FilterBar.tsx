'use client'
import { ReactNode, useState } from 'react'
import { SlidersHorizontal, X } from 'lucide-react'
import clsx from 'clsx'
import { useScrollLock } from '@/hooks/useMediaQuery'

interface FilterBarProps {
  /** Campo de busca — fica SEMPRE visível, inclusive no celular. Buscar é a
   * ação mais frequente numa lista; escondê-la atrás de um botão de filtros
   * transformaria um toque em três. */
  search?: ReactNode
  /** Demais filtros (selects, faixas de data, chips). Inline no desktop,
   * dentro do painel no celular. */
  children: ReactNode
  /** Quantos filtros estão aplicados — vira o contador no botão. Sem ele o
   * usuário não tem como saber que a lista está filtrada quando o painel está
   * fechado, e conclui que "sumiram registros". */
  activeCount?: number
  onClear?: () => void
  className?: string
}

/**
 * Barra de filtros responsiva.
 *
 * Desktop: os filtros ficam em linha, como sempre estiveram.
 * Celular: recolhem num painel inferior aberto pelo botão "Filtros".
 *
 * O motivo é espaço vertical: uma barra com busca + 3 selects + duas datas
 * ocupa ~4 fileiras em 375px, ou seja, a lista de resultados começa abaixo da
 * primeira dobra — o usuário abre a tela e não vê nenhum dado.
 *
 * Os filhos são renderizados UMA vez só (não uma cópia por breakpoint): são
 * campos de formulário controlados, e duplicá-los criaria `id`s repetidos,
 * dois alvos para o mesmo rótulo e foco pulando entre cópias invisíveis. Por
 * isso a troca inline↔painel é feita só com classes no mesmo elemento.
 */
export default function FilterBar({ search, children, activeCount = 0, onClear, className }: FilterBarProps) {
  const [open, setOpen] = useState(false)
  useScrollLock(open)

  return (
    <div className={className}>
      <div className="flex items-center gap-2">
        {search && <div className="min-w-0 flex-1">{search}</div>}
        <button
          onClick={() => setOpen(true)}
          className="btn-secondary shrink-0 sm:hidden"
          aria-expanded={open}
          aria-controls="filtros-painel"
        >
          <SlidersHorizontal className="h-4 w-4" />
          <span>Filtros</span>
          {activeCount > 0 && (
            <span className="ml-0.5 rounded-full bg-brand-500 px-1.5 text-[11px] font-bold leading-5 text-white">
              {activeCount}
            </span>
          )}
        </button>
      </div>

      {/* Backdrop — só existe enquanto o painel está aberto no celular. */}
      {open && (
        <div
          className="fixed inset-0 z-40 bg-black/60 backdrop-blur-sm sm:hidden"
          onClick={() => setOpen(false)}
        />
      )}

      <div
        id="filtros-painel"
        className={clsx(
          // Painel inferior no celular…
          'max-sm:fixed max-sm:inset-x-0 max-sm:bottom-0 max-sm:z-50 max-sm:max-h-[80dvh] max-sm:overflow-y-auto',
          'max-sm:rounded-t-2xl max-sm:border-t max-sm:border-surface-500 max-sm:bg-surface-800 max-sm:p-4 max-sm:pb-safe max-sm:animate-sheet-up',
          'max-sm:flex max-sm:flex-col max-sm:gap-3',
          !open && 'max-sm:hidden',
          // …e linha de filtros comum a partir de sm.
          'sm:mt-3 sm:flex sm:flex-wrap sm:items-center sm:gap-2',
        )}
      >
        <div className="flex items-center justify-between sm:hidden">
          <h3 className="font-semibold text-white">Filtros</h3>
          <button onClick={() => setOpen(false)} aria-label="Fechar filtros" className="touch-target flex items-center justify-center text-gray-500">
            <X className="h-5 w-5" />
          </button>
        </div>

        {children}

        {/* Ações do painel: no celular precisam ser explícitas ("Aplicar" só
            fecha — os filtros já são aplicados na hora). No desktop somem. */}
        <div className="mt-1 flex gap-2 sm:hidden">
          {onClear && activeCount > 0 && (
            <button onClick={onClear} className="btn-secondary flex-1 justify-center">Limpar</button>
          )}
          <button onClick={() => setOpen(false)} className="btn-primary flex-1 justify-center">
            Ver resultados
          </button>
        </div>
      </div>
    </div>
  )
}
