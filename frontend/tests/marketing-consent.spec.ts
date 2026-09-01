import { expect, test } from '@playwright/test'
import { COOKIE_CONSENT_KEY, createCookieConsent, saveCookieConsent } from '../lib/cookieConsent'
import { applyMarketingConsent, consentWasReduced, googleConsentState, hasOptionalConsent, isMarketingPage, prepareMarketingTags, trackMarketingEvent } from '../lib/marketing'

test.beforeEach(() => {
  const storage = new Map<string, string>()
  Object.defineProperty(globalThis, 'window', { configurable: true, value: {
    location: new URL('https://3esysten.com.br/institucional'),
    localStorage: {
      getItem: (key: string) => storage.get(key) ?? null,
      setItem: (key: string, value: string) => storage.set(key, value),
    },
    dispatchEvent: () => true,
  } })
})

test.afterEach(() => { Reflect.deleteProperty(globalThis, 'window') })

const accepted = () => createCookieConsent({ analytics: true, marketing: true })
const save = (value = accepted()) => window.localStorage.setItem(COOKIE_CONSENT_KEY, JSON.stringify(value))

test('private routes, custom domains and tenant storefronts never initialize tags', () => {
  for (const [host, path] of [
    ['exemplosvisual.localhost', '/'], ['cliente.3esysten.com.br', '/institucional'],
    ['loja.example', '/'], ['3esysten.com.br', '/admin/crediario'],
    ['3esysten.com.br', '/login'], ['3esysten.com.br', '/cliente'],
  ]) {
    expect(isMarketingPage(host, path)).toBe(false)
    Object.assign(window.location, { hostname: host, pathname: path })
    expect(prepareMarketingTags(accepted(), 'GTM-TEST123', '1234567890')).toEqual({ google: false, meta: false })
    expect(trackMarketingEvent('lead_submit')).toBe(false)
    expect(window.dataLayer).toBeUndefined()
  }
})

test('default denial precedes update, container initialization and events with configured IDs', () => {
  save()
  expect(prepareMarketingTags(accepted(), 'GTM-TEST123', '1234567890')).toEqual({ google: true, meta: true })
  trackMarketingEvent('octus_page_view', { page_path: '/institucional' })
  const queue = window.dataLayer!
  expect(Array.from(queue[0] as IArguments)).toEqual(['consent', 'default', googleConsentState(null)])
  expect(Array.from(queue[1] as IArguments)).toEqual(['consent', 'update', googleConsentState(accepted())])
  expect(queue[2]).toMatchObject({ event: 'gtm.js' })
  expect(queue[3]).toEqual({ event: 'octus_page_view', page_path: '/institucional' })
  expect(window.fbq!.queue).toEqual([
    ['consent', 'grant'], ['init', '1234567890'], ['track', 'PageView', { page_path: '/institucional' }],
  ])
})

test('initialization is idempotent, including React StrictMode re-runs', () => {
  prepareMarketingTags(accepted(), 'GTM-TEST123', '1234567890')
  prepareMarketingTags(accepted(), 'GTM-TEST123', '1234567890')
  expect(window.dataLayer!.filter(item => (item as { event?: string }).event === 'gtm.js')).toHaveLength(1)
  expect(window.fbq!.queue!.filter(item => (item as string[])[0] === 'init')).toHaveLength(1)
})

test('denial loads neither vendor and queues no behavioral event for later replay', () => {
  save(createCookieConsent({ analytics: false, marketing: false }))
  expect(prepareMarketingTags(null, 'GTM-TEST123', '1234567890')).toEqual({ google: false, meta: false })
  expect(trackMarketingEvent('lead_submit')).toBe(false)
  expect(window.dataLayer).toHaveLength(2)
  expect(window.fbq).toBeUndefined()
})

test('analytics-only leaves Meta blocked and malformed vendor IDs are ignored', () => {
  expect(prepareMarketingTags(createCookieConsent({ analytics: true, marketing: false }), 'GTM-TEST123', '1234567890'))
    .toEqual({ google: true, meta: false })
  expect(window.fbq).toBeUndefined()
  expect(prepareMarketingTags(accepted(), 'bad-id', 'not-a-number')).toEqual({ google: false, meta: false })
})

test('revoking consent denies Google and Meta and prevents further event emission', () => {
  prepareMarketingTags(accepted(), 'GTM-TEST123', '1234567890')
  const denied = createCookieConsent({ analytics: false, marketing: false })
  save(denied)
  applyMarketingConsent(denied)
  expect(consentWasReduced(accepted(), denied)).toBe(true)
  expect(consentWasReduced(accepted(), null)).toBe(true)
  expect(consentWasReduced(null, accepted())).toBe(false)
  expect(window.fbq!.queue!.at(-1)).toEqual(['consent', 'revoke'])
  expect(trackMarketingEvent('lead_submit')).toBe(false)
})

test('event payload drops personal data, arbitrary values, full URLs and query strings', () => {
  save()
  trackMarketingEvent('lead_submit', { form: 'institucional', email: 'fake@example.test',
    page_path: '/institucional?email=fake@example.test', plan: 'fake@example.test', phone: '12345678' })
  expect(window.dataLayer!.at(-1)).toEqual({ event: 'lead_submit', form: 'institucional' })
})

test('reload safety marker ignores stale stored acceptance until another explicit choice', () => {
  save()
  window.location.search = '?reset_consent=1'
  expect(trackMarketingEvent('lead_submit')).toBe(false)
})

test('blocked storage still respects a decision during the current document', () => {
  window.localStorage.setItem = () => { throw new Error('blocked') }
  saveCookieConsent(createCookieConsent({ analytics: false, marketing: false }))
  expect(trackMarketingEvent('lead_submit')).toBe(false)
  // Restaurar estado de módulo para outros testes sem armazenamento real.
  window.localStorage.setItem = () => {}
  saveCookieConsent(accepted())
})

test('Google Consent Mode v2 starts with every optional purpose denied', () => {
  expect(googleConsentState(null)).toEqual({
    analytics_storage: 'denied',
    ad_storage: 'denied',
    ad_user_data: 'denied',
    ad_personalization: 'denied',
    functionality_storage: 'granted',
    security_storage: 'granted',
  })
})

test('analytics consent does not grant advertising purposes', () => {
  const preferences = createCookieConsent({ analytics: true, marketing: false })
  expect(googleConsentState(preferences)).toMatchObject({
    analytics_storage: 'granted',
    ad_storage: 'denied',
    ad_user_data: 'denied',
    ad_personalization: 'denied',
  })
  expect(hasOptionalConsent(preferences)).toBe(true)
})

test('rejecting optional categories keeps third-party tags blocked', () => {
  const preferences = createCookieConsent({ analytics: false, marketing: false })
  expect(hasOptionalConsent(preferences)).toBe(false)
})
