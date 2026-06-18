'use client'

import { useState, useEffect, useCallback } from 'react'
import { userApi, UserPreferences, DEFAULT_PREFERENCES } from '@/lib/api'

const LOCAL_KEY = 'user-preferences'

function mergeWithDefaults(partial: Partial<UserPreferences>): UserPreferences {
  return {
    aiButton:      { ...DEFAULT_PREFERENCES.aiButton,      ...(partial.aiButton      ?? {}) },
    notifications: { ...DEFAULT_PREFERENCES.notifications, ...(partial.notifications ?? {}) },
    pdv:           { ...DEFAULT_PREFERENCES.pdv,           ...(partial.pdv           ?? {}) },
  }
}

export function usePreferences() {
  const [prefs,   setPrefs]   = useState<UserPreferences>(DEFAULT_PREFERENCES)
  const [loading, setLoading] = useState(true)
  const [saving,  setSaving]  = useState(false)

  // Carrega do servidor (com fallback para localStorage)
  useEffect(() => {
    const cached = localStorage.getItem(LOCAL_KEY)
    if (cached) {
      try { setPrefs(mergeWithDefaults(JSON.parse(cached))) } catch {}
    }

    userApi.getPreferences()
      .then(({ data }) => {
        const merged = mergeWithDefaults(data)
        setPrefs(merged)
        localStorage.setItem(LOCAL_KEY, JSON.stringify(merged))
      })
      .catch(() => {}) // usa o default/cache se não autenticado
      .finally(() => setLoading(false))
  }, [])

  const save = useCallback(async (next: UserPreferences) => {
    setPrefs(next)
    localStorage.setItem(LOCAL_KEY, JSON.stringify(next))
    setSaving(true)
    try {
      await userApi.updatePreferences(next)
    } catch {}
    finally { setSaving(false) }
  }, [])

  const update = useCallback((patch: Partial<UserPreferences>) => {
    const next = mergeWithDefaults({ ...prefs, ...patch })
    save(next)
  }, [prefs, save])

  return { prefs, loading, saving, update, save }
}
