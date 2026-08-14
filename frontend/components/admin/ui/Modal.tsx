'use client'
import { ReactNode, useEffect } from 'react'
import { LucideIcon, X } from 'lucide-react'
import clsx from 'clsx'
import { useScrollLock } from '@/hooks/useMediaQuery'

const MAX_WIDTH = {
  sm: 'sm:max-w-sm',
  md: 'sm:max-w-md',
  lg: 'sm:max-w-lg',
  xl: 'sm:max-w-xl',
  '2xl': 'sm:max-w-2xl',
} as const

interface ModalProps {
  onClose: () => void
  maxWidth?: keyof typeof MAX_WIDTH
  /** Fundo do painel — os modais existentes variam entre surface-700 e
   * surface-800 sem critério claro; exposto como prop (em vez de deixar o
   * caller sobrescrever via `className`) pra nunca ter duas classes `bg-*`
   * concorrendo na mesma string. */
  surface?: 'surface-700' | 'surface-800'
  /** Título + ícone do cabeçalho padrão. Omitir os dois pra montar um cabeçalho
   * próprio dentro de `children` (alguns modais precisam de mais controle,
   * ex. busca logo abaixo do título). */
  title?: string
  icon?: LucideIcon
  children: ReactNode
  /** Classe extra no painel (ex. `flex flex-col` pra layout com lista rolável,
   * ou `p-6` quando não há cabeçalho padrão). */
  className?: string
  /** `max-h` + `overflow-y-auto` no painel inteiro. Desliga se o modal já
   * controla o próprio scroll interno (lista com header/footer fixos). */
  scrollable?: boolean
  /** Pra modal que abre por cima de outro modal (ex. escolher conta de
   * crediário durante o fechamento de comanda) — z-index maior e overlay um
   * pouco mais escuro, senão fica indistinguível do modal de baixo. */
  stacked?: boolean
  /** Desliga fechar ao clicar fora — pros formulários de cadastro
   * (usuarios/page.tsx) onde um clique sem querer no fundo não pode derrubar
   * dado já digitado. Só X/Cancelar fecham nesse caso. */
  closeOnBackdrop?: boolean
}

/** Shell padrão de modal do admin — overlay + painel centralizado, clique fora
 * fecha. Extraído do mesmo bloco `fixed inset-0 z-50 ... bg-black/NN` que
 * estava copiado à mão em 13+ componentes (comanda/*, financeiro/*,
 * CobrancaPixModal, AuditLogDetailModal, blocos inline em várias páginas).
 *
 * No celular (< sm) o painel vira BOTTOM SHEET: colado no rodapé, largura
 * cheia, cantos arredondados só no topo e alça de arraste. Motivo prático, não
 * estético — um modal centralizado de 90% da altura empurra o cabeçalho pro
 * meio da tela, deixa faixas de overlay inúteis em cima e embaixo, e joga os
 * botões de ação longe do polegar. O sheet ancora as ações na borda inferior,
 * que é a zona de alcance natural de quem segura o aparelho com uma mão.
 * A responsividade é 100% CSS (não JS), então o SSR já sai com o layout certo
 * e não existe flash de modal-centralizado virando sheet na hidratação. */
export default function Modal({ onClose, maxWidth = 'md', surface = 'surface-800', title, icon: Icon, children, className, scrollable = true, stacked = false, closeOnBackdrop = true }: ModalProps) {
  useScrollLock(true)

  // Esc fecha — o teclado do usuário de desktop espera isso, e no mobile o
  // botão "voltar" de teclados externos/tablets emite o mesmo evento.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title}
      className={clsx(
        'fixed inset-0 flex items-end justify-center backdrop-blur-sm sm:items-center sm:p-4',
        stacked ? 'z-[70] bg-black/75' : 'z-50 bg-black/60',
      )}
      onClick={closeOnBackdrop ? onClose : undefined}
    >
      <div
        className={clsx(
          'w-full border shadow-2xl border-surface-500',
          // Mobile: sheet colado embaixo, cantos só no topo, altura limitada
          // pela viewport REAL (dvh) — com 100vh o Safari corta o rodapé do
          // painel atrás da barra de endereço.
          'max-h-[92dvh] rounded-t-2xl animate-sheet-up',
          // Desktop: volta a ser o card centralizado de sempre.
          'sm:max-h-[85vh] sm:rounded-2xl sm:animate-none',
          surface === 'surface-700' ? 'bg-surface-700' : 'bg-surface-800',
          MAX_WIDTH[maxWidth],
          scrollable && 'overflow-y-auto',
          className,
        )}
        onClick={e => e.stopPropagation()}
      >
        {/* Alça de arraste: só no mobile, e só como affordance visual — informa
            "isto desliza/fecha por baixo" antes do usuário tentar. */}
        <div className="sticky top-0 z-10 flex justify-center pt-2 pb-1 sm:hidden" style={{ backgroundColor: 'inherit' }}>
          <span className="h-1 w-10 rounded-full bg-surface-400" />
        </div>

        {(title || Icon) && (
          <div className="sticky top-0 z-10 flex shrink-0 items-center justify-between border-b border-surface-500 p-4 max-sm:top-6" style={{ backgroundColor: 'inherit' }}>
            <h3 className="flex min-w-0 items-center gap-2 font-semibold text-white">
              {Icon && <Icon className="h-4 w-4 shrink-0 text-brand-400" />}
              <span className="truncate">{title}</span>
            </h3>
            <button
              onClick={onClose}
              aria-label="Fechar"
              className="touch-target -mr-2 flex items-center justify-center rounded-lg px-2 text-gray-500 hover:text-gray-300"
            >
              <X className="h-5 w-5" />
            </button>
          </div>
        )}
        {children}
        {/* Respiro da barra de gestos do aparelho — o último botão do modal não
            pode encostar na borda inferior da tela. */}
        <div className="pb-safe sm:hidden" />
      </div>
    </div>
  )
}
