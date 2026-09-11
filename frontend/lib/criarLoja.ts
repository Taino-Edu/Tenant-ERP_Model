// =============================================================================
// criarLoja.ts — Endereço da loja criada pelo próprio lojista no site.
//
// O slug precisa bater com o que o backend aceita (TenantSignupService): 1 a 20
// caracteres, letras minúsculas, números e hífen, sem hífen nas pontas. A
// validação aqui é conforto — quem decide é o servidor.
// =============================================================================

import { ROOT_DOMAIN } from './institucional'

export const SLUG_MAX = 20

const SLUG_VALIDO = /^[a-z0-9](?:[a-z0-9-]{0,18}[a-z0-9])?$/

// Faixa Unicode dos acentos combinantes (U+0300 a U+036F). Comparado por
// código numérico, e não com escape dentro de regex, para que nenhum editor ou
// formatador troque o escape pelo caractere invisível.
const ACENTO_INICIO = 0x300
const ACENTO_FIM = 0x36f

/** "Empório São João" → "emporio-sao-joao". */
export function slugDaLoja(nome: string): string {
  const semAcento = Array.from(nome.normalize('NFD'))
    .filter(caractere => {
      const codigo = caractere.codePointAt(0) ?? 0
      return codigo < ACENTO_INICIO || codigo > ACENTO_FIM
    })
    .join('')

  return semAcento
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+/, '')
    .slice(0, SLUG_MAX)
    .replace(/-+$/, '')
}

export const slugValido = (slug: string) => SLUG_VALIDO.test(slug)

/** Domínio raiz das lojas, para exibir e montar URLs. */
export const dominioDasLojas = () => ROOT_DOMAIN || '3esysten.com.br'

export const enderecoDaLoja = (slug: string) => `${slug || 'sua-loja'}.${dominioDasLojas()}`

/** Origem (protocolo + host + porta) do subdomínio da loja. A porta só existe em
 *  dev local (loja.localhost:3000); em produção window.location.port é vazio. */
function origemDaLoja(slug: string): string {
  const porta = window.location.port ? `:${window.location.port}` : ''
  return `${window.location.protocol}//${slug}.${dominioDasLojas()}${porta}`
}

/** Troca o ticket da confirmação por sessão no subdomínio certo e abre o guia
 *  inicial — a loja acabou de nascer vazia, e a tela de comandas não diz nada a
 *  quem ainda não cadastrou um produto. */
export function urlDeEntradaNaLoja(slug: string, ticket: string): string {
  const params = new URLSearchParams({ ticket, next: '/admin/primeiros-passos' })
  return `${origemDaLoja(slug)}/api/auth/redeem-login?${params}`
}

export const urlDeLoginDaLoja = (slug: string) => `${origemDaLoja(slug)}/login`

/** Evento que o card de preço dispara ao escolher um plano. O formulário de
 *  criação de loja fica na mesma página, então a escolha precisa chegar nele sem
 *  recarregar — recarregar jogaria fora o que a pessoa já digitou. */
export const PLANO_ESCOLHIDO_EVENT = 'octus:plano-escolhido'

/** Registra o plano escolhido na URL (sobrevive a F5 e a link compartilhado),
 *  avisa o formulário e leva a pessoa até ele.
 *
 *  A rolagem é feita aqui, e não deixada para o href="#contato" do botão: trocar
 *  a URL com replaceState dentro do clique cancela a navegação para a âncora —
 *  medido no Chromium, o hash nunca chegava a mudar. O href continua no botão
 *  só como caminho para quem está sem JavaScript. */
export function escolherPlanoParaTeste(nome: string) {
  const url = new URL(window.location.href)
  url.searchParams.set('plano', nome)
  url.hash = 'contato'
  window.history.replaceState(null, '', url)
  window.dispatchEvent(new CustomEvent<string>(PLANO_ESCOLHIDO_EVENT, { detail: nome }))
  document.getElementById('contato')?.scrollIntoView({ behavior: 'smooth' })
}
