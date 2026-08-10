'use client'
import { ReactNode, useEffect, useState } from 'react'
import { createPortal } from 'react-dom'

/**
 * Portal para `document.body` que LEVA O TEMA JUNTO.
 *
 * O tema claro do admin é escopado em `html.light .admin-shell` — de propósito:
 * a classe `light` mora no `<html>` e cascatearia para páginas que têm esquema
 * de cor próprio (institucional, /plataforma, vitrine pública). Ver o comentário
 * no topo de globals.css.
 *
 * O efeito colateral disso é que qualquer `createPortal(…, document.body)` sai
 * da árvore do `.admin-shell` e **perde o tema**: as variáveis voltam aos
 * valores escuros do `:root`, e as regras de compatibilidade (`.text-white`,
 * `.text-gray-*`, `.input`) deixam de casar. Na prática, um modal preto boiando
 * numa página clara — foi o que apareceu na Frente de Caixa.
 *
 * A classe `admin-portal` reintroduz o escopo perdido: globals.css lista as duas
 * lado a lado em cada regra de tema claro. `display: contents` faz a div não
 * gerar caixa nenhuma, então ela não interfere no layout nem cria um bloco
 * contentor que quebraria o `position: fixed` dos overlays — e mesmo assim as
 * variáveis CSS continuam sendo herdadas pelos filhos.
 *
 * Portalar continua sendo necessário: o container da página usa `animate-slide-up`,
 * e uma transform em qualquer ancestral vira bloco contentor de `position: fixed`
 * — o overlay ficaria preso dentro do card em vez de cobrir a tela.
 */
export default function AdminPortal({ children }: { children: ReactNode }) {
  // `document` não existe na renderização do servidor; monta só no cliente.
  const [montado, setMontado] = useState(false)
  useEffect(() => setMontado(true), [])
  if (!montado) return null

  return createPortal(
    <div className="admin-portal" style={{ display: 'contents' }}>{children}</div>,
    document.body,
  )
}
