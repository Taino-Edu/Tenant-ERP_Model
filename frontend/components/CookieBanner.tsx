'use client'

import Link from 'next/link'
import { useEffect, useState } from 'react'
import { Cookie, Settings2, ShieldCheck, X } from 'lucide-react'
import { lgpdApi } from '@/lib/api'
import {
  OPEN_COOKIE_SETTINGS_EVENT,
  createCookieConsent,
  readCookieConsent,
  saveCookieConsent,
} from '@/lib/cookieConsent'

export default function CookieBanner() {
  const [visible, setVisible] = useState(false)
  const [customizing, setCustomizing] = useState(false)
  const [analytics, setAnalytics] = useState(false)
  const [marketing, setMarketing] = useState(false)

  useEffect(() => {
    const saved = readCookieConsent()
    if (saved) {
      setAnalytics(saved.analytics)
      setMarketing(saved.marketing)
    } else {
      // Remove a chave antiga para que uma decisão incompleta seja renovada.
      try { localStorage.removeItem('cookieConsent') } catch { /* armazenamento indisponível */ }
      setVisible(true)
    }

    const openSettings = () => {
      const current = readCookieConsent()
      setAnalytics(current?.analytics ?? false)
      setMarketing(current?.marketing ?? false)
      setCustomizing(true)
      setVisible(true)
    }
    window.addEventListener(OPEN_COOKIE_SETTINGS_EVENT, openSettings)
    return () => window.removeEventListener(OPEN_COOKIE_SETTINGS_EVENT, openSettings)
  }, [])

  async function persist(nextAnalytics: boolean, nextMarketing: boolean) {
    const preferences = createCookieConsent({ analytics: nextAnalytics, marketing: nextMarketing })
    try {
      saveCookieConsent(preferences)
    } catch {
      // A decisão ainda vale durante esta navegação mesmo se o navegador
      // estiver bloqueando armazenamento persistente.
      window.dispatchEvent(new CustomEvent('octus:cookie-consent-changed', { detail: preferences }))
    }

    setAnalytics(nextAnalytics)
    setMarketing(nextMarketing)
    setVisible(false)
    setCustomizing(false)

    // Registra tanto aceite quanto recusa; falha de rede não bloqueia o site.
    try { await lgpdApi.recordConsent(nextAnalytics || nextMarketing) } catch { /* best effort */ }
  }

  if (!visible) return null

  return (
    <div className="fixed inset-x-3 bottom-3 z-[9999] mx-auto max-w-3xl print:hidden" role="dialog" aria-modal="true" aria-labelledby="cookie-title">
      <div className="rounded-2xl border border-[#0C3D5A]/15 bg-white p-5 text-[#22384A] shadow-2xl shadow-[#0C3D5A]/20 sm:p-6">
        <div className="flex items-start gap-3">
          <span className="rounded-xl bg-brand-50 p-2 text-brand-700"><Cookie className="h-5 w-5" /></span>
          <div className="min-w-0 flex-1">
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 id="cookie-title" className="font-bold text-[#0C3D5A]">Sua privacidade, sua escolha</h2>
                <p className="mt-1 text-sm leading-relaxed text-[#526E80]">
                  Cookies necessários mantêm login, segurança e comandas funcionando e estão sempre ativos.
                  Com sua autorização, usamos dados opcionais para medir e melhorar o sistema. Não vendemos seus dados.
                </p>
              </div>
              {readCookieConsent() && (
                <button onClick={() => setVisible(false)} aria-label="Fechar preferências" className="rounded-lg p-1 text-[#6B8598] hover:bg-brand-50"><X className="h-4 w-4" /></button>
              )}
            </div>

            {customizing && (
              <div className="mt-4 space-y-2" aria-label="Categorias de cookies">
                <Preference label="Necessários" description="Autenticação, segurança, preferências e funcionamento do serviço." checked disabled onChange={() => {}} />
                <Preference label="Análise" description="Mede uso e desempenho para orientar melhorias. Opcional." checked={analytics} onChange={setAnalytics} />
                <Preference label="Marketing" description="Permite campanhas e mensuração publicitária, quando configuradas. Opcional." checked={marketing} onChange={setMarketing} />
              </div>
            )}

            <div className="mt-4 flex flex-col-reverse gap-2 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex flex-wrap gap-x-3 gap-y-1 text-xs">
                <Link href="/cookies" className="font-medium text-brand-700 underline">Política de Cookies</Link>
                <Link href="/privacidade" className="font-medium text-brand-700 underline">Privacidade</Link>
              </div>
              <div className="flex flex-wrap justify-end gap-2">
                {!customizing && (
                  <button onClick={() => setCustomizing(true)} className="inline-flex items-center gap-1.5 rounded-lg border border-[#0C3D5A]/20 px-3 py-2 text-xs font-semibold hover:bg-brand-50"><Settings2 className="h-3.5 w-3.5" /> Personalizar</button>
                )}
                <button onClick={() => persist(false, false)} className="rounded-lg border border-[#0C3D5A]/20 px-3 py-2 text-xs font-semibold hover:bg-brand-50">Recusar opcionais</button>
                <button onClick={() => persist(customizing ? analytics : true, customizing ? marketing : true)} className="inline-flex items-center gap-1.5 rounded-lg bg-brand-600 px-4 py-2 text-xs font-bold text-white hover:bg-brand-700"><ShieldCheck className="h-3.5 w-3.5" />{customizing ? 'Salvar escolhas' : 'Aceitar todos'}</button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

function Preference({ label, description, checked, disabled, onChange }: { label: string; description: string; checked: boolean; disabled?: boolean; onChange: (value: boolean) => void }) {
  return (
    <label className="flex cursor-pointer items-center gap-3 rounded-xl border border-[#0C3D5A]/10 px-3 py-2.5">
      <input type="checkbox" className="h-4 w-4 accent-sky-600" checked={checked} disabled={disabled} onChange={e => onChange(e.target.checked)} />
      <span className="flex-1"><span className="block text-sm font-semibold text-[#0C3D5A]">{label}</span><span className="block text-xs text-[#6B8598]">{description}</span></span>
      {disabled && <span className="text-[10px] font-bold uppercase text-green-700">Sempre ativo</span>}
    </label>
  )
}
