'use client'

import { useEffect } from 'react'
import { usePreferences } from '@/hooks/usePreferences'

export default function VLibrasController() {
  const { prefs } = usePreferences()

  useEffect(() => {
    let el = document.getElementById('vlibras-ctrl') as HTMLStyleElement | null
    if (!el) {
      el = document.createElement('style')
      el.id = 'vlibras-ctrl'
      document.head.appendChild(el)
    }
    // Quando desativado oculta todo o widget. Quando ativado, deixa o VLibras
    // controlar seu próprio layout (sobrescrever posição quebra a inicialização).
    el.textContent = prefs.vlibras.enabled ? '' : '[vw]{display:none!important;}'
  }, [prefs.vlibras.enabled])

  return null
}
