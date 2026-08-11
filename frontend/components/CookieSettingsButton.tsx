'use client'

import { Settings2 } from 'lucide-react'
import { OPEN_COOKIE_SETTINGS_EVENT } from '@/lib/cookieConsent'

export default function CookieSettingsButton() {
  return (
    <button type="button" onClick={() => window.dispatchEvent(new Event(OPEN_COOKIE_SETTINGS_EVENT))} className="inline-flex items-center gap-2 rounded-lg bg-white px-4 py-2 text-sm font-bold text-[#0C3D5A] hover:bg-brand-50">
      <Settings2 className="h-4 w-4" /> Abrir preferências de cookies
    </button>
  )
}
