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
    // Oculta no mobile (janelas menores que 768px) porque o plugin
    // oficial não é usável e atrapalha a tela inteira.
    const mobileHide = '@media (max-width: 768px) { [vw] { display: none !important; } }'
    
    let css = ''
    if (!prefs.vlibras.enabled) {
      css = '[vw] { display: none !important; }'
    } else {
      // Força a posição baseada na configuração (o padrão oficial do plugin é centro-direita)
      if (prefs.vlibras.corner === 'bottom-left') {
        css = '[vw] { left: 0 !important; right: auto !important; } [vw] .vw-plugin-wrapper { left: 0 !important; right: auto !important; }'
      } else if (prefs.vlibras.corner === 'top-right') {
        css = '[vw] { top: 10vh !important; bottom: auto !important; }'
      } else if (prefs.vlibras.corner === 'top-left') {
        css = '[vw] { left: 0 !important; right: auto !important; top: 10vh !important; bottom: auto !important; } [vw] .vw-plugin-wrapper { left: 0 !important; right: auto !important; }'
      }
      // 'bottom-right' não precisa de CSS extra pois é a âncora nativa dele.
    }
    
    el.textContent = css + ' ' + mobileHide
  }, [prefs.vlibras.enabled, prefs.vlibras.corner])

  return null
}
