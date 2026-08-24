// =============================================================================
// contatos.ts — Telefones, e-mail e perfis sociais da 3E Systen.
//
// Separado de lib/institucional.ts por uma razão de fronteira, não de gosto:
// aquele arquivo importa `useEffect`/`useState` (tem o hook do formulário de
// lead), o que o torna client-only. O JSON-LD da landing precisa dos MESMOS
// endereços e é montado no servidor — importar institucional.ts de lá derruba
// o build inteiro ("You're importing a component that needs useEffect").
//
// Aqui só existe dado. Serve os dois lados.
// =============================================================================

/** CNPJ da 3E Systen, já formatado para leitura.
 *
 *  Fica aqui, e não escrito à mão em cada tela, pelo mesmo motivo dos telefones:
 *  ele aparece no rodapé, nos Termos, na Política de Privacidade e no JSON-LD
 *  declarado ao Google. Número de inscrição divergente entre o contrato e o site
 *  é o tipo de erro que ninguém percebe e que enfraquece justamente os
 *  documentos que existem para identificar a empresa. */
export const CNPJ = '68.381.935/0001-07'

/** Só os dígitos — é o formato que `schema.org/taxID` e integrações esperam. */
export const CNPJ_DIGITOS = CNPJ.replace(/\D/g, '')

export const CONTACTS = {
  marketingWhatsapp: 'https://wa.me/5517997455482',
  marketingPhone: '+55 17 99745-5482',
  supportPhone: '+55 17 99756-3555',
  devPhone: '+55 17 99745-5282',
  email: '3esysten@gmail.com',
  instagram: 'https://www.instagram.com/3e.systen/',
  linkedin: 'https://www.linkedin.com/company/3e-systen/',
  // Sem os parâmetros de compartilhamento (`?_r=`, `?_t=`): eles são um token
  // da sessão de quem copiou o link, não fazem parte do endereço do perfil, e
  // entrariam no `sameAs` declarado ao Google como se fizessem.
  tiktok: 'https://www.tiktok.com/@3esysten',
  /** Vazio = perfil ainda não existe/não informado. Ver SOCIAL_PROFILES. */
  youtube: '',
  facebook: '',
} as const

/** Os perfis sociais em UMA lista, na ordem em que aparecem no rodapé.
 *
 *  Existe por dois motivos que se encontram no mesmo dado. O rodapé mostra os
 *  ícones; o JSON-LD da landing declara os mesmos endereços em `sameAs`, que é
 *  como o Google liga o site aos perfis (e é o que alimenta painel de
 *  conhecimento e resultado de marca). Mantidos separados, um perfil novo
 *  entrava no rodapé e o buscador nunca ficava sabendo.
 *
 *  Entrada com URL vazia não renderiza e não entra no `sameAs` — perfil que
 *  ainda não existe não vira link quebrado nem declaração falsa para o Google.
 */
export const SOCIAL_PROFILES = [
  { key: 'instagram', label: 'Instagram', url: CONTACTS.instagram },
  { key: 'tiktok',    label: 'TikTok',    url: CONTACTS.tiktok },
  { key: 'youtube',   label: 'YouTube',   url: CONTACTS.youtube },
  { key: 'facebook',  label: 'Facebook',  url: CONTACTS.facebook },
  { key: 'linkedin',  label: 'LinkedIn',  url: CONTACTS.linkedin },
].filter(p => p.url.length > 0)

/** `tel:` a partir do número exibido. O `+` precisa sobreviver ao corte dos
 *  separadores: sem ele o discador trata "5517..." como número local. */
export const telHref = (display: string) => `tel:+${display.replace(/\D/g, '')}`
