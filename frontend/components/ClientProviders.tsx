'use client'

import { PreferencesProvider } from '@/contexts/PreferencesContext'
import { SiteConfigProvider } from '@/contexts/SiteConfigContext'

export default function ClientProviders({ children }: { children: React.ReactNode }) {
  return (
    <SiteConfigProvider>
      <PreferencesProvider>{children}</PreferencesProvider>
    </SiteConfigProvider>
  )
}
