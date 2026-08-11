import { expect, test } from '@playwright/test'
import {
  COOKIE_CONSENT_VERSION,
  createCookieConsent,
  parseCookieConsent,
} from '../lib/cookieConsent'

test('creates a versioned preference record with necessary storage always enabled', () => {
  const value = createCookieConsent({ analytics: false, marketing: true }, '2026-08-11T12:00:00.000Z')
  expect(value).toEqual({
    version: COOKIE_CONSENT_VERSION,
    necessary: true,
    analytics: false,
    marketing: true,
    decidedAt: '2026-08-11T12:00:00.000Z',
  })
})

test('rejects legacy, malformed and stale consent records', () => {
  expect(parseCookieConsent('accepted')).toBeNull()
  expect(parseCookieConsent('{bad-json')).toBeNull()
  expect(parseCookieConsent(JSON.stringify({
    version: '1.0', necessary: true, analytics: true, marketing: true, decidedAt: '2026-01-01',
  }))).toBeNull()
})

test('accepts only complete current-version consent records', () => {
  const stored = createCookieConsent({ analytics: true, marketing: false })
  expect(parseCookieConsent(JSON.stringify(stored))).toEqual(stored)
})
