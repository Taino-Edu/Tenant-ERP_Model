import { headers } from 'next/headers'
import { getTenantIconsForHost } from '@/lib/serverSiteConfig'
import { PLATFORM_URL } from '@/lib/seo'

/**
 * Declara quem OPERA a página, nas telas públicas que pedem credencial.
 *
 * Por que isto existe: em 21/08/2026 o Search Console acusou "Páginas
 * enganosas" (engenharia social) no domínio. A forma que dispara isso está
 * espalhada por toda vitrine: `benditacoxinha.3esysten.com.br/login` mostra a
 * marca "Bendita Coxinha" e pede e-mail e senha, num domínio que não pertence a
 * essa marca. Para um classificador automático, isso é indistinguível de uma
 * página falsa da Bendita Coxinha — é literalmente o formato do phishing.
 *
 * A defesa é a página AFIRMAR de quem ela é. Página de phishing não diz que é
 * operada por outra empresa nem linka para o site dela.
 *
 * Server Component de propósito, e este é o ponto todo: o nome da loja no resto
 * da tela só aparece depois da hidratação (SiteConfigContext busca no client),
 * então o HTML que o rastreador recebe diz apenas "Octus". Um aviso renderizado
 * no client seria invisível justamente para quem precisa lê-lo.
 *
 * No domínio da própria plataforma não renderiza nada: lá a marca e o domínio
 * são os mesmos, não há divergência para explicar.
 */
export default async function OperadorDaPlataforma() {
  const icons = await getTenantIconsForHost(headers().get('host'))
  const nomeDaLoja = icons?.siteName

  // Sem tenant resolvido (domínio da plataforma, ou API fora do ar) não há o
  // que declarar. Cair em silêncio é o certo: um texto genérico "esta loja usa
  // o Octus" no login da própria plataforma confundiria mais do que ajuda.
  if (!nomeDaLoja || nomeDaLoja === 'Octus') return null

  return (
    <p className="relative z-10 mx-auto mt-8 max-w-md px-6 pb-6 text-center text-xs leading-5 text-gray-500">
      <span className="font-semibold text-gray-400">{nomeDaLoja}</span> usa o Octus,
      plataforma de gestão da{' '}
      <a href={PLATFORM_URL} target="_blank" rel="noreferrer"
        className="text-brand-400 underline-offset-2 hover:underline">
        3E Systen
      </a>
      . Esta página é operada pela 3E Systen em nome do estabelecimento.
    </p>
  )
}
