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
 * `tone="brand"` usa a cor de marca do tenant (--brand-500, injetada em runtime
 * por TenantColorInjector), para quando a marca deve aparecer colorida e não
 * na cor do texto.
 */
export default function Logo({
  className,
  tone = 'current',
  title = 'Octus',
}: {
  className?: string
  tone?: 'current' | 'brand'
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
        backgroundColor: tone === 'brand' ? 'rgb(var(--brand-500))' : 'currentColor',
      }}
    />
  )
}
