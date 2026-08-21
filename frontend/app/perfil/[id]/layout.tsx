import type { Metadata } from 'next'

// `noindex` deliberado. A página mostra nome, foto, desde quando é cliente,
// quantas compras fez e saldo de pontos de uma PESSOA. Ser acessível por link
// (o cliente abre o próprio perfil) é uma coisa; estar no índice do Google,
// pesquisável pelo nome dela, é outra — e num produto que vende módulo de LGPD
// seria contradição direta.
//
// O `robots.txt` também bloqueia `/perfil/`. Os dois juntos porque fazem
// trabalhos diferentes: o robots impede o rastreio, a meta tag tira do índice
// o que já foi rastreado antes desta mudança. Só o robots não desindexa nada
// que já esteja lá — o Google inclusive mantém a URL indexada "sem conteúdo"
// quando não consegue rastrear para ver a meta tag.
export const metadata: Metadata = {
  robots: { index: false, follow: false },
}

export default function PerfilLayout({ children }: { children: React.ReactNode }) {
  return children
}
