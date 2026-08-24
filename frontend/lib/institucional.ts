// =============================================================================
// institucional.ts — O que o site público (institucional + afiliados) divide.
//
// Existia um único arquivo institucional, então tema, contatos e o envio de
// lead moravam dentro dele. Com a página de afiliados passam a ser DUAS telas
// com o mesmo cabeçalho, o mesmo rodapé, o mesmo par claro/escuro e o mesmo
// formulário — e "duas telas com a mesma aparência" é exatamente o arranjo em
// que a cópia diverge: uma ganha um link novo no menu, a outra não; uma corrige
// um telefone, a outra fica com o antigo.
//
// Aqui ficam os valores e o comportamento compartilhados. Cada página cuida só
// do próprio conteúdo.
// =============================================================================

import { useEffect, useState } from 'react'
import { leadsApi, type LeadKind } from '@/lib/api'

export const ROOT_DOMAIN = process.env.NEXT_PUBLIC_ROOT_DOMAIN || ''

/** Versão do aviso de privacidade aceito nos formulários públicos. Muda junto
 *  com o texto de /privacidade — o backend guarda o valor para provar QUAL
 *  texto a pessoa viu no dia do aceite. */
export const PRIVACY_NOTICE_VERSION = '2.2'

// Contatos e perfis sociais moraram aqui até 21/08/2026. Foram para
// lib/contatos.ts porque o JSON-LD da landing (servidor) precisa deles, e este
// arquivo é client-only por causa dos hooks abaixo. Reexportados para não
// quebrar os imports que já apontavam para cá.
export { CNPJ, CNPJ_DIGITOS, CONTACTS, SOCIAL_PROFILES, telHref } from './contatos'


export const NAV_LINKS = [
  { href: '/institucional#plataforma', label: 'Plataforma' },
  { href: '/institucional#recursos', label: 'Recursos' },
  { href: '/institucional#contador', label: 'Portal do Contador' },
  { href: '/institucional#planos', label: 'Planos' },
  { href: '/institucional#clientes', label: 'Clientes' },
  { href: '/institucional#fundadores', label: 'Fundadores' },
  { href: '/parceiros', label: 'Afiliados' },
]

export type InstitucionalTheme = ReturnType<typeof themeFor>

export function themeFor(isDark: boolean) {
  return isDark
    ? {
        page: 'bg-[#020914] text-white', surface: 'bg-[#061426]', soft: 'bg-[#091b31]',
        border: 'border-white/10', heading: 'text-white', body: 'text-slate-300', muted: 'text-slate-400',
        card: 'bg-[#061426] border-white/10 hover:border-octus-400/50', header: 'bg-[#020914]/92 border-white/10',
        outline: 'border-white/20 text-white hover:bg-white/5',
        input: 'bg-white/5 border-white/15 text-white placeholder:text-slate-500',
      }
    : {
        // `octus-50` no lugar do antigo `#f3f7ff`: aquele era um lavanda
        // puxado para o azul-royal do Tailwind, e ao lado do ciano da logo lia
        // como uma segunda marca. Este é o mesmo ciano, só bem diluído.
        page: 'bg-white text-[#071f3d]', surface: 'bg-white', soft: 'bg-octus-50',
        border: 'border-[#0b3261]/10', heading: 'text-[#071f3d]', body: 'text-[#38516d]', muted: 'text-[#657b93]',
        card: 'bg-white border-[#0b3261]/10 hover:border-octus-500/50', header: 'bg-white/90 border-[#0b3261]/10',
        outline: 'border-[#0b3261]/20 text-[#0b3261] hover:bg-octus-50',
        input: 'bg-white border-[#0b3261]/15 text-[#071f3d] placeholder:text-[#7d8ea1]',
      }
}

/**
 * Tema claro/escuro do site público, persistido em localStorage.
 *
 * Começa SEMPRE no claro e corrige no efeito, em vez de ler o localStorage na
 * inicialização do estado: o HTML vem do servidor, que não tem acesso ao
 * armazenamento do navegador, e ler ali produziria um primeiro render diferente
 * do que o servidor mandou (erro de hidratação).
 *
 * O efeito também marca `body.institucional-page`, que o globals.css usa para
 * esconder o rodapé global e o botão de instalar PWA — o site público tem
 * rodapé próprio e não é o app instalável da loja.
 */
export function useInstitucionalTheme() {
  const [isDark, setIsDark] = useState(false)

  useEffect(() => {
    setIsDark(localStorage.getItem('institucional-theme') === 'dark')
    document.body.classList.add('institucional-page')
    return () => document.body.classList.remove('institucional-page')
  }, [])

  // `institucional-dark` no <body> é o que faz a classe `.octus-accent` do
  // globals.css trocar de tom. O ciano da marca precisa de dois valores — um
  // escuro o bastante para ler sobre branco, um claro o bastante para ler sobre
  // o navy — e esse par aparece em ~30 strings estáticas de className ao longo
  // das duas páginas. Um token no CSS resolve nas duas de uma vez; passar o
  // tema por interpolação exigiria transformar cada uma dessas strings em
  // template literal, com a mesma chance de esquecer uma.
  useEffect(() => {
    document.body.classList.toggle('institucional-dark', isDark)
    return () => document.body.classList.remove('institucional-dark')
  }, [isDark])

  function toggleTheme() {
    setIsDark(current => {
      const next = !current
      localStorage.setItem('institucional-theme', next ? 'dark' : 'light')
      return next
    })
  }

  return { isDark, toggleTheme, theme: themeFor(isDark) }
}

export interface LeadFields {
  nome: string
  telefone: string
  email?: string
  mensagem?: string
  privacyNoticeAcknowledged: boolean
}

/**
 * Envia um lead já carimbado com a origem da visita.
 *
 * A atribuição (UTMs, referrer, página) é lida da URL no momento do envio, e
 * não guardada em estado, porque é a mesma para os dois formulários do site e
 * errar aqui significa lead chegando no CRM sem saber de onde veio.
 *
 * `campaign` recebe um padrão por formulário: o link de afiliados quase nunca
 * vem com `?campaign=` na URL, e sem isso as candidaturas a parceiro cairiam na
 * mesma fila indistinta dos pedidos de teste grátis.
 *
 * `kind` é outra coisa e por isso é outro parâmetro: campanha é rótulo de
 * marketing e pode ser sobrescrita pela URL; `kind` diz QUAL formulário é, e o
 * backend deriva dele a finalidade do tratamento gravada no registro de
 * privacidade. Um não pode mandar no outro.
 */
export async function submitLead(
  fields: LeadFields,
  { kind = 'Institucional', defaultCampaign }: { kind?: LeadKind; defaultCampaign?: string } = {},
) {
  const query = new URLSearchParams(window.location.search)
  const trim = (value: string | null) => value?.slice(0, 120) || undefined

  await leadsApi.create({
    ...fields,
    kind,
    privacyNoticeVersion: PRIVACY_NOTICE_VERSION,
    campaign: trim(query.get('campaign')) ?? defaultCampaign,
    utmSource: trim(query.get('utm_source')),
    utmMedium: trim(query.get('utm_medium')),
    utmCampaign: trim(query.get('utm_campaign')),
    utmTerm: trim(query.get('utm_term')),
    utmContent: trim(query.get('utm_content')),
    referrerUrl: document.referrer.slice(0, 500) || undefined,
    landingPage: window.location.href.slice(0, 500),
  })
}
