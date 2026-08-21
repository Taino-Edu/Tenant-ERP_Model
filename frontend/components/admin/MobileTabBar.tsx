'use client'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { Menu } from 'lucide-react'
import clsx from 'clsx'
import { tabBarItems } from '@/lib/adminNav'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import { useAdminPermissions } from '@/hooks/useAdminPermissions'

interface Props {
  /** Abre o drawer com o menu completo — o 5º slot da barra. */
  onOpenMenu: () => void
  /** Bolinha de não-lidas no botão "Menu": as notificações vivem em telas que
   * não estão na barra, então sem esse sinal o usuário só descobriria abrindo
   * o menu por acaso. */
  hasAlert?: boolean
}

/**
 * Barra de navegação inferior — só existe no celular (< md).
 *
 * O drawer lateral sozinho custa dois toques e um gesto para QUALQUER troca de
 * tela, e o operador de balcão alterna entre Caixa, Comanda e Estoque o dia
 * inteiro. A barra inferior resolve isso em um toque e fica na zona que o
 * polegar alcança sem reposicionar o aparelho — o topo da tela, onde está o
 * botão de menu, é justamente a região mais difícil de alcançar com uma mão.
 *
 * Os itens saem de `tabBarItems()`, que aplica as mesmas regras de permissão e
 * de módulo da sidebar (lib/adminNav.ts).
 */
export default function MobileTabBar({ onOpenMenu, hasAlert }: Props) {
  const pathname = usePathname()
  const { site } = useSiteConfig()

  // Role e permissões vêm de cookie — inexistentes no SSR. O guard vive no
  // hook, que é o mesmo usado pela Sidebar: as duas mostram a mesma navegação e
  // não podem divergir na hidratação.
  const { isAdmin, can } = useAdminPermissions()

  const items = tabBarItems({
    isAdmin,
    enabledModules: site.enabledModules,
    hasPerm: can,
  })

  return (
    <nav
      aria-label="Navegação principal"
      className="fixed bottom-0 left-0 right-0 z-30 flex border-t border-surface-500 bg-surface-800 pb-safe md:hidden"
    >
      {items.map(({ href, label, short, icon: Icon }) => {
        const active = pathname === href || pathname.startsWith(href + '/')
        return (
          <Link
            key={href}
            href={href}
            aria-current={active ? 'page' : undefined}
            className={clsx(
              'flex flex-1 flex-col items-center justify-center gap-0.5 py-2 text-[10px] font-medium transition-colors',
              active ? 'text-brand-400' : 'text-gray-500',
            )}
          >
            <Icon className={clsx('h-5 w-5', active && 'text-brand-500')} />
            <span className="max-w-full truncate px-0.5">{short ?? label}</span>
          </Link>
        )
      })}

      <button
        onClick={onOpenMenu}
        aria-label="Abrir menu completo"
        className="relative flex flex-1 flex-col items-center justify-center gap-0.5 py-2 text-[10px] font-medium text-gray-500"
      >
        <Menu className="h-5 w-5" />
        <span>Menu</span>
        {hasAlert && (
          <span className="absolute right-1/4 top-1.5 h-2 w-2 rounded-full bg-red-500" />
        )}
      </button>
    </nav>
  )
}
