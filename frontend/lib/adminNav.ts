import {
  LayoutDashboard, Package, QrCode, ShoppingBag, Users, Megaphone,
  CreditCard, Shield, TrendingUp, BarChart2, Info, UserCog, Settings, Timer,
  Wallet, Plug, ClipboardList, MessageSquare, Receipt, Palette, LifeBuoy, Mail,
  Rocket, PartyPopper, Sparkles, UtensilsCrossed,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'

export interface NavItem {
  href: string
  label: string
  icon: LucideIcon
  /** Permissão exigida (`null` = visível para qualquer perfil autenticado). */
  perm: string | null
  /** Módulo do tenant que precisa estar habilitado em SiteConfig.enabledModules. */
  module?: string
  badge?: string
  /** Rótulo curto para a barra inferior do celular — "Frente de Caixa" não
   * cabe num alvo de 64px de largura, "Caixa" cabe. */
  short?: string
}

export interface NavSection {
  label: string
  adminOnly?: boolean
  items: NavItem[]
}

/** Definição única da navegação do admin — consumida pela Sidebar (desktop e
 * drawer) e pela MobileTabBar. Antes vivia dentro de Sidebar.tsx; foi extraída
 * quando a barra inferior do celular passou a precisar das MESMAS regras de
 * permissão e de módulo. Duplicar a lista significaria, mais cedo ou mais
 * tarde, um item aparecendo na barra do celular para quem não pode acessá-lo. */
export const NAV_SECTIONS: NavSection[] = [
  {
    label: 'Operacional',
    items: [
      // ComandaController exige a permissão `comandas`. Enquanto o menu pedia
      // `dashboard`, quem tinha só comandas não achava a tela, e quem tinha só
      // dashboard entrava pra tomar 403 na primeira chamada.
      // Sem `module`: comanda é plano base. Ver o comentário em ComandaController.
      { href: '/admin/comanda',      label: 'Comanda',          icon: Users,           badge: 'LIVE', perm: 'comandas', short: 'Comanda' },
      { href: '/admin/dashboard',    label: 'Painel Geral',     icon: LayoutDashboard,                perm: 'dashboard', short: 'Painel' },
      { href: '/admin/venda-avulsa', label: 'Frente de Caixa',  icon: ShoppingBag,                    perm: 'pdv', short: 'Caixa' },
      { href: '/admin/qrcodes',      label: 'Gatilhos QR Code', icon: QrCode,                         perm: 'qrcodes', module: 'restaurante', short: 'QR Code' },
    ],
  },
  {
    // Fica logo após Operacional de propósito: Perfis de Acesso precisa
    // existir ANTES de cadastrar um Operador (em Vendas & Clientes) — quem
    // configura a loja passa por aqui primeiro.
    label: 'Administração',
    adminOnly: true,
    items: [
      { href: '/admin/perfis',      label: 'Perfis de Acesso',  icon: UserCog,  perm: null },
      { href: '/admin/integracoes', label: 'Integrações',       icon: Plug,     perm: null },
      { href: '/admin/site',        label: 'Personalizar Site', icon: Palette,  perm: null },
      { href: '/admin/email',       label: 'E-mail',            icon: Mail,     perm: null },
      { href: '/admin/ia-config',   label: 'Assistente de IA',  icon: Sparkles, perm: null, module: 'ia' },
    ],
  },
  {
    label: 'Módulos',
    items: [
      { href: '/admin/fiscal',      label: 'Fiscal',            icon: Receipt,          perm: 'fiscal',     module: 'fiscal' },
      { href: '/admin/eventos',     label: 'Gestão de Eventos', icon: PartyPopper,      perm: 'eventos',    module: 'eventos' },
      { href: '/admin/comanda/restaurante', label: 'Restaurante', icon: UtensilsCrossed, perm: 'restaurante', module: 'restaurante' },
      { href: '/admin/suporte',     label: 'Suporte',           icon: LifeBuoy,         perm: 'suporte' },
    ],
  },
  {
    label: 'Vendas & Clientes',
    items: [
      { href: '/admin/usuarios',  label: 'Clientes',   icon: Users,         perm: 'usuarios' },
      { href: '/admin/crediario', label: 'Crediário',  icon: CreditCard,    perm: 'crediario' },
      { href: '/admin/reservas',  label: 'Pré-vendas', icon: ClipboardList, perm: 'estoque', module: 'estoque' },
    ],
  },
  {
    label: 'Estoque & Catálogo',
    items: [
      { href: '/admin/estoque', label: 'Estoque', icon: Package, perm: 'estoque', short: 'Estoque' },
    ],
  },
  {
    label: 'Financeiro',
    items: [
      { href: '/admin/financeiro',     label: 'Financeiro',          icon: TrendingUp, perm: 'financeiro' },
      { href: '/admin/contas-receber', label: 'Contas a Pagar/Rec',  icon: Wallet,     perm: 'financeiro' },
      { href: '/admin/relatorios',     label: 'Relatórios',          icon: BarChart2,  perm: 'relatorios' },
    ],
  },
  {
    label: 'Comunicação',
    items: [
      { href: '/admin/anuncios',   label: 'Anúncios',   icon: Megaphone,     perm: 'anuncios' },
      { href: '/admin/mensageria', label: 'Mensageria', icon: MessageSquare, perm: 'anuncios' },
      { href: '/admin/timer',      label: 'Timers',     icon: Timer,         perm: 'timers' },
    ],
  },
  {
    label: 'Compliance',
    items: [
      { href: '/admin/lgpd',             label: 'LGPD & Auditoria', icon: Shield, perm: 'lgpd' },
      { href: '/admin/primeiros-passos', label: 'Primeiros Passos', icon: Rocket, perm: null },
      { href: '/admin/sobre',            label: 'Sobre o Sistema',  icon: Info,   perm: null },
    ],
  },
  {
    label: 'Pessoal',
    items: [
      { href: '/admin/configuracoes', label: 'Configurações', icon: Settings, perm: null },
    ],
  },
]

export interface NavVisibilityCtx {
  isAdmin: boolean
  enabledModules: string[]
  hasPerm: (perm: string) => boolean
}

export function isItemVisible(item: NavItem, ctx: NavVisibilityCtx): boolean {
  if (item.module && !ctx.enabledModules.includes(item.module)) return false
  return item.perm === null || ctx.hasPerm(item.perm)
}

export function visibleSections(ctx: NavVisibilityCtx): NavSection[] {
  return NAV_SECTIONS
    .filter(s => !s.adminOnly || ctx.isAdmin)
    .map(s => ({ ...s, items: s.items.filter(i => isItemVisible(i, ctx)) }))
    .filter(s => s.items.length > 0)
}

/** Ordem de preferência da barra inferior do celular: as quatro telas de uso
 * diário de quem opera a loja no balcão. O quinto slot é sempre "Menu". Itens
 * sem permissão/módulo caem fora e o próximo da fila assume o lugar. */
const TAB_BAR_ORDER = [
  '/admin/venda-avulsa',
  '/admin/comanda',
  '/admin/dashboard',
  '/admin/estoque',
  '/admin/usuarios',
  '/admin/financeiro',
]

const ALL_ITEMS = NAV_SECTIONS.flatMap(s => s.items)

/** Até 4 itens para a barra inferior, respeitando permissões e módulos. */
export function tabBarItems(ctx: NavVisibilityCtx): NavItem[] {
  return TAB_BAR_ORDER
    .map(href => ALL_ITEMS.find(i => i.href === href))
    .filter((i): i is NavItem => !!i && isItemVisible(i, ctx))
    .slice(0, 4)
}

/** Título da tela atual — usado na barra superior do celular, onde não há
 * sidebar visível dizendo onde o usuário está. */
export function currentNavTitle(pathname: string): string | null {
  // Mais específico primeiro: /admin/lgpd/documento/1 deve casar com LGPD, e
  // não parar num prefixo mais curto que também case.
  const match = [...ALL_ITEMS]
    .sort((a, b) => b.href.length - a.href.length)
    .find(i => pathname === i.href || pathname.startsWith(i.href + '/'))
  return match?.label ?? null
}
