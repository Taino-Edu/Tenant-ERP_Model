'use client'
import { ButtonHTMLAttributes, forwardRef } from 'react'
import { Loader2 } from 'lucide-react'
import clsx from 'clsx'

export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'success'
export type ButtonSize = 'sm' | 'md'

const VARIANT_CLASS: Record<ButtonVariant, string> = {
  primary: 'btn-primary',
  secondary: 'btn-secondary',
  danger: 'btn-danger',
  success: 'btn-success',
}

const SIZE_CLASS: Record<ButtonSize, string> = {
  sm: 'text-sm py-1.5 px-3',
  md: '',
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: ButtonSize
  loading?: boolean
  /** Ocupa 100% da largura + centraliza conteúdo — atalho pro padrão
   * `w-full justify-center` usado nos botões de confirmação de modal. */
  full?: boolean
}

/** Formaliza .btn-primary/-secondary/-danger/-success (globals.css) como
 * componente React, com tamanho e estado de loading padronizados — hoje cada
 * tela decide na mão como desabilitar/mostrar spinner durante o submit. */
const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = 'primary', size = 'md', loading, full, disabled, className, children, ...props },
  ref,
) {
  return (
    <button
      ref={ref}
      disabled={disabled || loading}
      className={clsx(VARIANT_CLASS[variant], SIZE_CLASS[size], full && 'w-full justify-center', className)}
      {...props}
    >
      {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
      {children}
    </button>
  )
})

export default Button
