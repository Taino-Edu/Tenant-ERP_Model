'use client'

import { PreferencesProvider } from '@/contexts/PreferencesContext'

export default function ClientProviders({ children }: { children: React.ReactNode }) {
  return <PreferencesProvider>{children}</PreferencesProvider>
}
