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
 * Não tenta cobrir tab-bars internas (usuarios/sobre/reservas já têm as suas). */
export default function PageHeader({ icon: Icon, title, description, actions, backHref }: PageHeaderProps) {
  return (
    <div className="flex items-center justify-between flex-wrap gap-3">
      <div className="flex items-start gap-3">
        {backHref && (
          <Link
            href={backHref}
            aria-label="Voltar"
            className="mt-1 text-gray-500 hover:text-white transition-colors shrink-0"
          >
            <ChevronLeft className="w-5 h-5" />
          </Link>
        )}
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <Icon className="w-6 h-6 text-brand-400" />
            {title}
          </h1>
          {description && <p className="text-gray-400 text-sm mt-0.5">{description}</p>}
        </div>
      </div>
      {actions && <div className="flex gap-2 flex-wrap">{actions}</div>}
    </div>
  )
}
