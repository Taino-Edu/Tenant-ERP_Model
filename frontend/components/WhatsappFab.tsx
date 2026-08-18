'use client'
// =============================================================================
// WhatsappFab.tsx — Atalho de WhatsApp da vitrine.
//
// A versão anterior era uma pílula verde grande, sempre aberta, ancorada no
// canto inferior direito. Três problemas somados:
//
//  1. Ela nunca encolhia, então ficava permanentemente por cima do conteúdo —
//     no celular tapava uma coluna inteira da grade de produtos.
//  2. Dividia o canto com o botão de instalar o PWA (`fixed bottom-5 right-5`),
//     e os dois se sobrepunham.
//  3. Dizia só "Falar com <nome>", que não informa nada: o visitante não sabe
//     se aquilo tira dúvida, confirma estoque ou fecha pedido.
//
// Aqui o botão é adaptativo: aparece expandido UMA vez por sessão com uma frase
// que explica para que serve, depois vive como um círculo discreto. Ele recolhe
// quando o visitante está rolando para baixo (lendo), reaparece quando ele
// volta, e some de vez quando o rodapé — que já traz o número — entra na tela
// ou quando há um modal aberto.
// =============================================================================

import { useEffect, useRef, useState } from 'react'
import { X } from 'lucide-react'

/** Chave de sessão: a dica explica o botão uma vez e não insiste a cada página. */
const TIP_SEEN_KEY = 'wa-fab-tip'
/** Quanto tempo a dica fica aberta sozinha antes de recolher. */
const TIP_AUTO_CLOSE_MS = 9000
/** Rolagem mínima (px) para tratar como "o visitante está lendo" e recolher. */
const SCROLL_THRESHOLD = 8

/** Glifo oficial do WhatsApp. O `MessageCircle` genérico do lucide não é
 *  reconhecido de relance — a marca é o que faz o botão ser entendido sem ler. */
function WhatsappGlyph({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413z" />
    </svg>
  )
}

