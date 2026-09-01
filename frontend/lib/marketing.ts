'use client'

import type { CookieConsentPreferences } from './cookieConsent'
import { readCookieConsent } from './cookieConsent'
import { isPlatformHost } from './seo'

export type MarketingEventName =
  | 'octus_page_view'
  | 'view_pricing'
  | 'select_plan'
  | 'whatsapp_click'
  | 'lead_submit'
  | 'sign_up'
  | 'trial_started'
  | 'subscription_paid'

export type MarketingEventParameters = Record<string, string | number | boolean | undefined>

declare global {
  interface Window {
    dataLayer?: unknown[]
    octusConsentInitialized?: boolean
    octusGoogleStarted?: boolean
    octusMetaStarted?: boolean
    fbq?: ((command: string, event: string, parameters?: MarketingEventParameters) => void) & {
      callMethod?: (...args: unknown[]) => void
      queue?: unknown[]
      loaded?: boolean
      version?: string
    }
    _fbq?: Window['fbq']
  }
}

/** Somente páginas comerciais da plataforma; nunca lojas, login ou painel. */
export function isMarketingPage(hostname: string, pathname: string) {
  const publicHost = isPlatformHost(hostname) || hostname === 'localhost' || hostname === '127.0.0.1'
  return publicHost && ['/', '/institucional', '/parceiros'].includes(pathname.replace(/\/$/, '') || '/')
}

export function consentWasReduced(previous: CookieConsentPreferences | null, next: CookieConsentPreferences | null) {
  return Boolean((previous?.analytics && !next?.analytics) || (previous?.marketing && !next?.marketing))
}

function consentCommand(_command: string, _action: string, _state: ReturnType<typeof googleConsentState>) {
  // O protocolo gtag usa IArguments, não um evento-objeto nem um array simples.
  window.dataLayer = window.dataLayer || []
  window.dataLayer.push(arguments)
}

export function googleConsentState(preferences: CookieConsentPreferences | null) {
  return {
    analytics_storage: preferences?.analytics ? 'granted' : 'denied',
    ad_storage: preferences?.marketing ? 'granted' : 'denied',
    ad_user_data: preferences?.marketing ? 'granted' : 'denied',
    ad_personalization: preferences?.marketing ? 'granted' : 'denied',
    functionality_storage: 'granted',
    security_storage: 'granted',
  } as const
}

export function hasOptionalConsent(preferences: CookieConsentPreferences | null) {
  return preferences?.analytics === true || preferences?.marketing === true
}

/** Atualiza Consent Mode v2 e o Pixel sem enviar evento de navegação. */
export function applyMarketingConsent(preferences: CookieConsentPreferences | null) {
  if (typeof window === 'undefined') return

  if (!window.octusConsentInitialized) {
    consentCommand('consent', 'default', googleConsentState(null))
    window.octusConsentInitialized = true
  }
  consentCommand('consent', 'update', googleConsentState(preferences))

  if (window.fbq) {
    window.fbq('consent', preferences?.marketing ? 'grant' : 'revoke')
  }
}

/** Prepara as filas ANTES de renderizar os scripts externos. Idempotente no StrictMode. */
export function prepareMarketingTags(preferences: CookieConsentPreferences | null, gtmId: string, metaId: string) {
  if (typeof window === 'undefined' || !isMarketingPage(window.location.hostname, window.location.pathname)) {
    return { google: false, meta: false }
  }
  applyMarketingConsent(preferences)
  const google = /^GTM-[A-Z0-9]+$/i.test(gtmId) && hasOptionalConsent(preferences)
  const meta = /^\d{5,20}$/.test(metaId) && preferences?.marketing === true
  if (google && !window.octusGoogleStarted) {
    window.dataLayer!.push({ 'gtm.start': Date.now(), event: 'gtm.js' })
    window.octusGoogleStarted = true
  }
  if (meta && !window.octusMetaStarted) {
    const fbq: NonNullable<Window['fbq']> = function (...args) {
      if (fbq.callMethod) fbq.callMethod(...args)
      else fbq.queue!.push(args)
    }
    fbq.queue = []
    fbq.loaded = true
    fbq.version = '2.0'
    window.fbq = window.fbq || fbq
    window._fbq = window._fbq || window.fbq
    window.fbq('consent', 'grant')
    window.fbq('init', metaId)
    window.octusMetaStarted = true
  }
  return { google, meta }
}

/**
 * Emite apenas rótulos controlados pelo código. Se nenhuma categoria opcional foi
 * aceita, o evento nem entra na fila: aceitar depois não pode enviar ações
 * realizadas enquanto a pessoa havia recusado o rastreamento.
 */
export function trackMarketingEvent(name: MarketingEventName, parameters: MarketingEventParameters = {}) {
  if (typeof window === 'undefined') return false
  if (!isMarketingPage(window.location.hostname, window.location.pathname)) return false
  const preferences = readCookieConsent()
  if (!hasOptionalConsent(preferences)) return false

  // Nunca repassar campos de formulário, URLs completas ou identificadores.
  const allowed: Record<string, readonly string[]> = {
    page_path: ['/', '/institucional', '/parceiros'],
    form: ['institucional', 'parceiros'],
    lead_kind: ['trial', 'referral_partner'],
    placement: ['founders', 'contact'],
    plan: ['Lagoa', 'Rio', 'Mar'],
  }
  const cleanParameters = Object.fromEntries(Object.entries(parameters).filter(
    ([key, value]) => typeof value === 'string' && allowed[key]?.includes(value),
  ))

  window.dataLayer = window.dataLayer || []
  window.dataLayer.push({ event: name, ...cleanParameters })

  if (preferences?.marketing && window.fbq) {
    const standardEvents: Partial<Record<MarketingEventName, string>> = {
      octus_page_view: 'PageView',
      view_pricing: 'ViewContent',
      lead_submit: 'Lead',
      sign_up: 'CompleteRegistration',
      trial_started: 'StartTrial',
      subscription_paid: 'Purchase',
    }
    const standardEvent = standardEvents[name]
    if (standardEvent) window.fbq('track', standardEvent, cleanParameters)
    else window.fbq('trackCustom', name, cleanParameters)
  }

  return true
}
