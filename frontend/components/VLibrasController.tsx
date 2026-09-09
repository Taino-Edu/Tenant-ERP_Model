'use client'

import { useCallback, useEffect, useRef, useState } from 'react'
import { Accessibility } from 'lucide-react'
import { usePreferences } from '@/hooks/usePreferences'

type VLibrasWindow = Window & {
  VLibras?: { Widget: new (url: string) => unknown }
}

export default function VLibrasController() {
  const { prefs } = usePreferences()
  const [loaded, setLoaded] = useState(false)
  const loadingRef = useRef<Promise<void> | null>(null)
  const initializedRef = useRef(false)

  const openVlibras = useCallback(() => {
    if (!prefs.vlibras.enabled) return

    if (initializedRef.current) {
      ;(document.querySelector('[vw-access-button]') as HTMLElement | null)?.click()
      return
    }

    if (!loadingRef.current) {
      loadingRef.current = new Promise<void>((resolve, reject) => {
        const initialize = () => {
          const vlibras = (window as VLibrasWindow).VLibras
          if (!vlibras?.Widget) {
            reject(new Error('VLibras indisponível.'))
            return
          }
          new vlibras.Widget('https://vlibras.gov.br/app')
          initializedRef.current = true
          setLoaded(true)
          resolve()
        }

        const existing = document.querySelector<HTMLScriptElement>('script[data-octus-vlibras]')
        if (existing) {
          if ((window as VLibrasWindow).VLibras?.Widget) initialize()
          else {
            existing.addEventListener('load', initialize, { once: true })
            existing.addEventListener('error', () => reject(new Error('Falha ao carregar VLibras.')), { once: true })
          }
          return
        }

        const script = document.createElement('script')
        script.dataset.octusVlibras = 'true'
        script.src = 'https://vlibras.gov.br/app/vlibras-plugin.js'
        script.onload = initialize
        script.onerror = () => reject(new Error('Falha ao carregar VLibras.'))
        document.body.appendChild(script)
      })
    }

    loadingRef.current.then(() => {
      // Dá ao componente do fornecedor um ciclo curto para conectar os eventos
      // internos antes de encaminhar o clique que pediu a tradução.
      window.setTimeout(() => {
        ;(document.querySelector('[vw-access-button]') as HTMLElement | null)?.click()
      }, 250)
    }).catch(() => {
      loadingRef.current = null
      initializedRef.current = false
      setLoaded(false)
    })
  }, [prefs.vlibras.enabled])

  useEffect(() => {
    let el = document.getElementById('vlibras-ctrl') as HTMLStyleElement | null
    if (!el) {
      el = document.createElement('style')
      el.id = 'vlibras-ctrl'
      document.head.appendChild(el)
    }
    // Oculta no mobile (janelas menores que 768px) porque o plugin
    // oficial não é usável e atrapalha a tela inteira.
    const mobileHide = '@media (max-width: 768px) { body:not(.institucional-page) [vw] { display: none !important; } }'
    
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

  useEffect(() => {
    const accessButton = document.querySelector('[vw-access-button]') as HTMLElement | null
    if (!accessButton) return
    const requestLoad = () => {
      if (!initializedRef.current) openVlibras()
    }
    accessButton.addEventListener('click', requestLoad)
    return () => accessButton.removeEventListener('click', requestLoad)
  }, [openVlibras])

  if (!prefs.vlibras.enabled || loaded) return null

  const vertical = prefs.vlibras.corner.startsWith('top') ? 'top-24' : 'bottom-24'
  const horizontal = prefs.vlibras.corner.endsWith('left') ? 'left-4' : 'right-4'

  return (
    <button
      type="button"
      onClick={openVlibras}
      aria-label="Abrir tradução em Libras"
      title="Acessibilidade em Libras"
      className={`js-vlibras-launcher fixed z-[9998] flex h-12 w-12 items-center justify-center rounded-full bg-octus-700 text-white shadow-lg transition hover:bg-octus-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-octus-500 focus-visible:ring-offset-2 ${vertical} ${horizontal}`}
    >
      <Accessibility aria-hidden="true" className="h-6 w-6" />
    </button>
  )
}
