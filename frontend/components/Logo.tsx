import clsx from 'clsx'

/**
 * Marca da plataforma, pintada por CSS em vez de embutida no arquivo.
 *
 * O arquivo original é MONOCROMÁTICO (formas brancas sobre fundo preto), o que
 * abre um caminho melhor do que manter duas artes: `mask-image` usa o PNG só
 * como recorte e a cor vem de `background-color`. Uma arte, qualquer cor.
 *
 * É isso que atende "a logo muda quando trocamos de dark pra white" sem
 * duplicar asset: `currentColor` faz a marca herdar a cor do texto do contexto,
 * então ela acompanha o tema automaticamente — inclusive o tema claro do
 * /admin, que troca --text-primary de #F3F4F6 para #111118. Dois arquivos
 * exigiriam alguém lembrar de trocar os dois a cada ajuste de arte, e de
 * escolher qual carregar em cada tela.
 *
 * Os três tons, e quando usar cada um:
 *
 * - `cyan` (padrão) — o ciano fixo da plataforma (#28b0d6). É o correto para a
 *   marca do Octus: `--brand-cyan` não é tocado pelo TenantColorInjector, então
 *   a identidade da plataforma não vira roxa numa loja que escolheu roxo.
 * - `current` — herda `currentColor`. Para quando a marca deve ler como texto e
 *   acompanhar o tema claro/escuro do contexto.
 * - `brand` — a cor de marca DO TENANT (--brand-500). Só faz sentido quando a
 *   marca representa a loja, não a plataforma.
 */
export default function Logo({
  className,
  tone = 'cyan',
  title = 'Octus',
}: {
  className?: string
  tone?: 'cyan' | 'current' | 'brand'
  title?: string
}) {
  return (
    <span
      role="img"
      aria-label={title}
      className={clsx('inline-block shrink-0', className)}
      style={{
        // O PNG tem alpha = forma, então o modo padrão (alpha) já recorta certo.
        WebkitMaskImage: 'url(/logo-octus.png)',
        maskImage: 'url(/logo-octus.png)',
        WebkitMaskRepeat: 'no-repeat',
        maskRepeat: 'no-repeat',
        WebkitMaskPosition: 'center',
        maskPosition: 'center',
        // `contain` e não `cover`: a marca não pode ser cortada em nenhuma
        // proporção de caixa que o chamador use.
        WebkitMaskSize: 'contain',
        maskSize: 'contain',
        backgroundColor:
          tone === 'cyan'  ? 'rgb(var(--brand-cyan))'
          : tone === 'brand' ? 'rgb(var(--brand-500))'
          : 'currentColor',
      }}
    />
  )
}