export default function WhatsappFab({
  number,
  personName,
  storeName,
  /** Some enquanto há modal aberto — nada flutua por cima de um diálogo. */
  suppressed = false,
  /** Cores da vitrine, para a dica combinar com o tema claro/escuro escolhido. */
  card,
  cardText,
  cardNavy,
  border,
}: {
  number: string
  personName: string
  storeName: string
  suppressed?: boolean
  card: string
  cardText: string
  cardNavy: string
  border: string
}) {
  const [tipOpen, setTipOpen] = useState(false)
  const [collapsed, setCollapsed] = useState(true)
  const [atFooter, setAtFooter] = useState(false)
  const lastScrollY = useRef(0)

  // Mensagem já preenchida: o visitante chega no atendimento dizendo de onde
  // veio, em vez de abrir uma conversa em branco que alguém precisa decifrar.
  const href = `https://wa.me/${number}?text=${encodeURIComponent(
    `Olá! Vim pelo site da ${storeName} e gostaria de mais informações.`,
  )}`

  // Dica de abertura — uma vez por sessão, depois de a página assentar.
  useEffect(() => {
    let seen = false
    try { seen = sessionStorage.getItem(TIP_SEEN_KEY) === '1' } catch {}
    if (seen) return

    const show = setTimeout(() => {
      setTipOpen(true)
      try { sessionStorage.setItem(TIP_SEEN_KEY, '1') } catch {}
    }, 2200)
    const hide = setTimeout(() => setTipOpen(false), 2200 + TIP_AUTO_CLOSE_MS)
    return () => { clearTimeout(show); clearTimeout(hide) }
  }, [])

  // Rolando para baixo o visitante está lendo: o botão vira só o círculo.
  // Rolando para cima (ou parado no topo) ele volta a caber na tela sem atrapalhar.
  useEffect(() => {
    lastScrollY.current = window.scrollY
    function onScroll() {
      const y = window.scrollY
      const delta = y - lastScrollY.current
      if (Math.abs(delta) < SCROLL_THRESHOLD) return
      lastScrollY.current = y
      if (delta > 0) setTipOpen(false)
      setCollapsed(delta > 0 || y > 120)
    }
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  // O rodapé já mostra o número e o e-mail. Manter o botão flutuando ali seria
  // repetir a mesma informação por cima dela própria.
  //
  // Sem `rootMargin`: o gatilho certo é a borda de baixo da viewport, que é
  // exatamente onde o botão mora. Encolher a raiz por cima (um `-40%` embaixo,
  // por exemplo) empurraria o gatilho para o meio da tela e o rodapé poderia
  // aparecer inteiro no canto do botão sem nunca "intersectar".
  useEffect(() => {
    const footer = document.querySelector('footer')
    if (!footer) return
    const observer = new IntersectionObserver(([entry]) => setAtFooter(entry.isIntersecting))
    observer.observe(footer)
    return () => observer.disconnect()
  }, [])

  if (!number) return null

  const hidden = suppressed || atFooter
  const expanded = tipOpen || !collapsed

  return (
    <div
      className={`js-wa-fab fixed right-4 z-40 flex flex-col items-end gap-2 transition-all duration-300 sm:right-6 ${
        hidden ? 'pointer-events-none translate-y-6 opacity-0' : 'translate-y-0 opacity-100'
      }`}
      style={{ bottom: 'calc(1rem + env(safe-area-inset-bottom, 0px))' }}
    >
      {/* Dica informativa: diz o que dá para resolver por ali e quem responde.
          Nada de `aria-hidden` no contêiner: ele embrulha o botão de fechar, e
          esconder da árvore de acessibilidade um controle que continua no fluxo
          de tabulação deixa quem navega por teclado com foco num elemento que o
          leitor de tela não anuncia. */}
      {tipOpen && (
        <div
          role="status"
          className="relative max-w-[17rem] rounded-2xl border px-4 py-3 pr-9 text-left shadow-lg"
          style={{ backgroundColor: card, borderColor: border, boxShadow: '0 10px 30px rgba(0,0,0,0.12)' }}
        >
          <p className="text-[13px] font-bold leading-tight" style={{ color: cardNavy }}>
            Precisa de ajuda para escolher?
          </p>
          <p className="mt-1 text-[12px] leading-snug" style={{ color: cardText }}>
            {personName} responde pelo WhatsApp: disponibilidade, preço, reserva e formas de pagamento.
          </p>
          <button
            type="button"
            onClick={() => setTipOpen(false)}
            aria-label="Fechar aviso"
            className="absolute right-1.5 top-1.5 rounded-lg p-1.5 transition-opacity hover:opacity-60"
            style={{ color: cardText }}
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      )}

      <a
        href={href}
        target="_blank"
        rel="noreferrer"
        aria-label={`Falar com ${personName} da ${storeName} pelo WhatsApp`}
        onMouseEnter={() => setCollapsed(false)}
        onMouseLeave={() => setCollapsed(true)}
        onFocus={() => setCollapsed(false)}
        onBlur={() => setCollapsed(true)}
        className="flex h-14 items-center gap-2.5 overflow-hidden rounded-full pl-[15px] pr-[15px] text-sm font-bold text-white shadow-xl outline-none transition-all duration-300 focus-visible:ring-4 focus-visible:ring-[#25D366]/40 active:scale-95"
        style={{ backgroundColor: '#25D366', boxShadow: '0 8px 24px rgba(37,211,102,0.35)' }}
      >
        <WhatsappGlyph className="h-[26px] w-[26px] shrink-0" />
        {/* O rótulo ocupa largura só quando expandido; `max-w` animável evita o
            salto de layout que `display:none` causaria. */}
        <span
          className={`hidden whitespace-nowrap transition-all duration-300 sm:block ${
            expanded ? 'max-w-[13rem] opacity-100' : 'max-w-0 opacity-0'
          }`}
        >
          Falar com {personName}
        </span>
      </a>
    </div>
  )
}
