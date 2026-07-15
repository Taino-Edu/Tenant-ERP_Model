'use client'

import { useEffect, useRef } from 'react'
import { usePathname } from 'next/navigation'
import { enqueueUsageEvent, flushUsageEvents } from '@/lib/usageTracking'

const FLUSH_INTERVAL_MS = 30_000

/** Componente invisível — mede quanto tempo cada tela do admin fica aberta e
 * manda pro backend em lote. Monta uma vez em admin/layout.tsx, cobre toda
 * navegação client-side (App Router não dispara reload de página). */
export default function UsageTracker() {
  const pathname = usePathname()
  const enteredAtRef = useRef<number>(Date.now())
  const currentPathRef = useRef<string | null>(null)

  useEffect(() => {
    const now = Date.now()
    const prevPath = currentPathRef.current
    if (prevPath && prevPath !== pathname) {
      enqueueUsageEvent(prevPath, now - enteredAtRef.current)
    }
    currentPathRef.current = pathname
    enteredAtRef.current = now
  }, [pathname])

  useEffect(() => {
    const interval = setInterval(flushUsageEvents, FLUSH_INTERVAL_MS)

    function onVisibilityChange() {
      if (document.hidden && currentPathRef.current) {
        enqueueUsageEvent(currentPathRef.current, Date.now() - enteredAtRef.current)
        enteredAtRef.current = Date.now()
      }
      flushUsageEvents()
    }

    document.addEventListener('visibilitychange', onVisibilityChange)
    window.addEventListener('pagehide', onVisibilityChange)

    return () => {
      clearInterval(interval)
      document.removeEventListener('visibilitychange', onVisibilityChange)
      window.removeEventListener('pagehide', onVisibilityChange)
    }
  }, [])

  return null
}
