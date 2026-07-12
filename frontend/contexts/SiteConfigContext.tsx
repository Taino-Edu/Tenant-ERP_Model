'use client'

import { createContext, useContext, useState, useEffect, ReactNode } from 'react'
import { siteConfigApi, SiteConfigDto } from '@/lib/api'

// Espelha os defaults do backend (SiteConfig.cs) — usado até a config real
// carregar, e como base pra qualquer tenant que não tenha personalizado nada.
export const DEFAULT_SITE_CONFIG: SiteConfigDto = {
  siteName: 'Minha Loja',
  heroSubtitle: 'Produtos e a melhor experiência de atendimento da região. Acumule pontos e compre direto na mesa.',
  addressLine: 'Sua Cidade — UF',
  contactPersonName: 'Atendimento',
  whatsappNumber: '',
  contactEmail: 'contato@tenant-erp.local',
  logoUrl: null,
  faviconUrl: null,
  pwaIconUrl: null,
  adminIconUrl: null,
  navTorneiosLabel: '',
  navProdutosLabel: 'Produtos',
  navMercadoLabel: '',
  navPontosLabel: 'Pontos',
  ctaVerEventosLabel: '',
  ctaVerTorneiosLabel: '',
  ctaVerProdutosLabel: 'Ver Produtos',
  torneiosEyebrow: '',
  torneiosTitle: '',
  produtosEyebrow: 'Vitrine',
  produtosTitle: 'Em Destaque',
  pontosEyebrow: 'Programa de Fidelidade',
  pontosTitle: 'Ganhe pontos a cada visita',
  pontosParagraph: 'Acumule pontos nas suas compras e troque por descontos. Só com CPF e WhatsApp — nada de senha ou aplicativo.',
  pontosFidelidadeAtivo: true,
  colorPrimary: '#3EC2F2',
  colorAccent: '#FFE45E',
  colorNavy: '#0C3D5A',
  colorBackground: '#EBF7FD',
  colorCard: '#FFFFFF',
  // Default permissivo (não bloqueia nada) até o fetch real responder — mesmo
  // espírito do resto destes defaults, que espelham o comportamento atual.
  enabledModules: ['fiscal'],
}

interface SiteConfigContextValue {
  site:    SiteConfigDto
  loading: boolean
}

const SiteConfigContext = createContext<SiteConfigContextValue | null>(null)

export function SiteConfigProvider({ children }: { children: ReactNode }) {
  const [site,    setSite]    = useState<SiteConfigDto>(DEFAULT_SITE_CONFIG)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    siteConfigApi.get()
      .then(({ data }: { data: SiteConfigDto }) => setSite(data))
      .catch((err: unknown) => {
        // TenantResolutionMiddleware já bloqueia TODA chamada de API com 403
        // pra tenant suspenso — em vez de deixar a página carregar vazia (sem
        // produtos/config), redireciona pra uma tela clara. Não mexe no
        // painel /admin (o lojista já sabe do status por /plataforma, e
        // mandar ele pra uma tela de cliente seria confuso).
        const status = (err as { response?: { status?: number } })?.response?.status
        if (typeof window !== 'undefined' && status === 403) {
          const path = window.location.pathname
          if (!path.startsWith('/admin') && path !== '/loja-suspensa') {
            window.location.href = '/loja-suspensa'
          }
        }
      })
      .finally(() => setLoading(false))
  }, [])

  return (
    <SiteConfigContext.Provider value={{ site, loading }}>
      {children}
    </SiteConfigContext.Provider>
  )
}

/** Nome/contato/endereço/logo da loja atual — busca única cacheada no provider raiz. */
export function useSiteConfig() {
  const ctx = useContext(SiteConfigContext)
  if (!ctx) throw new Error('useSiteConfig must be used inside SiteConfigProvider')
  return ctx
}
