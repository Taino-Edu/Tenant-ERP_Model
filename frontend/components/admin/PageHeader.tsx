'use client'
import { LucideIcon, ChevronLeft } from 'lucide-react'
import Link from 'next/link'
import { ReactNode } from 'react'

interface PageHeaderProps {
  icon: LucideIcon
  title: string
  description?: string
  actions?: ReactNode
  backHref?: string
}

/** Cabeçalho padrão das páginas do admin — título+ícone+descrição+ações.
 * Não tenta cobrir tab-bars internas (usuarios/sobre/reservas já têm as suas).
 *
 * No celular:
 * - o título cai para `text-lg` — `text-2xl` com um ícone de 24px consumia
 *   quase um terço da largura útil e empurrava as ações para uma terceira
 *   linha;
 * - a descrição some (`hidden xs:block`) nas telas mais estreitas: é texto de
 *   apoio, e ali ele custa uma dobra inteira antes do conteúdo real;
 * - as ações viram uma faixa que rola de lado em vez de quebrarem em várias
 *   fileiras empilhadas.
 */
export default function PageHeader({ icon: Icon, title, description, actions, backHref }: PageHeaderProps) {
  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center sm:justify-between">
      <div className="flex min-w-0 items-start gap-2 sm:gap-3">
        {backHref && (
          <Link
            href={backHref}
            aria-label="Voltar"
            className="mt-0.5 shrink-0 text-gray-500 transition-colors hover:text-white sm:mt-1"
          >
            <ChevronLeft className="h-6 w-6 sm:h-5 sm:w-5" />
          </Link>
        )}
        <div className="min-w-0">
          <h1 className="flex items-center gap-2 text-lg font-bold text-white sm:text-2xl">
            <Icon className="h-5 w-5 shrink-0 text-brand-400 sm:h-6 sm:w-6" />
            <span className="truncate">{title}</span>
          </h1>
          {description && <p className="mt-0.5 hidden text-sm text-gray-400 xs:block">{description}</p>}
        </div>
      </div>
      {actions && (
        <div className="chip-row -mx-4 px-4 pb-0.5 sm:mx-0 sm:flex-wrap sm:overflow-visible sm:px-0">
          {actions}
        </div>
      )}
    </div>
  )
}
