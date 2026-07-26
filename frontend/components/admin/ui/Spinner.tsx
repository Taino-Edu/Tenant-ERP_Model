'use client'
import { Loader2 } from 'lucide-react'
import clsx from 'clsx'

const SIZE_CLASS = { sm: 'w-4 h-4', md: 'w-5 h-5', lg: 'w-8 h-8' } as const

interface SpinnerProps {
  size?: keyof typeof SIZE_CLASS
  /** Centraliza num bloco com padding vertical — pro caso de uso mais comum
   * ("carregando..." ocupando uma seção inteira). Sem isso, retorna só o ícone,
   * pra compor dentro de um botão por exemplo. */
  block?: boolean
  className?: string
}

/** `<Loader2 className="animate-spin" />` — mesmo ícone/animação repetidos
 * cruamente em ~20 lugares do admin, sem tamanho/contêiner padronizado. */
export default function Spinner({ size = 'md', block, className }: SpinnerProps) {
  const icon = <Loader2 className={clsx(SIZE_CLASS[size], 'animate-spin text-brand-400', className)} />
  if (!block) return icon
  return <div className="flex justify-center py-8">{icon}</div>
}
