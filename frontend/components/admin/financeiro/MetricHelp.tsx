'use client'

import { useEffect, useId, useState } from 'react'
import { CircleHelp, X } from 'lucide-react'

interface MetricHelpProps {
  title: string
  children: React.ReactNode
}

export function MetricHelp({ title, children }: MetricHelpProps) {
  const [open, setOpen] = useState(false)
  const titleId = useId()

  useEffect(() => {
    if (!open) return
    const close = (event: KeyboardEvent) => event.key === 'Escape' && setOpen(false)
    window.addEventListener('keydown', close)
    return () => window.removeEventListener('keydown', close)
  }, [open])

  return (
    <>
      <button
        type="button"
        aria-label={`Entenda: ${title}`}
        title={`Entenda: ${title}`}
        onClick={() => setOpen(true)}
        className="inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-gray-500 transition-colors hover:bg-surface-600 hover:text-brand-300"
      >
        <CircleHelp className="h-4 w-4" />
      </button>

      {open && (
        <div
          className="fixed inset-0 z-[80] flex items-end justify-center bg-black/65 p-0 sm:items-center sm:p-4"
          onMouseDown={event => event.target === event.currentTarget && setOpen(false)}
        >
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby={titleId}
            className="w-full max-w-lg rounded-t-lg border border-surface-500 bg-surface-800 p-5 shadow-2xl sm:rounded-lg"
          >
            <div className="mb-3 flex items-start justify-between gap-3">
              <h2 id={titleId} className="text-base font-bold text-white">{title}</h2>
              <button
                type="button"
                aria-label="Fechar explicação"
                title="Fechar"
                onClick={() => setOpen(false)}
                className="inline-flex h-8 w-8 items-center justify-center rounded-md text-gray-400 hover:bg-surface-600 hover:text-white"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="space-y-2 text-sm leading-6 text-gray-300">{children}</div>
          </section>
        </div>
      )}
    </>
  )
}
