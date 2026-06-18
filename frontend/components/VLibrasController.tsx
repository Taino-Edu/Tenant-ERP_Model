'use client'

import { useEffect } from 'react'
import { usePreferences } from '@/hooks/usePreferences'

const CORNER_CSS: Record<string, string> = {
  'bottom-right': 'bottom:10px!important;right:10px!important;top:auto!important;left:auto!important;',
  'bottom-left':  'bottom:10px!important;left:10px!important;top:auto!important;right:auto!important;',
  'top-right':    'top:64px!important;right:10px!important;bottom:auto!important;left:auto!important;',
  'top-left':     'top:64px!important;left:10px!important;bottom:auto!important;right:auto!important;',
}

export default function VLibrasController() {
  const { prefs } = usePreferences()

  useEffect(() => {
    let el = document.getElementById('vlibras-ctrl') as HTMLStyleElement | null
    if (!el) {
      el = document.createElement('style')
      el.id = 'vlibras-ctrl'
      document.head.appendChild(el)
    }

    if (!prefs.vlibras.enabled) {
      el.textContent = '[vw]{display:none!important;}'
    } else {
      const pos = CORNER_CSS[prefs.vlibras.corner] ?? CORNER_CSS['bottom-right']
      el.textContent = `[vw]{position:fixed!important;${pos}z-index:9990!important;}`
    }
  }, [prefs.vlibras.enabled, prefs.vlibras.corner])

  return null
}
