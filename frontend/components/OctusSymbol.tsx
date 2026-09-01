import clsx from 'clsx'

/**
 * Símbolo do polvo recortado da arte oficial da plataforma.
 *
 * A origem continua sendo `/logo-octus.png`; o componente apenas enquadra a
 * parte superior da marca (o polvo) para usos compactos como avatar de marca.
 * Assim não criamos uma segunda versão da identidade que poderia divergir da
 * arte oficial.
 */
export default function OctusSymbol({
  className,
  title = 'Octus',
}: {
  className?: string
  title?: string
}) {
  return (
    <span
      role="img"
      aria-label={title}
      className={clsx('relative inline-block shrink-0 overflow-hidden', className)}
    >
      <span
        aria-hidden="true"
        className="pointer-events-none absolute max-w-none select-none"
        style={{
          width: '196.75%',
          height: '106%',
          left: '-46.75%',
          top: '13.25%',
          WebkitMaskImage: 'url(/logo-octus.png)',
          maskImage: 'url(/logo-octus.png)',
          WebkitMaskRepeat: 'no-repeat',
          maskRepeat: 'no-repeat',
          WebkitMaskSize: '100% 100%',
          maskSize: '100% 100%',
          backgroundColor: 'rgb(var(--brand-cyan))',
        }}
      />
    </span>
  )
}
