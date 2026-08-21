'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import { useRouter } from 'next/navigation'
import { Keyboard, X } from 'lucide-react'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import { useAdminPermissions } from '@/hooks/useAdminPermissions'
import {
  ADMIN_KEYBOARD_SHORTCUTS,
  isEditableShortcutTarget,
} from '@/lib/adminKeyboardShortcuts'

const HELP_SHORTCUTS = [
  { key: '?', label: '?', description: 'Mostrar / esconder esta ajuda', category: 'Ajuda' },
  { key: 'Escape', label: 'Esc', description: 'Fechar modal ou painel aberto', category: 'Ajuda' },
]

export default function KeyboardShortcutsOverlay() {
  const router = useRouter()
  const { site } = useSiteConfig()
  const [open, setOpen] = useState(false)

  const { can } = useAdminPermissions()

  // `can` entra nas dependências e não é decoração: sem ele o filtro rodaria
  // uma vez só, com o cookie ainda não lido, e a lista de atalhos ficaria vazia
  // de tudo que exige permissão — inclusive as teclas, que saem daqui.
  const shortcuts = useMemo(() => {
    return ADMIN_KEYBOARD_SHORTCUTS.filter(shortcut => {
      if (shortcut.module && !site.enabledModules?.includes(shortcut.module)) return false
      if (shortcut.permission && !can(shortcut.permission)) return false
      return true
    })
  }, [site.enabledModules, can])

  const handleKey = useCallback((event: KeyboardEvent) => {
    if (event.key === 'Escape') {
      setOpen(false)
      return
    }

    // O site não sequestra teclas enquanto o usuário escreve, seleciona dados
    // ou interage dentro de uma área que suspende explicitamente os atalhos.
    if (
      event.defaultPrevented
      || event.isComposing
      || event.repeat
      || event.ctrlKey
      || event.metaKey
      || event.altKey
      || isEditableShortcutTarget(event)
    ) return

    if (event.key === '?') {
      event.preventDefault()
      setOpen(value => !value)
      return
    }

    const shortcut = shortcuts.find(item => item.key === event.key.toLowerCase())
    if (!shortcut) return

    event.preventDefault()
    if (shortcut.eventName) window.dispatchEvent(new CustomEvent(shortcut.eventName))
    if (shortcut.route) router.push(shortcut.route)
  }, [router, shortcuts])

  useEffect(() => {
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [handleKey])

  if (!open) return null

  const visibleShortcuts = [...shortcuts, ...HELP_SHORTCUTS]
  const categories = [...new Set(visibleShortcuts.map(shortcut => shortcut.category))]

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm"
      onClick={event => { if (event.target === event.currentTarget) setOpen(false) }}
    >
      <div className="w-full max-w-2xl overflow-hidden rounded-2xl border border-surface-500 bg-surface-800 shadow-2xl">
        <div className="flex items-center justify-between border-b border-surface-600 px-5 py-4">
          <div className="flex items-center gap-2">
            <Keyboard className="h-4 w-4 text-brand-400" />
            <h3 className="text-sm font-semibold text-white">Atalhos de teclado</h3>
          </div>
          <button onClick={() => setOpen(false)} className="text-gray-500 transition-colors hover:text-gray-300" aria-label="Fechar atalhos">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="max-h-[72vh] space-y-5 overflow-y-auto p-5">
          {categories.map(category => (
            <section key={category}>
              <p className="mb-2 text-[10px] font-bold uppercase tracking-wider text-gray-500">{category}</p>
              <div className="grid gap-1 sm:grid-cols-2">
                {visibleShortcuts.filter(shortcut => shortcut.category === category).map(shortcut => (
                  <div
                    key={shortcut.key}
                    className="flex items-center justify-between gap-4 rounded-lg px-2 py-1.5 transition-colors hover:bg-surface-700"
                  >
                    <span className="text-sm text-gray-300">{shortcut.description}</span>
                    <kbd className="min-w-[28px] shrink-0 rounded-md border border-surface-500 bg-surface-700 px-2 py-0.5 text-center font-mono text-[11px] font-bold text-gray-200">
                      {shortcut.label}
                    </kbd>
                  </div>
                ))}
              </div>
            </section>
          ))}

          <p className="border-t border-surface-700 pt-3 text-center text-[11px] leading-relaxed text-gray-500">
            Os atalhos ficam pausados enquanto você digita ou interage com o Assistente de IA.
          </p>
        </div>
      </div>
    </div>
  )
}
