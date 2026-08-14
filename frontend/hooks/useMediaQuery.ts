'use client'
import { useEffect, useState } from 'react'

/**
 * Casa uma media query CSS e re-renderiza quando ela muda.
 *
 * Começa SEMPRE em `false`, mesmo que a query já case no momento da montagem:
 * no SSR não existe `window`, e ler o valor real no initializer faria o
 * primeiro render do client divergir do HTML do servidor (mismatch de
 * hidratação — o React descarta a árvore e remonta, causando o flash que o
 * Sidebar já contornava com o padrão `mounted`). O valor verdadeiro entra no
 * primeiro efeito, ainda antes do paint.
 */
export function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState(false)

  useEffect(() => {
    const mql = window.matchMedia(query)
    setMatches(mql.matches)
    const onChange = (e: MediaQueryListEvent) => setMatches(e.matches)
    mql.addEventListener('change', onChange)
    return () => mql.removeEventListener('change', onChange)
  }, [query])

  return matches
}

// Os limites batem com os breakpoints do Tailwind (tailwind.config.ts) — se um
// mudar, o outro precisa mudar junto, senão o layout CSS e a lógica de
// renderização condicional discordam na faixa entre os dois valores.
const MOBILE_QUERY = '(max-width: 639px)'  // < sm
const TABLET_QUERY = '(max-width: 1023px)' // < lg

/** `true` abaixo de 640px (mesmo ponto de corte do prefixo `sm:`). */
export function useIsMobile(): boolean {
  return useMediaQuery(MOBILE_QUERY)
}

/** `true` abaixo de 1024px — celular e tablet em pé. */
export function useIsCompact(): boolean {
  return useMediaQuery(TABLET_QUERY)
}

/**
 * Trava o scroll do body enquanto `locked` estiver ativo (drawer, sheet,
 * modal). Conta referências: com dois overlays empilhados, fechar o de cima
 * não pode destravar o scroll enquanto o de baixo continua aberto.
 */
let lockCount = 0

export function useScrollLock(locked: boolean) {
  useEffect(() => {
    if (!locked) return
    lockCount++
    document.body.classList.add('overflow-locked')
    return () => {
      lockCount = Math.max(0, lockCount - 1)
      if (lockCount === 0) document.body.classList.remove('overflow-locked')
    }
  }, [locked])
}
