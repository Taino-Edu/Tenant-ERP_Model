'use client'
import { useEffect } from 'react'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import { brandCssVars } from '@/lib/colors'

export const BRAND_CACHE_KEY = 'admin-brand-vars'

/** Aplica a cor de marca do tenant (SiteConfig.colorPrimary) nos tokens
 * brand-400/500/600 do Tailwind, que centenas de classNames já espalhadas no
 * admin (bg-brand-500, text-brand-400 etc.) passam a refletir automaticamente
 * — sem editar essas páginas. Cacheia o último resultado em localStorage pra
 * o script inline em app/admin/layout.tsx aplicar antes da hidratação (evita
 * flash da cor default no reload). */
export default function TenantColorInjector() {
  const { site } = useSiteConfig()

  useEffect(() => {
    if (!site.colorPrimary) return
    const vars = brandCssVars(site.colorPrimary)
    const root = document.documentElement
    for (const [key, value] of Object.entries(vars)) {
      root.style.setProperty(key, value)
    }
    try { localStorage.setItem(BRAND_CACHE_KEY, JSON.stringify(vars)) } catch {}
  }, [site.colorPrimary])

  return null
}
