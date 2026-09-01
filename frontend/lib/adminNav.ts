import {
  LayoutDashboard, Package, QrCode, ShoppingBag, Users, Megaphone,
  CreditCard, Shield, TrendingUp, BarChart2, Info, UserCog, Settings, Timer,
  Wallet, Plug, ClipboardList, MessageSquare, Receipt, Palette, LifeBuoy, Mail,
  Rocket, PartyPopper, Sparkles, UtensilsCrossed, CircleHelp, ReceiptText,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'

export interface NavItem {
  href: string
  label: string
  icon: LucideIcon
  /** Permissão exigida (`null` = visível para qualquer perfil autenticado). */
  perm: string | null
  /** Algumas configurações pertencem exclusivamente ao dono da loja. */
  adminOnly?: boolean
  /** Módulo do tenant que precisa estar habilitado em SiteConfig.enabledModules. */
  module?: string
  badge?: string
  /** Rótulo curto para a barra inferior do celular. */
  short?: string
}

export interface NavSection {
  /** Nome orientado à tarefa que aparece no primeiro nível do menu. */
  label: string
  icon: LucideIcon
  items: NavItem[]
}

/**
 * Navegação orientada ao trabalho do cliente.
 *
 * O primeiro nível tem poucas áreas estáveis. As funções continuam com as
 * mesmas rotas e permissões, mas aparecem como subpáginas apenas dentro da
 * área ativa. Isso preserva links, atalhos e controle de acesso sem obrigar o
 * usuário a memorizar a arquitetura interna do produto.
 */
export const NAV_SECTIONS: NavSection[] = [
  {
    label: 'Início',
    icon: LayoutDashboard,
    items: [
      { href: '/admin/dashboard', label: 'Visão geral', icon: LayoutDashboard, perm: 'dashboard', short: 'Início' },
    ],
  },
  {
    label: 'Vendas',
    icon: ShoppingBag,
    items: [
      { href: '/admin/venda-avulsa', label: 'Frente de Caixa', icon: ShoppingBag, perm: 'pdv', short: 'Caixa' },
      { href: '/admin/comanda', label: 'Comandas', icon: Users, badge: 'LIVE', perm: 'comandas', short: 'Comanda' },
      { href: '/admin/comanda/restaurante', label: 'Restaurante', icon: UtensilsCrossed, perm: 'restaurante', module: 'restaurante' },
      { href: '/admin/reservas', label: 'Pré-vendas', icon: ClipboardList, perm: 'estoque', module: 'estoque' },
    ],
  },
  {
    label: 'Produtos',
    icon: Package,
    items: [
      { href: '/admin/estoque', label: 'Produtos e estoque', icon: Package, perm: 'estoque', short: 'Estoque' },
    ],
  },
  {
    label: 'Clientes e equipe',
    icon: Users,
    items: [
      { href: '/admin/usuarios', label: 'Clientes e operadores', icon: Users, perm: 'usuarios' },
      { href: '/admin/crediario', label: 'Crediário', icon: CreditCard, perm: 'crediario' },
      { href: '/admin/perfis', label: 'Perfis de acesso', icon: UserCog, perm: null, adminOnly: true },
    ],
  },
  {
    label: 'Financeiro',
    icon: TrendingUp,
    items: [
      { href: '/admin/financeiro', label: 'Visão financeira', icon: TrendingUp, perm: 'financeiro' },
      { href: '/admin/contas-receber', label: 'Contas a pagar e receber', icon: Wallet, perm: 'financeiro' },
      { href: '/admin/relatorios', label: 'Relatórios', icon: BarChart2, perm: 'relatorios' },
      { href: '/admin/fiscal', label: 'Fiscal', icon: Receipt, perm: 'fiscal', module: 'fiscal' },
    ],
  },
  {
    label: 'Comunicação',
    icon: Megaphone,
    items: [
      { href: '/admin/site', label: 'Site e aparência', icon: Palette, perm: null, adminOnly: true },
      { href: '/admin/qrcodes', label: 'QR Codes e mesas', icon: QrCode, perm: 'qrcodes', module: 'restaurante' },
      { href: '/admin/anuncios', label: 'Anúncios', icon: Megaphone, perm: 'anuncios' },
      { href: '/admin/mensageria', label: 'Mensageria', icon: MessageSquare, perm: 'anuncios' },
      { href: '/admin/email', label: 'E-mail', icon: Mail, perm: null, adminOnly: true },
      { href: '/admin/eventos', label: 'Eventos', icon: PartyPopper, perm: 'eventos', module: 'eventos' },
      { href: '/admin/timer', label: 'Timers', icon: Timer, perm: 'timers' },
    ],
  },
  {
    label: 'Configurações',
    icon: Settings,
    items: [
      { href: '/admin/configuracoes', label: 'Minhas preferências', icon: Settings, perm: null },
      // adminOnly: dado de cobrança é do dono da loja, não de quem opera o caixa.
      // O backend enforça o mesmo com [Authorize(Roles = "Admin")].
      { href: '/admin/assinatura', label: 'Assinatura', icon: ReceiptText, perm: null, adminOnly: true },
      { href: '/admin/integracoes', label: 'Integrações', icon: Plug, perm: null, adminOnly: true },
      { href: '/admin/ia-config', label: 'Assistente de IA', icon: Sparkles, perm: null, adminOnly: true, module: 'ia' },
      { href: '/admin/lgpd', label: 'LGPD e auditoria', icon: Shield, perm: 'lgpd' },
    ],
  },
  {
    label: 'Ajuda',
    icon: CircleHelp,
    items: [
      { href: '/admin/primeiros-passos', label: 'Guia inicial', icon: Rocket, perm: null },
      { href: '/admin/suporte', label: 'Suporte', icon: LifeBuoy, perm: 'suporte' },
      { href: '/admin/sobre', label: 'Sobre o sistema', icon: Info, perm: null },
    ],
  },
]

export interface NavVisibilityCtx {
  isAdmin: boolean
  enabledModules: string[]
  hasPerm: (perm: string) => boolean
}

export function isItemVisible(item: NavItem, ctx: NavVisibilityCtx): boolean {
  if (item.adminOnly && !ctx.isAdmin) return false
  if (item.module && !ctx.enabledModules.includes(item.module)) return false
  return item.perm === null || ctx.hasPerm(item.perm)
}

export function visibleSections(ctx: NavVisibilityCtx): NavSection[] {
  return NAV_SECTIONS
    .map(section => ({
      ...section,
      items: section.items.filter(item => isItemVisible(item, ctx)),
    }))
    .filter(section => section.items.length > 0)
}

const ALL_ITEMS = NAV_SECTIONS.flatMap(section => section.items)

/** Retorna somente a rota mais específica; evita dois itens ativos em rotas filhas. */
export function currentNavItem(pathname: string): NavItem | null {
  return [...ALL_ITEMS]
    .sort((a, b) => b.href.length - a.href.length)
    .find(item => pathname === item.href || pathname.startsWith(item.href + '/')) ?? null
}

export function currentNavSection(pathname: string, ctx: NavVisibilityCtx): NavSection | null {
  const current = currentNavItem(pathname)
  if (!current) return null
  return visibleSections(ctx).find(section => section.items.some(item => item.href === current.href)) ?? null
}

export function isCurrentNavItem(pathname: string, href: string): boolean {
  return currentNavItem(pathname)?.href === href
}

/** Ordem de preferência da barra inferior do celular. O quinto slot é Menu. */
const TAB_BAR_ORDER = [
  '/admin/venda-avulsa',
  '/admin/comanda',
  '/admin/dashboard',
  '/admin/estoque',
  '/admin/usuarios',
  '/admin/financeiro',
]

const ALL_NAV_ITEMS = NAV_SECTIONS.flatMap(section => section.items)

/** Até 4 itens de uso diário, respeitando permissões e módulos. */
export function tabBarItems(ctx: NavVisibilityCtx): NavItem[] {
  return TAB_BAR_ORDER
    .map(href => ALL_NAV_ITEMS.find(item => item.href === href))
    .filter((item): item is NavItem => !!item && isItemVisible(item, ctx))
    .slice(0, 4)
}

/** Título da tela atual usado na barra superior do celular. */
export function currentNavTitle(pathname: string): string | null {
  return currentNavItem(pathname)?.label ?? null
}
