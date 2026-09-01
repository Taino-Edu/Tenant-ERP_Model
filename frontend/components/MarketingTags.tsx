'use client'

import Script from 'next/script'
import { usePathname } from 'next/navigation'
import { useEffect, useRef, useState } from 'react'
import { COOKIE_CONSENT_EVENT, COOKIE_CONSENT_KEY, type CookieConsentPreferences, readCookieConsent, refreshCookieConsentFromStorage } from '@/lib/cookieConsent'
import { applyMarketingConsent, consentWasReduced, hasOptionalConsent, isMarketingPage, prepareMarketingTags, trackMarketingEvent } from '@/lib/marketing'

const GTM_ID = process.env.NEXT_PUBLIC_GTM_ID || ''
const META_PIXEL_ID = process.env.NEXT_PUBLIC_META_PIXEL_ID || ''

export default function MarketingTags() {
  const pathname = usePathname()
  const lastPage = useRef<string | null>(null)
  const [tags, setTags] = useState({ google: false, meta: false })

  useEffect(() => {
    let previous: CookieConsentPreferences | null = readCookieConsent()
    const started = () => window.octusGoogleStarted || window.octusMetaStarted
    const synchronize = () => {
      const next = readCookieConsent()
      if (!isMarketingPage(window.location.hostname, window.location.pathname)) {
        setTags({ google: false, meta: false })
        if (started()) {
          applyMarketingConsent(null)
          window.location.reload()
        }
        return
      }
      if (started() && consentWasReduced(previous, next)) {
        applyMarketingConsent(next)
        // Desmontar <Script> não desfaz timers/listeners do fornecedor.
        // Recarrega sem tags, inclusive se o armazenamento estiver bloqueado.
        setTags({ google: false, meta: false })
        const destination = new URL(window.location.href)
        try {
          if (window.localStorage.getItem(COOKIE_CONSENT_KEY) !== JSON.stringify(next)) destination.searchParams.set('reset_consent', '1')
        } catch {
          destination.searchParams.set('reset_consent', '1')
        }
        window.location.replace(destination.href)
        return
      }
      const metaWasJustGranted = !previous?.marketing && next?.marketing
      previous = next
      const enabled = prepareMarketingTags(next, GTM_ID, META_PIXEL_ID)
      setTags(enabled)
      if (!hasOptionalConsent(next)) lastPage.current = null
      else if (lastPage.current !== pathname) {
        trackMarketingEvent('octus_page_view', { page_path: pathname })
        lastPage.current = pathname
      } else if (metaWasJustGranted && enabled.meta) {
        window.fbq?.('track', 'PageView')
      }
    }
    const onStorage = (event: StorageEvent) => {
      if (event.key === COOKIE_CONSENT_KEY || event.key === null) {
        refreshCookieConsentFromStorage()
        synchronize()
      }
    }
    // Links para fora do comercial abrem outro documento antes de o roteador
    // SPA levar scripts de marketing para uma área privada.
    const onClick = (event: MouseEvent) => {
      if (!started() || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return
      const anchor = event.target instanceof Element ? event.target.closest('a') : null
      if (!anchor || anchor.hasAttribute('download') || (anchor.target && anchor.target !== '_self')) return
      const destination = new URL(anchor.href, window.location.href)
      if (destination.origin === window.location.origin && !isMarketingPage(destination.hostname, destination.pathname)) {
        event.preventDefault()
        event.stopPropagation()
        window.location.assign(destination.href)
      }
    }
    synchronize()
    window.addEventListener(COOKIE_CONSENT_EVENT, synchronize)
    window.addEventListener('storage', onStorage)
    document.addEventListener('click', onClick, true)
    return () => {
      window.removeEventListener(COOKIE_CONSENT_EVENT, synchronize)
      window.removeEventListener('storage', onStorage)
      document.removeEventListener('click', onClick, true)
    }
  }, [pathname])

  return <>
    {tags.google ? <Script id="octus-gtm" src={`https://www.googletagmanager.com/gtm.js?id=${encodeURIComponent(GTM_ID)}`} strategy="afterInteractive" /> : null}
    {tags.meta ? <Script id="octus-meta" src="https://connect.facebook.net/en_US/fbevents.js" strategy="afterInteractive" /> : null}
  </>
}
