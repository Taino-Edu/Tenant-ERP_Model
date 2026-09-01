export const COOKIE_CONSENT_KEY = 'octus_cookie_consent'
export const COOKIE_CONSENT_VERSION = '2.0'
export const COOKIE_CONSENT_EVENT = 'octus:cookie-consent-changed'
export const OPEN_COOKIE_SETTINGS_EVENT = 'octus:open-cookie-settings'

let volatileConsent: CookieConsentPreferences | null | undefined

export type CookieConsentPreferences = {
  version: string
  necessary: true
  analytics: boolean
  marketing: boolean
  decidedAt: string
}

export function createCookieConsent(
  optional: Pick<CookieConsentPreferences, 'analytics' | 'marketing'>,
  decidedAt = new Date().toISOString(),
): CookieConsentPreferences {
  return {
    version: COOKIE_CONSENT_VERSION,
    necessary: true,
    analytics: optional.analytics,
    marketing: optional.marketing,
    decidedAt,
  }
}

export function parseCookieConsent(value: string | null): CookieConsentPreferences | null {
  if (!value) return null
  try {
    const parsed = JSON.parse(value) as Partial<CookieConsentPreferences>
    if (
      parsed.version !== COOKIE_CONSENT_VERSION ||
      parsed.necessary !== true ||
      typeof parsed.analytics !== 'boolean' ||
      typeof parsed.marketing !== 'boolean' ||
      typeof parsed.decidedAt !== 'string'
    ) return null
    return parsed as CookieConsentPreferences
  } catch {
    // Migração do banner antigo: a escolha precisa ser renovada porque ele não
    // informava categorias nem permitia gerenciar preferências.
    return null
  }
}

export function readCookieConsent(): CookieConsentPreferences | null {
  if (typeof window === 'undefined') return null
  // Marcador de segurança após revogação: não reutilizar um aceite antigo se
  // o navegador tiver recusado a escrita no localStorage. Não é autorização.
  if (new URLSearchParams(window.location.search).get('reset_consent') === '1') return null
  if (volatileConsent !== undefined) return volatileConsent
  try { return parseCookieConsent(window.localStorage.getItem(COOKIE_CONSENT_KEY)) }
  catch { return null }
}

export function saveCookieConsent(preferences: CookieConsentPreferences) {
  const destination = new URL(window.location.href)
  if (destination.searchParams.has('reset_consent')) {
    destination.searchParams.delete('reset_consent')
    window.history.replaceState(window.history.state, '', destination.href)
  }
  try {
    window.localStorage.setItem(COOKIE_CONSENT_KEY, JSON.stringify(preferences))
    volatileConsent = undefined
  } catch {
    // Mantém coerência entre o banner e os emissores nesta navegação.
    volatileConsent = preferences
  }
  window.dispatchEvent(new CustomEvent(COOKIE_CONSENT_EVENT, { detail: preferences }))
}

export function hasCookieConsent(category: 'analytics' | 'marketing') {
  return readCookieConsent()?.[category] === true
}

/** Uma decisão persistida em outra aba substitui o fallback desta aba. */
export function refreshCookieConsentFromStorage() {
  volatileConsent = undefined
}
