'use client'
import { ReactNode } from 'react'
import { LucideIcon, X } from 'lucide-react'
import clsx from 'clsx'

const MAX_WIDTH = {
  sm: 'max-w-sm',
  md: 'max-w-md',
  lg: 'max-w-lg',
  xl: 'max-w-xl',
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
}

/** Shell padrão de modal do admin — overlay + painel centralizado, clique fora
 * fecha. Extraído do mesmo bloco `fixed inset-0 z-50 ... bg-black/NN` que
 * estava copiado à mão em 13+ componentes (comanda/*, financeiro/*,
 * CobrancaPixModal, AuditLogDetailModal, blocos inline em várias páginas). */
export default function Modal({ onClose, maxWidth = 'md', surface = 'surface-800', title, icon: Icon, children, className, scrollable = true, stacked = false }: ModalProps) {
  return (
    <div
      className={clsx(
        'fixed inset-0 flex items-center justify-center p-4 backdrop-blur-sm',
        stacked ? 'z-[70] bg-black/75' : 'z-50 bg-black/60',
      )}
      onClick={onClose}
    >
      <div
        className={clsx(
          'border border-surface-500 rounded-2xl w-full shadow-2xl',
          surface === 'surface-700' ? 'bg-surface-700' : 'bg-surface-800',
          MAX_WIDTH[maxWidth],
          scrollable && 'max-h-[85vh] overflow-y-auto',
          className,
        )}
        onClick={e => e.stopPropagation()}
      >
        {(title || Icon) && (
          <div className="flex items-center justify-between p-4 border-b border-surface-500 shrink-0">
            <h3 className="font-semibold text-white flex items-center gap-2">
              {Icon && <Icon className="w-4 h-4 text-brand-400" />}
              {title}
            </h3>
            <button onClick={onClose} className="text-gray-500 hover:text-gray-300">
              <X className="w-5 h-5" />
            </button>
          </div>
        )}
        {children}
      </div>
    </div>
  )
}
